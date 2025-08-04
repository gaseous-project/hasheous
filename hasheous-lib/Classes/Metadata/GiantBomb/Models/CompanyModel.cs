namespace GiantBomb.Models
{
    public class GiantBombCompanyResponse : IBaseResponse<Company>
    {

    }

    public class Company
    {
        public string api_detail_url { get; set; }
        public long id { get; set; }
        public string name { get; set; }
    }
}