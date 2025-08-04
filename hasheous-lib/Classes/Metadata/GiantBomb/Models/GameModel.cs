using System;
using System.Collections.Generic;

namespace GiantBomb.Models
{
    public class GiantBombGameResponse : IBaseResponse<Game>
    {

    }

    public class Game
    {
        public string aliases { get; set; }
        public string api_detail_url { get; set; }
        public string date_added { get; set; }
        public string date_last_updated { get; set; }
        public string deck { get; set; }
        public string description { get; set; }
        public string expected_release_day { get; set; }
        public string expected_release_month { get; set; }
        public string expected_release_quarter { get; set; }
        public string expected_release_year { get; set; }
        public string guid { get; set; }
        public long id { get; set; }
        public Image image { get; set; }
        public List<ImageTag> image_tags { get; set; }
        public string name { get; set; }
        public string number_of_user_reviews { get; set; }
        public List<Rating> original_game_rating { get; set; }
        public string original_release_date { get; set; }
        public List<Platform> platforms { get; set; }
        public string site_detail_url { get; set; }
    }
}