namespace hasheous_server.Models
{
    public class CompanyItem : CompanyItemModel
    {
        public long Id { get; set; }
        public List<Dictionary<string, object>>? SignatureCompanies { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime UpdatedDate { get; set; }
    }
}