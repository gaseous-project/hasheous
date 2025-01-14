namespace TheGamesDB.JSON
{
    public class TheGamesDBDatabase
    {
        public int code { get; set; }
        public string status { get; set; }
        public long last_edit_id { get; set; }
        public DataItem data { get; set; }
        public IncludeItem include { get; set; }

        public class DataItem
        {
            public long count { get; set; }
            public List<GameItem> games { get; set; }
            public class GameItem
            {
                public long id { get; set; }
                public string game_title { get; set; }
                public string? release_date { get; set; }
                public long? platform { get; set; }
                public int? region_id { get; set; }
                public int? country_id { get; set; }
                public int? players { get; set; }
                public string? overview { get; set; }
                public string? last_updated { get; set; }
                public string? rating { get; set; }
                public string? coop { get; set; }
                public string? youtube { get; set; }
                public string? os { get; set; }
                public string? processor { get; set; }
                public string? ram { get; set; }
                public string? hdd { get; set; }
                public string? video { get; set; }
                public string? sound { get; set; }
                public long[]? developers { get; set; }
                public long[]? genres { get; set; }
                public long[]? publishers { get; set; }
                public string[]? alternates { get; set; }
                public UidItem[]? uids { get; set; }
                public string[]? hashes { get; set; }

                public class UidItem
                {
                    public string? uid { get; set; }
                    public string? games_uids_patterns_id { get; set; }
                }
            }
        }
        public class IncludeItem
        {
            public BoxartItem boxart { get; set; }
            public PlatformItem platform { get; set; }

            public class BoxartItem
            {
                public Dictionary<string, string> base_url { get; set; }
                public Dictionary<string, DataItem[]> data { get; set; }

                public class DataItem
                {
                    public long id { get; set; }
                    public ImageType? type { get; set; }
                    public ImageSide? side { get; set; }
                    public string? filename { get; set; }
                    public string? resolution { get; set; }

                    public enum ImageType
                    {
                        boxart,
                        banner,
                        screenshot,
                        fanart,
                        clearlogo,
                        consoleart,
                        controllerart,
                        banner_thumb,
                        screenshot_thumb,
                        fanart_thumb,
                        clearlogo_thumb,
                        consoleart_thumb,
                        controllerart_thumb
                    }

                    public enum ImageSide
                    {
                        front,
                        back,
                        spine,
                        top,
                        bottom,
                        left,
                        right,
                        screenshot
                    }
                }
            }

            public class PlatformItem
            {
                public Dictionary<string, DataItem> data { get; set; }
                public class DataItem
                {
                    public long id { get; set; }
                    public string name { get; set; }
                    public string alias { get; set; }
                }
            }
        }
    }
}