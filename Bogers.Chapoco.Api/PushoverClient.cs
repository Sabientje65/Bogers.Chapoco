using System.Text.Json.Serialization;

namespace Bogers.Chapoco.Api;

public class PushoverClient
{
    private readonly HttpClient _client;
    
    public PushoverClient(PushoverConfiguration configuration)
    {
        _client = new HttpClient { BaseAddress = new Uri("https://api.pushover.net/") };
    }

    public async Task SendMessage(PushoverMessage message)
    {
        // todo: logging + error handling
        await _client.PostAsJsonAsync("/1/messages.json", message);
    }
}

public class PushoverMessage
{
    [JsonPropertyName("url_title")]
    public string UrlTitle { get; set; }
    
    [JsonPropertyName("url")]
    public string Url { get; set; }
    
    [JsonPropertyName("title")]
    public string Title { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; }
    
    public PushoverMessage(string message)
    {
        Message = message;
    }
}


public class PushoverConfiguration
{
    public string AppToken { get; set; }
    
    public string UserToken { get; set; }
}