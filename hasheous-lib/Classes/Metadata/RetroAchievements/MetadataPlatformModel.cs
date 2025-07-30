namespace RetroAchievements.Models
{
    public class PlatformModel
    {
        public long ID { get; set; }
        public string Name { get; set; }
        public string IconURL { get; set; }
        public bool Active { get; set; }
        public bool IsGameSystem { get; set; }
        public List<GameModel>? Games { get; set; }
    }
}