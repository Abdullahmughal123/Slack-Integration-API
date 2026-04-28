using SlackIntegration.DTOs;

namespace SlackIntegration.Interfaces;

public interface ISlackService
{
    string GetInstallUrl();
    Task<SlackWorkspaceDto> SaveTokenAsync(string code);
    Task<SlackMessageResultDto> SendMessageAsync(string message);
}
