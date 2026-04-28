using System.Text.Json.Serialization;

public class SlackMessageResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("channel")]
    public string? Channel { get; set; }

    [JsonPropertyName("ts")]
    public string? Ts { get; set; }

    [JsonPropertyName("message")]
    public SlackMessage? Message { get; set; }
}

public class SlackMessage
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("user")]
    public string? User { get; set; }

    [JsonPropertyName("ts")]
    public string? Ts { get; set; }
}
