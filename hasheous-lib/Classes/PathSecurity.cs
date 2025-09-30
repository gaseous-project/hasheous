using System.Text.RegularExpressions;
using System.IO.Compression;

namespace Classes
{
    /// <summary>
    /// Security related helpers for validating archive entry paths (e.g., to prevent Zip Slip).
    /// </summary>
    public static class PathSecurity
    {
        private static readonly Regex TraversalSegmentRegex = new Regex(@"(^|[\\/])\.\.($|[\\/])", RegexOptions.Compiled);

        /// <summary>
        /// Determines whether a zip (or similar archive) entry path is unsafe (Zip Slip attempt).
        /// Rules:
        /// - Allows occurrences of ".." inside file names when not bounded by a path separator (e.g. "My Game...dat", "name..ext").
        /// - Blocks when ".." appears as a path segment or adjacent to a separator (segment patterns: "../", "/../", "..\\", "\\..", starts with "../", ends with "/..", etc.).
        /// - Also blocks if the combined full path escapes the intended extraction root.
        /// </summary>
        /// <param name="extractionRootFullPath">Full path to the extraction root (must be a fully qualified path, ending with a directory separator recommended).</param>
        /// <param name="entryFullName">The raw entry path inside the archive.</param>
        /// <returns>True if unsafe and should be skipped; false if considered safe.</returns>
        public static bool IsZipSlipUnsafe(string extractionRootFullPath, string entryFullName)
        {
            if (string.IsNullOrWhiteSpace(entryFullName)) return true; // treat empty as unsafe
            if (string.IsNullOrEmpty(extractionRootFullPath)) return true;

            // Normalize separators
            var normalized = entryFullName.Replace('\\', '/');

            // Explicit fast-path checks for directory traversal patterns to appease static analysis (GitHub Advanced Security).
            // We only block when '..' is used as a path segment (preceded or followed by a separator or string boundary).
            // This allows benign filenames like "My Game...dat" or "name..ext".
            if (normalized.Contains("../", StringComparison.Ordinal) ||
                normalized.StartsWith("../", StringComparison.Ordinal) ||
                normalized.EndsWith("/..", StringComparison.Ordinal))
            {
                return true;
            }

            // Detect traversal segment strictly when '..' is its own segment (preceded or followed by separator or string boundary)
            if (TraversalSegmentRegex.IsMatch(normalized))
            {
                return true; // explicit directory traversal segment detected
            }

            // Build destination path and ensure containment
            try
            {
                // Reconstruct using platform separators
                var platformPath = normalized.Replace('/', System.IO.Path.DirectorySeparatorChar);
                var destination = System.IO.Path.GetFullPath(System.IO.Path.Combine(extractionRootFullPath, platformPath));

                var rootFull = extractionRootFullPath;
                if (!rootFull.EndsWith(System.IO.Path.DirectorySeparatorChar))
                {
                    rootFull += System.IO.Path.DirectorySeparatorChar;
                }

                if (!destination.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                {
                    return true; // escaped the root
                }
            }
            catch
            {
                return true; // any path resolution exception treat as unsafe
            }

            return false; // safe
        }

        /// <summary>
        /// Safely extracts a zip archive to a destination directory, protecting against Zip Slip (path traversal) attacks.
        /// Allows benign occurrences of ".." inside filenames that are not directory traversal segments.
        /// </summary>
        /// <param name="zipFilePath">Path to the zip file.</param>
        /// <param name="destinationDirectory">Destination directory (created if missing).</param>
        /// <param name="renameOnCollision">If true, existing files are not overwritten: a GUID suffix is appended.</param>
        /// <param name="onSkippedEntry">Optional callback invoked with the entry FullName when skipped (unsafe or directory traversal).</param>
        public static void ExtractZipSafely(string zipFilePath, string destinationDirectory, bool renameOnCollision = true, Action<string>? onSkippedEntry = null)
        {
            if (!File.Exists(zipFilePath)) return;
            if (!Directory.Exists(destinationDirectory)) Directory.CreateDirectory(destinationDirectory);

            var rootFull = Path.GetFullPath(destinationDirectory);
            if (!rootFull.EndsWith(Path.DirectorySeparatorChar)) rootFull += Path.DirectorySeparatorChar;

            using var archive = ZipFile.OpenRead(zipFilePath);
            foreach (var entry in archive.Entries)
            {
                // Skip directory entries
                if (string.IsNullOrEmpty(entry.Name) && (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\")))
                {
                    continue;
                }

                // Explicit fast-path traversal pattern checks (mirrors logic in IsZipSlipUnsafe for static analysis clarity)
                var rawNormalized = entry.FullName.Replace('\\', '/');
                if (rawNormalized.Contains("../", StringComparison.Ordinal) ||
                    rawNormalized.StartsWith("../", StringComparison.Ordinal) ||
                    rawNormalized.EndsWith("/..", StringComparison.Ordinal))
                {
                    onSkippedEntry?.Invoke(entry.FullName);
                    continue;
                }

                if (IsZipSlipUnsafe(rootFull, entry.FullName))
                {
                    onSkippedEntry?.Invoke(entry.FullName);
                    continue;
                }

                var normalized = rawNormalized; // already normalized above
                var destinationPath = Path.GetFullPath(Path.Combine(destinationDirectory, normalized.Replace('/', Path.DirectorySeparatorChar)));

                // Ensure the resolved path is still within extraction root (prevents traversal)
                if (!destinationPath.StartsWith(rootFull, StringComparison.Ordinal))
                {
                    onSkippedEntry?.Invoke(entry.FullName + " (traversal detected)");
                    continue;
                }

                var destDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

                if (File.Exists(destinationPath))
                {
                    if (!renameOnCollision)
                    {
                        onSkippedEntry?.Invoke(entry.FullName + " (collision)");
                        continue;
                    }
                    var newFileName = $"{Path.GetFileNameWithoutExtension(entry.Name)}_{Guid.NewGuid()}{Path.GetExtension(entry.Name)}";
                    destinationPath = Path.Combine(destDir ?? destinationDirectory, newFileName);
                }

                entry.ExtractToFile(destinationPath);
            }
        }
    }
}
