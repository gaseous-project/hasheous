namespace GiantBomb.Models
{
    public class GiantBombUserReviewResponse : IBaseResponse<UserReview>
    {

    }

    public class UserReview
    {
        public string? api_detail_url { get; set; }
        public DateTime? date_added { get; set; }
        public DateTime? date_last_updated { get; set; }
        public string? deck { get; set; }
        public string? description { get; set; }
        public Game game { get; set; }
        public string? release { get; set; }
        public string? dlc { get; set; }
        public string? guid { get; set; }
        public long id { get; set; }
        public string? reviewer { get; set; }
        public int? score { get; set; }
        public string? site_detail_url { get; set; }
    }
}