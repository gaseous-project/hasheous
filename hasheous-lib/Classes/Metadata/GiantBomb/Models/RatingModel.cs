namespace GiantBomb.Models
{
    public class GiantBombRatingResponse : IBaseResponse<Rating>
    {

    }

    public class Rating
    {
        public string api_detail_url { get; set; }
        public string guid { get; set; }
        public long id { get; set; }
        public string image { get; set; }
        public string name { get; set; }
        public string rating_board { get; set; }
        public string site_detail_url { get; set; }
    }
}