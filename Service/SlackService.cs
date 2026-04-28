using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SlackIntegration.Interfaces;
using SlackIntegration.DTOs;
using SlackIntegration.Exceptions;

namespace SlackIntegration.Services;

public class SlackService : ISlackService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<SlackService> _logger;

    private const string CHANNEL_ID = "C0AUZDUT4PR";
    private const string OAUTH_URL = "https://slack.com/oauth/v2/authorize";
    private const string TOKEN_EXCHANGE_URL = "https://slack.com/api/oauth.v2.access";
    private const string MESSAGE_SEND_URL = "https://slack.com/api/chat.postMessage";
    private const string REQUIRED_SCOPES = "chat:write,chat:write.public,channels:read,groups:read";

    public SlackService(
        HttpClient http, 
        IConfiguration config, 
        AppDbContext db, 
        IEncryptionService encryptionService, 
        ILogger<SlackService> logger)
    {
        _http = http;
        _config = config;
        _db = db;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public string GetInstallUrl()
    {
        try
        {
            var clientId = GetSlackClientId();
            var redirectUrl = GetSlackRedirectUrl();

            var installUrl = $"{OAUTH_URL}?client_id={clientId}&scope={REQUIRED_SCOPES}&redirect_uri={redirectUrl}";
            _logger.LogInformation("Generated Slack install URL for client: {ClientId}", clientId);
            
            return installUrl;
        }
        catch (Exception ex) when (!(ex is SlackIntegrationException))
        {
            _logger.LogError(ex, "Failed to generate Slack install URL");
            throw new SlackIntegrationException("Failed to generate install URL", "INSTALL_URL_ERROR", ex);
        }
    }

    public async Task<SlackWorkspaceDto> SaveTokenAsync(string code)
    {
        try
        {
            ValidateAuthorizationCode(code);

            var tokenExchangeRequest = BuildTokenExchangeRequest(code);
            var response = await _http.PostAsync(TOKEN_EXCHANGE_URL, new FormUrlEncodedContent(tokenExchangeRequest));
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Slack OAuth response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                throw new SlackIntegrationException($"OAuth token exchange failed: {response.StatusCode}", "OAUTH_HTTP_ERROR");
            }

            var tokenResponse = JsonSerializer.Deserialize<SlackTokenResponse>(responseContent);
            ValidateTokenResponse(tokenResponse);

            var encryptedToken = _encryptionService.Encrypt(tokenResponse!.AccessToken!);
            var workspace = await SaveWorkspaceToDatabase(tokenResponse, encryptedToken);

            _logger.LogInformation("Successfully saved token for workspace: {WorkspaceName}", workspace.TeamName);

            return new SlackWorkspaceDto
            {
                Id = workspace.Id,
                TeamName = workspace.TeamName ?? "Unknown",
                CreatedAt = workspace.CreatedAt
            };
        }
        catch (Exception ex) when (!(ex is SlackIntegrationException))
        {
            _logger.LogError(ex, "Failed to save OAuth token");
            throw new SlackIntegrationException("Failed to save OAuth token", "OAUTH_SAVE_ERROR", ex);
        }
    }

    public async Task<SlackMessageResultDto> SendMessageAsync(string message)
    {
        try
        {
            ValidateMessage(message);

            var workspace = await GetConnectedWorkspace();
            var token = _encryptionService.Decrypt(workspace.EncryptedToken!);
            ValidateBotToken(token);

            _logger.LogInformation("Attempting to send message to channel {ChannelId}", CHANNEL_ID);

            var messageRequest = BuildMessageRequest(token, message);
            var response = await _http.SendAsync(messageRequest);
            
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Slack API response: {Response}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                var error = $"Slack API error: {response.StatusCode} - {responseContent}";
                return new SlackMessageResultDto
                {
                    Success = false,
                    Message = "Failed to send message",
                    Error = error
                };
            }

            var slackResponse = JsonSerializer.Deserialize<SlackMessageResponse>(responseContent);
            
            if (slackResponse == null || !slackResponse.Ok)
            {
                var error = $"Slack returned error: {slackResponse?.Error ?? "Unknown error"}";
                return new SlackMessageResultDto
                {
                    Success = false,
                    Message = "Failed to send message",
                    Error = error
                };
            }

            _logger.LogInformation("Message sent successfully to channel {ChannelId}, Timestamp: {Ts}", CHANNEL_ID, slackResponse.Ts);

            return new SlackMessageResultDto
            {
                Success = true,
                Message = "Message sent successfully",
                ChannelId = CHANNEL_ID,
                Timestamp = slackResponse.Ts
            };
        }
        catch (Exception ex) when (!(ex is SlackIntegrationException))
        {
            _logger.LogError(ex, "Failed to send message to Slack");
            throw new SlackIntegrationException("Failed to send message", "MESSAGE_SEND_ERROR", ex);
        }
    }

    #region Private Helper Methods

    private string GetSlackClientId()
    {
        var clientId = _config["Slack:ClientId"];
        if (string.IsNullOrEmpty(clientId))
        {
            throw new SlackIntegrationException("Slack ClientId not configured", "SLACK_CLIENT_ID_MISSING");
        }
        return clientId;
    }

    private string GetSlackRedirectUrl()
    {
        var redirectUrl = _config["Slack:RedirectUrl"];
        if (string.IsNullOrEmpty(redirectUrl))
        {
            throw new SlackIntegrationException("Slack RedirectUrl not configured", "SLACK_REDIRECT_URL_MISSING");
        }
        return redirectUrl;
    }

    private void ValidateAuthorizationCode(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            throw new SlackIntegrationException("Authorization code cannot be null or empty", "OAUTH_CODE_EMPTY");
        }
    }

    private Dictionary<string, string> BuildTokenExchangeRequest(string code)
    {
        return new Dictionary<string, string>
        {
            { "client_id", GetSlackClientId() },
            { "client_secret", _config["Slack:ClientSecret"]! },
            { "code", code },
            { "redirect_uri", GetSlackRedirectUrl() }
        };
    }

    private void ValidateTokenResponse(SlackTokenResponse? tokenResponse)
    {
        if (tokenResponse == null || !tokenResponse.Ok)
        {
            throw new SlackIntegrationException("OAuth token exchange failed", "OAUTH_EXCHANGE_FAILED");
        }

        if (string.IsNullOrEmpty(tokenResponse.AccessToken))
        {
            throw new SlackIntegrationException("No access token received", "OAUTH_NO_TOKEN");
        }

        if (!tokenResponse.AccessToken.StartsWith("xoxb-"))
        {
            _logger.LogError("Received non-bot token: {Token}", tokenResponse.AccessToken.Substring(0, Math.Min(10, tokenResponse.AccessToken.Length)));
            throw new SlackIntegrationException("Bot token required, received user token", "OAUTH_WRONG_TOKEN_TYPE");
        }
    }

    private async Task<SlackWorkspace> SaveWorkspaceToDatabase(SlackTokenResponse tokenResponse, string encryptedToken)
    {
        var workspace = new SlackWorkspace
        {
            TeamName = tokenResponse.Team?.Name ?? "Unknown",
            EncryptedToken = encryptedToken
        };

        _db.Workspaces.Add(workspace);
        await _db.SaveChangesAsync();

        return workspace;
    }

    private void ValidateMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            throw new SlackIntegrationException("Message cannot be null or empty", "MESSAGE_EMPTY");
        }
    }

    private async Task<SlackWorkspace> GetConnectedWorkspace()
    {
        var workspace = await _db.Workspaces.FirstOrDefaultAsync();
        
        if (workspace == null)
        {
            throw new SlackIntegrationException("No workspace connected", "NO_WORKSPACE");
        }

        if (string.IsNullOrEmpty(workspace.EncryptedToken))
        {
            throw new SlackIntegrationException("No token found for workspace", "NO_TOKEN");
        }

        return workspace;
    }

    private void ValidateBotToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            throw new SlackIntegrationException("Failed to decrypt token", "TOKEN_DECRYPT_FAILED");
        }

        if (!token.StartsWith("xoxb-"))
        {
            _logger.LogError("Non-bot token detected: {Token}", token.Substring(0, Math.Min(10, token.Length)));
            throw new SlackIntegrationException("Bot token required for messaging", "TOKEN_NOT_BOT");
        }
    }

    private HttpRequestMessage BuildMessageRequest(string token, string message)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, MESSAGE_SEND_URL);
        request.Headers.Add("Authorization", $"Bearer {token}");
        
        var payload = new Dictionary<string, string>
        {
            { "channel", CHANNEL_ID },
            { "text", message }
        };
        
        request.Content = new FormUrlEncodedContent(payload);
        
        return request;
    }

    #endregion
}