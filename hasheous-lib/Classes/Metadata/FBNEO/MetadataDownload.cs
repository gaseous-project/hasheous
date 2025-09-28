using Classes;
using hasheous_server.Classes;

namespace FBNEO
{
    public class DownloadManager
    {
        public static string GitUrl { get; } = "https://github.com/libretro/FBNeo.git";

        public async Task Download()
        {
            try
            {
                // setup output directory
                string extractDir = System.IO.Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_FBNEO);
                if (Directory.Exists(extractDir)) { Directory.Delete(extractDir, true); }
                Directory.CreateDirectory(extractDir);

                // clone the repository
                Logging.Log(Logging.LogType.Information, "WHDLoad", $"Cloning FBNEO metadata repository to '{extractDir}'...");
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
                string tosecProcessedDir = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesProcessedDirectory, "FBNeo");
                if (Directory.Exists(tosecProcessedDir)) { Directory.Delete(tosecProcessedDir, true); }

                // copy the signature files to the processing directory
                string datFile = Path.Combine(extractDir, "dats");
                if (Directory.Exists(datFile))
                {
                    foreach (var file in Directory.GetFiles(datFile, "*.dat", SearchOption.TopDirectoryOnly))
                    {
                        string destFile = Path.Combine(Config.LibraryConfiguration.LibrarySignaturesDirectory, "FBNeo", Path.GetFileName(file));
                        File.Copy(file, destFile, true);
                    }
                }
                else
                {
                    throw new Exception($"DAT files directory not found in the cloned repository: {datFile}");
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