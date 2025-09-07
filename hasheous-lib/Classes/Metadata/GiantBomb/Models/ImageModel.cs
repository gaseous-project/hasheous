using System.Text.Json.Serialization;

namespace GiantBomb.Models
{
    public class GiantBombImageResponse : IBaseResponse<Image>
    {

    }

    public class Image
    {
        // rewrite the URL to go via the proxy
        string baseurl = "https://www.giantbomb.com/";
        string replacementurl = "/api/v1/MetadataProxy/GiantBomb/";

        private string? _icon_url;
        public string icon_url
        {
            get
            {
                if (string.IsNullOrEmpty(_icon_url))
                {
                    return _icon_url;
                }
                else
                {
                    return _icon_url.Replace(baseurl, replacementurl);
                }
            }
            set
            {
                _icon_url = value;
            }
        }

        private string? _medium_url;
        public string medium_url
        {
            get
            {
                if (string.IsNullOrEmpty(_medium_url))
                {
                    return _medium_url;
                }
                else
                {
                    return _medium_url.Replace(baseurl, replacementurl);
                }
            }
            set
            {
                _medium_url = value;
            }
        }

        private string? _screen_url;
        public string screen_url
        {
            get
            {
                if (string.IsNullOrEmpty(_screen_url))
                {
                    return _screen_url;
                }
                else
                {
                    return _screen_url.Replace(baseurl, replacementurl);
                }
            }
            set
            {
                _screen_url = value;
            }
        }

        private string? _screen_large_url;
        public string screen_large_url
        {
            get
            {
                if (string.IsNullOrEmpty(_screen_large_url))
                {
                    return _screen_large_url;
                }
                else
                {
                    return _screen_large_url.Replace(baseurl, replacementurl);
                }
            }
            set
            {
                _screen_large_url = value;
            }
        }

        private string? _small_url;
        public string small_url
        {
            get
            {
                if (string.IsNullOrEmpty(_small_url))
                {
                    return _small_url;
                }
                else
                {
                    return _small_url.Replace(baseurl, replacementurl);
                }
            }
            set
            {
                _small_url = value;
            }
        }

        private string? _super_url;
        public string super_url
        {
            get
            {
                if (string.IsNullOrEmpty(_super_url))
                {
                    return _super_url;
                }
                else
                {
                    return _super_url.Replace(baseurl, replacementurl);
                }
            }
            set
            {
                _super_url = value;
            }
        }

        private string? _thumb_url;
        public string thumb_url
        {
            get
            {
                if (string.IsNullOrEmpty(_thumb_url))
                {
                    return _thumb_url;
                }
                else
                {
                    return _thumb_url.Replace(baseurl, replacementurl);
                }
            }
            set
            {
                _thumb_url = value;
            }
        }

        private string? _tiny_url;
        public string tiny_url
        {
            get
            {
                if (string.IsNullOrEmpty(_tiny_url))
                {
                    return _tiny_url;
                }
                else
                {
                    return _tiny_url.Replace(baseurl, replacementurl);
                }
            }
            set
            {
                _tiny_url = value;
            }
        }

        private string? _original_url;
        public string original_url
        {
            get
            {
                if (string.IsNullOrEmpty(_original_url))
                {
                    return _original_url;
                }
                else
                {
                    return _original_url.Replace(baseurl, replacementurl);
                }
            }
            set
            {
                _original_url = value;
            }
        }

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string guid { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        [System.Text.Json.Serialization.JsonIgnore]
        public string image_tags { get; set; }
    }
}