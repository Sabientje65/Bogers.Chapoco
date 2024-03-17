using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bogers.Chapoco.Api;

public class PocochaClient
{
    private readonly HttpClient _client;
    private readonly PocochaHeaderStore _headerStore;

    public PocochaClient(
        IHttpClientFactory httpClientFactory,
        PocochaHeaderStore headerStore
    )
    {
        _headerStore = headerStore;
        // _client = new HttpClient { BaseAddress = new Uri("https://api.pococha.com") };
        _client = httpClientFactory.CreateClient();
        _client.BaseAddress = new Uri("https://api.pococha.com");


        // should we do this on a per-client basis, or on a per-method invocation basis?
        // try
        // {
        //     foreach (var (name, value) in headerStore.Read())
        //     {
        //         _client.DefaultRequestHeaders.TryAddWithoutValidation(name, value);
        //     }
        // }
        // catch(Exception e)
        // {
        //     // log
        // }

        // _client.DefaultRequestHeaders
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

    // private async Task<T> Get<T>(
    //     string uri
    // )
    // {
    //     
    // }

    private void ThrowIfTokenInvalid()
    {
        if (!_headerStore.IsValid) throw new TokenExpiredException();
    }

    // private async Task<T> Post<T>(
    //     string uri,
    //     object content,
    //     CancellationToken token = default
    // )
    // {
    //     using var msg = new HttpRequestMessage(HttpMethod.Post, uri);
    //     msg.Content = new StringContent(JsonSerializer.Serialize(content));
    //     msg.Headers.TryAddWithoutValidation("Content-Type", "application/json");
    //     using var res = await Send(msg, token);
    //     
    //     if (res.StatusCode == HttpStatusCode.Unauthorized) throw new TokenExpiredException();
    //     // should we add the ability to log our response bodies? -> level = debug
    //     
    //     return await JsonSerializer.DeserializeAsync<T>(await msg.Content.ReadAsStreamAsync());
    // }
    
    private HttpRequestMessage BuildRequestMessage(
        HttpMethod method, 
        string uri,
        object? body = null
    )
    {
        var msg = new HttpRequestMessage(method, uri);
        _headerStore.ApplyTo(msg);

        if (body != null)
        {
            msg.Content = new StringContent(JsonSerializer.Serialize(body));
            msg.Headers.TryAddWithoutValidation("Content-Type", "application/json");
        }

        return msg;
    }

    private async Task<T> ReadJsonContent<T>(HttpResponseMessage res)
    {
        return await JsonSerializer.DeserializeAsync<T>(
            await res.Content.ReadAsStreamAsync()
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
    
    // public Task Get()
    // {
    //     _client.GetAsync(new )
    // }
    
    
}

public class TokenExpiredException : InvalidOperationException
{
    public TokenExpiredException() : base("The provided token was expired, please update the active token!")
    {
    }
}

public class LivesResource
{
    [JsonPropertyName("live_resources")]
    public LiveResource[] LiveResources { get; set; }
}

public class LiveResource
{
    [JsonPropertyName("live_edge")]
    public LiveEdge LiveEdge { get; set; }

    public Live Live { get; set; }
}

public class Live
{
    [JsonPropertyName("thumbnail_image_url")]
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
    [JsonPropertyName("playback_url")]
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

    [JsonPropertyName("profile_image_url")]
    public string ProfileImageUrl { get; set; }

    [JsonPropertyName("thumbnail_image_url")]
    public string ThumbnailImageUrl { get; set; }
}