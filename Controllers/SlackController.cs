using Microsoft.AspNetCore.Mvc;
using SlackIntegration.Interfaces;
using SlackIntegration.DTOs;
using SlackIntegration.Exceptions;

namespace SlackIntegration.Controllers;

[ApiController]
[Route("api/slack")]
[Produces("application/json")]
public class SlackController : ControllerBase
{
    private readonly ISlackService _slackService;
    private readonly ILogger<SlackController> _logger;

    public SlackController(ISlackService slackService, ILogger<SlackController> logger)
    {
        _slackService = slackService;
        _logger = logger;
    }

   
    [HttpGet("install")]
    [ProducesResponseType(typeof(SlackInstallResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public IActionResult GetInstallUrl()
    {
        try
        {
            var installUrl = _slackService.GetInstallUrl();
            
            var response = new SlackInstallResponseDto
            {
                Message = "Click the URL below to authorize with Slack",
                InstallUrl = installUrl,
                RedirectInstructions = "Copy and paste this URL in your browser to complete OAuth"
            };

            _logger.LogInformation("Generated Slack install URL");
            return Ok(response);
        }
        catch (SlackIntegrationException ex)
        {
            _logger.LogError(ex, "Failed to generate Slack install URL: {ErrorCode}", ex.ErrorCode);
            return CreateProblemDetails(ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating Slack install URL");
            return CreateInternalServerErrorProblemDetails(ex);
        }
    }

    [HttpGet("oauth/callback")]
    [ProducesResponseType(typeof(SlackWorkspaceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> HandleOAuthCallback([FromQuery] string code)
    {
        try
        {
            if (string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("OAuth callback received without authorization code");
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid OAuth Request",
                    Detail = "Authorization code is required",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var workspace = await _slackService.SaveTokenAsync(code);
            
            _logger.LogInformation("Successfully completed OAuth for workspace: {WorkspaceName}", workspace.TeamName);
            return Ok(workspace);
        }
        catch (SlackIntegrationException ex)
        {
            _logger.LogError(ex, "OAuth callback failed: {ErrorCode}", ex.ErrorCode);
            return CreateProblemDetails(ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in OAuth callback");
            return CreateInternalServerErrorProblemDetails(ex);
        }
    }

    [HttpPost("send")]
    [ProducesResponseType(typeof(SlackMessageResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SendMessage([FromBody] SlackMessageRequestDto request)
    {
        try
        {
            if (request == null || string.IsNullOrEmpty(request.Message))
            {
                _logger.LogWarning("Send message request received without message content");
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Message Request",
                    Detail = "Message content is required",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var result = await _slackService.SendMessageAsync(request.Message);
            
            if (result.Success)
            {
                _logger.LogInformation("Message sent successfully to Slack");
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("Message sending failed: {Error}", result.Error);
                return BadRequest(new ProblemDetails
                {
                    Title = "Message Sending Failed",
                    Detail = result.Error,
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }
        catch (SlackIntegrationException ex)
        {
            _logger.LogError(ex, "Message sending failed: {ErrorCode}", ex.ErrorCode);
            return CreateProblemDetails(ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending message");
            return CreateInternalServerErrorProblemDetails(ex);
        }
    }

    #region Private Helper Methods

    private ObjectResult CreateProblemDetails(SlackIntegrationException ex)
    {
        var statusCode = ex.ErrorCode switch
        {
            "OAUTH_CODE_EMPTY" or "MESSAGE_EMPTY" => StatusCodes.Status400BadRequest,
            "NO_WORKSPACE" or "NO_TOKEN" => StatusCodes.Status400BadRequest,
            "SLACK_CLIENT_ID_MISSING" or "SLACK_REDIRECT_URL_MISSING" => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status500InternalServerError
        };

        return Problem(
            title: "Slack Integration Error",
            detail: ex.Message,
            statusCode: statusCode,
            extensions: new Dictionary<string, object>
            {
                { "errorCode", ex.ErrorCode },
                { "errorDetails", ex.ErrorDetails }
            }
        );
    }

    private ObjectResult CreateInternalServerErrorProblemDetails(Exception ex)
    {
        return Problem(
            title: "Internal Server Error",
            detail: "An unexpected error occurred while processing your request",
            statusCode: StatusCodes.Status500InternalServerError,
            extensions: new Dictionary<string, object>
            {
                { "correlationId", HttpContext.TraceIdentifier }
            }
        );
    }

    #endregion
}