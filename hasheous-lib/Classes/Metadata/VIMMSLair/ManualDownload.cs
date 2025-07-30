using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Classes;

namespace VIMMSLair
{
    public class ManualDownloader
    {
        public ManualDownloader(string PlatformName)
        {
            _PlatformName = PlatformName;
        }

        private string _PlatformName;
        public string PlatformName
        {
            get
            {
                return _PlatformName;
            }
        }

        public string Url
        {
            get
            {
                return "https://vimm.net/manual/?mode=Advanced&p=list&system=" + _PlatformName + "&datname=*&f=json";
            }
        }

        public string LocalFilePath
        {
            get
            {
                return Path.Combine(Config.LibraryConfiguration.LibraryMetadataDirectory_VIMMSLair, "Manuals");
            }
        }

        public string LocalFileName
        {
            get
            {
                return Path.Combine(LocalFilePath, _PlatformName + ".json");

            }
        }

        public int MaxAgeInDays { get; set; } = 30;

        private bool IsLocalCopyOlderThanMaxAge()
        {
            if (!File.Exists(LocalFileName))
            {
                return true;
            }

            var lastWriteTime = File.GetLastWriteTime(LocalFileName);
            var age = DateTime.Now - lastWriteTime;
            return age.TotalDays > MaxAgeInDays;
        }

        public async Task<string> Download()
        {
            if (!Directory.Exists(LocalFilePath))
            {
                Directory.CreateDirectory(LocalFilePath);
            }

            if (IsLocalCopyOlderThanMaxAge() == true)
            {
                Logging.Log(Logging.LogType.Information, "VIMMSLair", "Downloading " + _PlatformName + " manual metadata from VIMMSLair");
                using (var client = new WebClient())
                {
                    var json = client.DownloadString(Url);
                    await File.WriteAllTextAsync(LocalFileName, json);
                }
            }
            else
            {
                Logging.Log(Logging.LogType.Information, "VIMMSLair", "Using local copy of " + _PlatformName + " manual metadata");
            }

            return LocalFileName;
        }
    }
}