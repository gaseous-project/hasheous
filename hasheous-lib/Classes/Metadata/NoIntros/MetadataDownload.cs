namespace NoIntros
{
    public class DownloadManager
    {
        private static readonly HttpClient client = new HttpClient();

        public string datUrl
        {
            get
            {
                return "https://no-intro.org/dat/No-Intro.dat";
            }
        }

        public string dbUrl
        {
            get
            {
                return "https://no-intro.org/db/No-Intro.db";
            }
        }
    }
}