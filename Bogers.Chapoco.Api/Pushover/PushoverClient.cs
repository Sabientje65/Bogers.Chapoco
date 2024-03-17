using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Bogers.Chapoco.Api.Pushover;

public class PushoverClient
{
    private readonly HttpClient _client;
    private readonly PushoverConfiguration _pushoverConfiguration;
    
    private static readonly JsonSerializerOptions PushoverJsonSerializerOptions = new JsonSerializerOptions()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    public PushoverClient(
        IHttpClientFactory httpClientFactory,
        IOptions<PushoverConfiguration> configuration
    )
    {
        _client = httpClientFactory.CreateClient("pushover");
        _pushoverConfiguration = configuration.Value;
        
        _client.BaseAddress = new Uri("https://api.pushover.net");
    }

    public async Task SendMessage(PushoverMessage message)
    {
        // log message? trace invocation?
        if (!_pushoverConfiguration.Enabled) return;
        
        var payload = JsonSerializer.SerializeToNode(message, PushoverJsonSerializerOptions);
        payload["token"] = _pushoverConfiguration.AppToken;
        payload["user"] = _pushoverConfiguration.UserToken;
        
        // todo: logging + error handling
        try
        {
            ThrowIfConfigurationInvalid();
            await _client.PostAsJsonAsync("/1/messages.json", payload);
        }
        catch (Exception e)
        {
            // log
        }
    }

    private void ThrowIfConfigurationInvalid()
    {
        if (
            String.IsNullOrEmpty(_pushoverConfiguration.AppToken) ||
            String.IsNullOrEmpty(_pushoverConfiguration.UserToken)
        )
        {
            throw new Exception("Invalid pushover configuration! Missing either AppToken, or UserToken");
        }
    }
}