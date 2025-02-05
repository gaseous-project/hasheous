namespace RetroAchievements.Models
{
    public class GameModel
    {
        public long ID { get; set; }
        public string Title { get; set; }
        public long ConsoleID { get; set; }
        public string ConsoleName { get; set; }
        public string? ImageIcon { get; set; }
        public int? NumAchievements { get; set; }
        public int? NumLeaderboards { get; set; }
        public int? Points { get; set; }
        public DateTime? DateModified { get; set; }
        public long? ForumTopicID { get; set; }
        public List<string>? Hashes { get; set; }
        public List<GameHashesModel>? GameHashes { get; set; }
    }
}