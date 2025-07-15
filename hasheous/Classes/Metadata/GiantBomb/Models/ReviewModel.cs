namespace GiantBomb.Models
{
    public class GiantBombReviewResponse : IBaseResponse<Review>
    {

    }

    public class Review
    {
        public string? api_detail_url { get; set; }
        public string? deck { get; set; }
        public string? description { get; set; }
        public string? dlc_name { get; set; }
        public Game? game { get; set; }
        public string? guid { get; set; }
        public long id { get; set; }
        public string? platforms { get; set; }
        public DateTime? publish_date { get; set; }
        public Release? release { get; set; }
        public string? reviewer { get; set; }
        public int? score { get; set; }
        public string? site_detail_url { get; set; }
    }
}