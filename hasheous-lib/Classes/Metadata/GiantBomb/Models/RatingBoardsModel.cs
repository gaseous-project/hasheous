namespace GiantBomb.Models
{
    public class GiantBombRatingBoardsResponse : IBaseResponse<RatingBoards>
    {

    }

    public class RatingBoards
    {
        public string api_detail_url { get; set; }
        public string date_added { get; set; }
        public string date_last_updated { get; set; }
        public string deck { get; set; }
        public string description { get; set; }
        public string guid { get; set; }
        public long id { get; set; }
        public Image image { get; set; }
        public string name { get; set; }
        public string site_detail_url { get; set; }
    }
}