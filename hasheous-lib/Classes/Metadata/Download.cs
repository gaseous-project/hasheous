using System.Net;
using Classes;

namespace hasheous_server.Classes
{
    public class DownloadTools
    {
        private static readonly HttpClient client = new HttpClient();

        /// <summary>
        /// Downloads a file from the specified URI to the specified destination file path.
        /// </summary>
        /// <param name="uri">
        /// The URI of the file to download.
        /// </param>
        /// <param name="DestinationFile">
        /// The local file path where the downloaded file will be saved.
        /// </param>
        /// <returns></returns>
        public static async Task<bool?> DownloadFile(Uri uri, string DestinationFile)
        {
            Logging.Log(Logging.LogType.Information, "Communications", "Downloading from " + uri.ToString() + " to " + DestinationFile);

            try
            {
                using (HttpResponseMessage response = client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).Result)
                {
                    response.EnsureSuccessStatusCode();

                    using (Stream contentStream = await response.Content.ReadAsStreamAsync(), fileStream = new FileStream(DestinationFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var totalRead = 0L;
                        var totalReads = 0L;
                        var buffer = new byte[8192];
                        var isMoreToRead = true;

                        do
                        {
                            var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                            if (read == 0)
                            {
                                isMoreToRead = false;
                            }
                            else
                            {
                                await fileStream.WriteAsync(buffer, 0, read);

                                totalRead += read;
                                totalReads += 1;

                                if (totalReads % 2000 == 0)
                                {
                                    Console.WriteLine(string.Format("total bytes downloaded so far: {0:n0}", totalRead));
                                }
                            }
                        }
                        while (isMoreToRead);
                    }
                }

                return true;
            }
            catch (HttpRequestException ex)
            {
                if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    if (File.Exists(DestinationFile))
                    {
                        FileInfo fi = new FileInfo(DestinationFile);
                        if (fi.Length == 0)
                        {
                            File.Delete(DestinationFile);
                        }
                    }
                }

                Logging.Log(Logging.LogType.Warning, "Download Images", "Error downloading file: ", ex);
            }

            return false;
        }

        /// <summary>
        /// Clones (if missing) or refreshes an existing git repository branch and reports if changes occurred.
        /// Change is detected only when:
        ///  - A fresh clone was performed.
        ///  - A pull advanced the current branch (HEAD commit changed).
        /// On any git failure an exception is thrown.
        /// </summary>
        /// <param name="repoUrl">Remote repository URL.</param>
        /// <param name="branch">Branch to clone or refresh.</param>
        /// <param name="localDirectory">Target local directory.</param>
        /// <returns>True if a clone happened or new commits were pulled; false if already up to date.</returns>
        public static async Task<bool> CloneOrRefreshRepoAsync(string repoUrl, string branch, string localDirectory)
        {
            if (string.IsNullOrWhiteSpace(repoUrl))
            throw new ArgumentException("repoUrl is required", nameof(repoUrl));
            if (string.IsNullOrWhiteSpace(branch))
            throw new ArgumentException("branch is required", nameof(branch));
            if (string.IsNullOrWhiteSpace(localDirectory))
            throw new ArgumentException("localDirectory is required", nameof(localDirectory));

            async Task<(int exitCode, string stdout, string stderr)> RunGitAsync(string args, string? workDir = null)
            {
            var psi = new System.Diagnostics.ProcessStartInfo("git", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            if (!string.IsNullOrEmpty(workDir))
                psi.WorkingDirectory = workDir;

            using var proc = System.Diagnostics.Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start git process.");

            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (!string.IsNullOrWhiteSpace(stdout))
                Logging.Log(Logging.LogType.Information, "Git", stdout.Trim());
            if (!string.IsNullOrWhiteSpace(stderr))
                Logging.Log(Logging.LogType.Warning, "Git", stderr.Trim());

            return (proc.ExitCode, stdout, stderr);
            }

            bool RepoMissing()
            {
            var gitDir = Path.Combine(localDirectory, ".git");
            return !Directory.Exists(localDirectory) || !Directory.Exists(gitDir);
            }

            // Fresh clone path
            if (RepoMissing())
            {
            Logging.Log(Logging.LogType.Information, "Git", $"Cloning {repoUrl} (branch {branch}) to {localDirectory}");
            Directory.CreateDirectory(localDirectory);
            var (cloneExit, _, _) = await RunGitAsync($"clone --branch \"{branch}\" --single-branch \"{repoUrl}\" \"{localDirectory}\"");
            if (cloneExit != 0)
                throw new Exception($"git clone failed (exit {cloneExit}) for {repoUrl}.");

            // Clone implies change.
            return true;
            }

            Logging.Log(Logging.LogType.Information, "Git", $"Refreshing repo at {localDirectory} (branch {branch})");

            // Ensure branch checked out
            var (checkoutExit, _, _) = await RunGitAsync($"checkout \"{branch}\"", localDirectory);
            if (checkoutExit != 0)
            throw new Exception($"git checkout failed (exit {checkoutExit}) for branch {branch}.");

            // Capture pre-pull HEAD
            var (headBeforeExit, headBefore, _) = await RunGitAsync("rev-parse HEAD", localDirectory);
            if (headBeforeExit != 0 || string.IsNullOrWhiteSpace(headBefore))
            throw new Exception("Failed to obtain current HEAD before pull.");

            // Fetch & prune
            var (fetchExit, _, _) = await RunGitAsync("fetch --all --prune", localDirectory);
            if (fetchExit != 0)
            throw new Exception("git fetch failed.");

            // Pull (ff-only)
            var (pullExit, pullStdout, pullStderr) = await RunGitAsync("pull --ff-only", localDirectory);
            if (pullExit != 0)
            throw new Exception($"git pull failed (exit {pullExit}). {pullStderr}");

            // Capture post-pull HEAD
            var (headAfterExit, headAfter, _) = await RunGitAsync("rev-parse HEAD", localDirectory);
            if (headAfterExit != 0 || string.IsNullOrWhiteSpace(headAfter))
            throw new Exception("Failed to obtain current HEAD after pull.");

            bool changed = !string.Equals(headBefore.Trim(), headAfter.Trim(), StringComparison.Ordinal);

            // Only perform hard reset & clean to ensure a pristine working tree (does not affect change detection).
            var (resetExit, _, _) = await RunGitAsync($"reset --hard origin/{branch}", localDirectory);
            if (resetExit != 0)
            throw new Exception("git reset --hard failed.");
            var (cleanExit, _, _) = await RunGitAsync("clean -fd", localDirectory);
            if (cleanExit != 0)
            throw new Exception("git clean failed.");

            if (changed)
            Logging.Log(Logging.LogType.Information, "Git", $"Changes detected for {localDirectory}: {headBefore.Trim()} -> {headAfter.Trim()}");
            else
            Logging.Log(Logging.LogType.Information, "Git", $"No changes detected for {localDirectory} (HEAD {headAfter.Trim()}).");

            return changed;
        }
    }
}