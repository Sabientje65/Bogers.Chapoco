using System.Text.Json;

namespace Bogers.Chapoco.Api.Pushover;

public class PushoverClient
{
    private readonly HttpClient _client;
    private readonly PushoverConfiguration _pushoverConfiguration;
    
    private static readonly JsonSerializerOptions PushoverJsonSerializerOptions = new JsonSerializerOptions()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
    
    public PushoverClient(
        IHttpClientFactory httpClientFactory,
        PushoverConfiguration configuration
    )
    {
        _client = httpClientFactory.CreateClient("pushover");
        _pushoverConfiguration = configuration;
        
        _client.BaseAddress = new Uri("https://api.pushover.net/");
    }

    public async Task SendMessage(PushoverMessage message)
    {
        var payload = JsonSerializer.SerializeToNode(message);
        payload["token"] = _pushoverConfiguration.AppToken;
        payload["user"] = _pushoverConfiguration.UserToken;
        
        // todo: logging + error handling
        try
        {
            await _client.PostAsJsonAsync("/1/messages.json", payload, PushoverJsonSerializerOptions);
        }
        catch (Exception e)
        {
            // log
        }
    }
}