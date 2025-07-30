namespace RetroAchievements.Models
{
    public class GameHashesModel
    {
        public string? Name { get; set; }
        public string? MD5 { get; set; }
        public string[]? Labels { get; set; }
        public string? PatchUrl { get; set; }
    }
}