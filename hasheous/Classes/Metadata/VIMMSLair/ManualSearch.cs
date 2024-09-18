using Classes;
using IGDB.Models;
using Newtonsoft.Json;

namespace VIMMSLair
{
    public class ManualSearch
    {
        public static string? Search(string PlatformName, string GameName)
        {
            Logging.Log(Logging.LogType.Information, "VIMMSLair", "Searching for manual for " + GameName + " on " + PlatformName);

            // download the manual list
            var manualDownloader = new ManualDownloader(PlatformName);
            manualDownloader.Download();

            // load the json into a ManualObject
            var manualObject = new ManualObject();
            string json = File.ReadAllText(manualDownloader.LocalFileName);
            manualObject = JsonConvert.DeserializeObject<ManualObject>(json);

            // search for the game
            foreach (var manual in manualObject.manuals)
            {
                foreach (var datName in manual.datNames)
                {
                    if (datName.ToLower() == GameName.ToLower())
                    {
                        Logging.Log(Logging.LogType.Information, "VIMMSLair", "Found manual for " + GameName + " on " + PlatformName);
                        return manual.id.ToString();
                    }
                }
            }

            Logging.Log(Logging.LogType.Information, "VIMMSLair", "No manual found for " + GameName + " on " + PlatformName);
            return "";
        }
    }
}