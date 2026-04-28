using System.Text.Json.Serialization;

public class SlackTokenResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("team")]
    public SlackTeam? Team { get; set; }
}

public class SlackTeam
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}