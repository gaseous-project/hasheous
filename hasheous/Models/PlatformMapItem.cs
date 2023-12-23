namespace hasheous_server.Models
{
    public class PlatformMapItem
    {
        public long IGDBId { get; set; }
        public string IGDBName { get; set; }
        public string IGDBSlug { get; set; }
        public List<string> AlternateNames { get; set; } = new List<string>();
        
        public FileExtensions Extensions { get; set; }
        public class FileExtensions
        {
            public List<string> SupportedFileExtensions { get; set; } = new List<string>();

            public List<string> UniqueFileExtensions { get; set; } = new List<string>();
        }

        public string RetroPieDirectoryName { get; set; }
        public WebEmulatorItem? WebEmulator { get; set; }

        public class WebEmulatorItem
        {
            public string Type { get; set; }
            public string Core { get; set; }

            public List<AvailableWebEmulatorItem> AvailableWebEmulators { get; set; } = new List<AvailableWebEmulatorItem>();

            public class AvailableWebEmulatorItem
            {
                public string EmulatorType { get; set; }
                public List<AvailableWebEmulatorCoreItem> AvailableWebEmulatorCores { get; set; } = new List<AvailableWebEmulatorCoreItem>();

                public class AvailableWebEmulatorCoreItem
                {
                    public string Core { get; set; }
                    public string? AlternateCoreName { get; set; } = "";
                    public bool Default { get; set; } = false;
                }
            }
        }

        public List<EmulatorBiosItem> Bios { get; set; }

        public class EmulatorBiosItem
        {
            public string hash { get; set; }
            public string description { get; set; }
            public string filename { get; set; }
        }

        public BackgroundMetadataMatcher.BackgroundMetadataMatcher.MatchMethod MatchMethod { get; set; }
    }
}
