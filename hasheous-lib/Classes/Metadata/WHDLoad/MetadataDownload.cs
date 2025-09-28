using Classes;
using hasheous_server.Classes;

namespace WHDLoad
{
    public class DownloadManager
    {
        public static string GitUrl { get; } = "https://github.com/BlitterStudio/amiberry.git";

        public async Task Download()
        {
            try
            {
                // setup output directory
                string extractDir = System.IO.Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_WHDLoad);
                if (Directory.Exists(extractDir)) { Directory.Delete(extractDir, true); }
                Directory.CreateDirectory(extractDir);

                // clone the repository
                Logging.Log(Logging.LogType.Information, "WHDLoad", $"Cloning WHDLoad metadata repository to '{extractDir}'...");
                var gitProcess = new System.Diagnostics.Process();
                gitProcess.StartInfo.FileName = "git";
                gitProcess.StartInfo.Arguments = $"clone {GitUrl} \"{extractDir}\"";
                gitProcess.StartInfo.RedirectStandardOutput = true;
                gitProcess.StartInfo.RedirectStandardError = true;
                gitProcess.StartInfo.UseShellExecute = false;
                gitProcess.StartInfo.CreateNoWindow = true;

                gitProcess.Start();
                string output = await gitProcess.StandardOutput.ReadToEndAsync();
                string error = await gitProcess.StandardError.ReadToEndAsync();
                await gitProcess.WaitForExitAsync();

                if (gitProcess.ExitCode != 0)
                {
                    throw new Exception($"Git clone failed with exit code {gitProcess.ExitCode}: {error}");
                }

                // cleanup signature processed directory
                string tosecProcessedDir = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesProcessedDirectory, "WHDLoad");
                if (Directory.Exists(tosecProcessedDir)) { Directory.Delete(tosecProcessedDir, true); }

                // copy the signature files to the processing directory
                string datFile = Path.Combine(extractDir, "whdboot", "game-data", "whdload_db.xml");
                if (File.Exists(datFile))
                {
                    string destDir = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, "WHDLoad");
                    if (Directory.Exists(destDir)) { Directory.Delete(destDir, true); }
                    Directory.CreateDirectory(destDir);
                    string destFile = Path.Combine(destDir, "whdload_db.dat");
                    File.Copy(datFile, destFile);

                    Logging.Log(Logging.LogType.Information, "WHDLoad", $"WHDLoad metadata file copied to processing directory: {destFile}");
                }
                else
                {
                    throw new Exception($"WHDLoad metadata file not found in cloned repository: {datFile}");
                }

                Logging.Log(Logging.LogType.Information, "WHDLoad", "WHDLoad metadata processing completed successfully.");
            }
            catch (Exception ex)
            {
                Logging.Log(Logging.LogType.Critical, "WHDLoad", $"Error downloading WHDLoad metadata: {ex.Message}");
            }
        }
    }
}