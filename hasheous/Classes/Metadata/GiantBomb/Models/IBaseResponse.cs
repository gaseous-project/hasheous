using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GiantBomb.Models
{
    public class IBaseResponse<T> where T : class
    {
        public string error { get; set; }
        public int limit { get; set; }
        public int offset { get; set; }
        public int number_of_page_results { get; set; }
        public int number_of_total_results { get; set; }
        public int status_code { get; set; }
        public List<T> results { get; set; }
        public string version { get; set; }
    }
}