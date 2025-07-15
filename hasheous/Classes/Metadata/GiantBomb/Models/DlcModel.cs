using System;
using System.Collections.Generic;

namespace GiantBomb.Models
{
    public class GiantBombDlcResponse : IBaseResponse<Dlc>
    {

    }

    public class Dlc
    {
        public string api_detail_url { get; set; }
        public string date_added { get; set; }
        public string date_last_updated { get; set; }
        public string deck { get; set; }
        public string description { get; set; }
        public Game? game { get; set; }
        public string guid { get; set; }
        public long id { get; set; }
        public Image image { get; set; }
        public string name { get; set; }
        public Platform? platform { get; set; }
        public DateTime? release_date { get; set; }
        public string site_detail_url { get; set; }
    }
}