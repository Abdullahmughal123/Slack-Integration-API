public class SlackWorkspace
{
    public int Id { get; set; }
    public string? TeamName { get; set; }
    public string? EncryptedToken { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}