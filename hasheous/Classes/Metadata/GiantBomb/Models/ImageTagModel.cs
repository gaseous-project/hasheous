namespace GiantBomb.Models
{
    public class GiantBombImageTagResponse : IBaseResponse<ImageTag>
    {

    }

    public class ImageTag
    {
        public string api_detail_url { get; set; }
        public string name { get; set; }
        public int total { get; set; }
    }
}
