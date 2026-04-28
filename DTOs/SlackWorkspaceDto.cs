namespace SlackIntegration.DTOs;

public class SlackWorkspaceDto
{
    public int Id { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SlackMessageResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ChannelId { get; set; }
    public string? Timestamp { get; set; }
    public string? Error { get; set; }
}

public class SlackInstallResponseDto
{
    public string Message { get; set; } = string.Empty;
    public string InstallUrl { get; set; } = string.Empty;
    public string RedirectInstructions { get; set; } = string.Empty;
}

public class SlackMessageRequestDto
{
    public string Message { get; set; } = string.Empty;
}
