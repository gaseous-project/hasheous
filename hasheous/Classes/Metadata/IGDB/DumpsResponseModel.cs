namespace InternetGameDatabase.Models
{
    public class DumpsResponseModel
    {
        public string? s3_url { get; set; }
        public string? endpoint { get; set; }
        public string? file_name { get; set; }
        public long? size_bytes { get; set; }
        public long? updated_at { get; set; }
        public string? schema_version { get; set; }
        public Dictionary<string, object>? schema { get; set; }
    }
}