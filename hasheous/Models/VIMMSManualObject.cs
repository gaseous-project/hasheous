namespace VIMMSLair
{
    public class ManualObject
    {
        public ManualObjectItem[] manuals { get; set; }
        public int count { get; set; }
        public int total { get; set; }

        public class ManualObjectItem
        {
            public long id { get; set; }
            public string system { get; set; }
            public int dpi { get; set; }
            public string[] datNames { get; set; }
            public string[] regions { get; set; }
        }
    }
}