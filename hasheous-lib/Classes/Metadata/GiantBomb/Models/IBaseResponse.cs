using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GiantBomb.Models
{
    public class IBaseResponse<T> where T : class
    {
        public static readonly string BaseUrl = "https://www.giantbomb.com/api/";
        public static readonly string ReplacementUrl = "/api/v1/MetadataProxy/GiantBomb/";

        public string error { get; set; }
        public int limit { get; set; }
        public int offset { get; set; }
        public int number_of_page_results { get; set; }
        public int number_of_total_results { get; set; }
        public int status_code { get; set; }
        private List<T> _results;
        private bool _rewritten;

        public List<T> results
        {
            get
            {
                if (_results != null && !_rewritten)
                {
                    foreach (var item in _results)
                    {
                        RewriteUrls(item);
                    }
                    _rewritten = true;
                }
                return _results;
            }
            set
            {
                _results = value;
                _rewritten = false;
            }
        }

        public string version { get; set; }

        private static void RewriteUrls(object obj)
        {
            if (obj == null) return;

            var type = obj.GetType();
            // Primitive / string guard
            if (type == typeof(string) || type.IsPrimitive) return;

            foreach (var prop in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (!prop.CanRead || !prop.CanWrite) continue;

                var propType = prop.PropertyType;
                if (propType == typeof(string))
                {
                    if (prop.Name != "site_detail_url" && prop.Name.EndsWith("_url", StringComparison.Ordinal))
                    {
                        var val = prop.GetValue(obj) as string;
                        if (!string.IsNullOrEmpty(val) && val.Contains(BaseUrl, StringComparison.Ordinal))
                        {
                            prop.SetValue(obj, val.Replace(BaseUrl, ReplacementUrl, StringComparison.Ordinal));
                        }
                    }
                }
                else if (typeof(System.Collections.IEnumerable).IsAssignableFrom(propType) && propType != typeof(string))
                {
                    // Handle lists / enumerables
                    var enumerable = prop.GetValue(obj) as System.Collections.IEnumerable;
                    if (enumerable == null) continue;
                    foreach (var element in enumerable)
                    {
                        if (element == null) continue;
                        var elType = element.GetType();
                        if (elType.IsClass && elType != typeof(string))
                        {
                            RewriteUrls(element);
                        }
                    }
                }
                else if (propType.IsClass && propType != typeof(string))
                {
                    var child = prop.GetValue(obj);
                    if (child != null)
                    {
                        RewriteUrls(child);
                    }
                }
            }
        }
    }
}