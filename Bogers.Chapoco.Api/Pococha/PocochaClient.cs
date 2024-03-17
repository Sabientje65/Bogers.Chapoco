using System.Net;
using System.Text.Json;

namespace Bogers.Chapoco.Api.Pococha;

public class PocochaClient
{
    private readonly HttpClient _client;
    private readonly PocochaHeaderStore _headerStore;

    private static readonly JsonSerializerOptions PocochaJsonSerializerOptions = new JsonSerializerOptions()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public PocochaClient(
        IHttpClientFactory httpClientFactory,
        PocochaHeaderStore headerStore
    )
    {
        _headerStore = headerStore;
        // _client = new HttpClient { BaseAddress = new Uri("https://api.pococha.com") };
        _client = httpClientFactory.CreateClient("pococha");
        _client.BaseAddress = new Uri("https://api.pococha.com");
    }

    /// <summary>
    /// Validate whether the current service can make requests authenticated requests to the pococha api
    /// </summary>
    /// <returns>True when authenticated</returns>
    public async Task<bool> IsAuthenticated()
    {
        // simply request /me or w/e, check for token being expired
        //v1/app_launch <-- check for 401
        try
        {
            ThrowIfTokenInvalid();
            using var msg = BuildRequestMessage(HttpMethod.Get, "/v1/app_launch");
            (await Send(msg)).Dispose();
            return true;
        }
        catch (TokenExpiredException)
        {
            return false;
        }
    }

    /// <summary>
    /// Retrieve of a list of followed accounts currently live
    /// </summary>
    /// <param name="token">Cancellation token</param>
    /// <returns>List of liveresources</returns>
    /// <exception cref="TokenExpiredException">Thrown when pococha token is expired</exception>
    public async Task<LivesResource> GetCurrentlyLive(CancellationToken token = default)
    {
        ThrowIfTokenInvalid();
        using var msg = BuildRequestMessage(
            HttpMethod.Get, 
            "/v5/lives/followings?on_air=true&page=0"
        );

        using var res = await Send(msg, token);
        
        //query:
        //  on_air: boolean
        //  page: number
        ///v5/lives/followings
        
        // await 
        return await ReadJsonContent<LivesResource>(res);
    }

    private void ThrowIfTokenInvalid()
    {
        if (!_headerStore.IsValid) throw new TokenExpiredException();
    }
    
    private HttpRequestMessage BuildRequestMessage(
        HttpMethod method, 
        string uri,
        object? body = null
    )
    {
        var msg = new HttpRequestMessage(method, uri);
        _headerStore.WriteTo(msg);

        if (body != null)
        {
            msg.Content = new StringContent(JsonSerializer.Serialize(body));
            msg.Headers.TryAddWithoutValidation("Content-Type", "application/json");
        }

        return msg;
    }

    private async Task<T> ReadJsonContent<T>(HttpResponseMessage res)
    {
        // todo: use snake_case json convert
        
        return await JsonSerializer.DeserializeAsync<T>(
            await res.Content.ReadAsStreamAsync(),
            PocochaJsonSerializerOptions       
        );
    }


    private async Task<HttpResponseMessage> Send(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default
    )
    {
        // log requests made + statuscode responses, level = info
        // include header level = debug
        
        var response = await _client.SendAsync(request, cancellationToken);
        
        // not authenticated anymore -> error
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _headerStore.Invalidate();
            ThrowIfTokenInvalid();
        }

        return response;

        // should handle invalidation here -> 401
    }
}

public class TokenExpiredException : InvalidOperationException
{
    public TokenExpiredException() : base("The provided token was expired, please update the active token!")
    {
    }
}

public class LivesResource
{
    // [JsonPropertyName("live_resources")]
    public LiveResource[] LiveResources { get; set; }
}

public class LiveResource
{
    // [JsonPropertyName("live_edge")]
    public LiveEdge LiveEdge { get; set; }

    public Live Live { get; set; }
}

public class Live
{
    // [JsonPropertyName("thumbnail_image_url")]
    public string ThumbnailImageUrl { get; set; }
    
    public string Title { get; set; }
    
    public string Url { get; set; }

    public Profile Profile { get; set; }

    public User User { get; set; }
}

public class LiveEdge
{
    public Ivs Ivs { get; set; }
}

public class Ivs
{
    // [JsonPropertyName("playback_url")]
    public string PlaybackUrl { get; set; }
}

public class Profile
{
    public string Bio { get; set; }

    public int Id { get; set; }
}

public class User
{
    public int Id { get; set; }

    public string Name { get; set; }
    
    public string ProfileImageUrl { get; set; }

    public string ThumbnailImageUrl { get; set; }
}