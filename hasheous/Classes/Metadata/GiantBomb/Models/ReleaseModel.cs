using System.Data;

namespace GiantBomb.Models
{
    public class GiantBombReleaseResponse : IBaseResponse<Review>
    {

    }

    public class Release
    {
        public string? abbreviation { get; set; }
        public string? api_detail_url { get; set; }
        public Company? company { get; set; }
        public DateTime? date_added { get; set; }
        public DateTime? date_last_updated { get; set; }
        public string? deck { get; set; }
        public string? description { get; set; }
        public string? guid { get; set; }
        public long id { get; set; }
        public Image? image { get; set; }
        public List<ImageTag>? image_tags { get; set; }
        public string? install_base { get; set; }
        public string? name { get; set; }
        public string? online_support { get; set; }
        public string? original_price { get; set; }
        public DateTime? release_date { get; set; }
        public string? site_detail_url { get; set; }
    }
}