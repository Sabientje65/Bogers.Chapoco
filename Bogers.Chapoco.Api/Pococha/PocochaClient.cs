﻿using System.IO.Compression;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bogers.Chapoco.Api.Pococha;

/// <summary>
/// Service for interacting with Pococha
/// </summary>
public class PocochaClient : IDisposable
{
    private readonly ILogger _logger;
    
    private readonly HttpClient _client;
    private readonly PocochaHeaderStore _headerStore;

    private static readonly JsonSerializerOptions PocochaJsonSerializerOptions = new JsonSerializerOptions()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public PocochaClient(
        ILogger<PocochaClient> logger,
        IHttpClientFactory httpClientFactory, 
        PocochaHeaderStore headerStore
    )
    {
        _logger = logger;
        
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
            using var msg = BuildRequestMessage(HttpMethod.Get, "/v1/my_profile");
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
    public async Task<LivesResource> GetFollowingCurrentlyLive(CancellationToken token = default)
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
        
        return await ReadJsonContent<LivesResource>(res);
    }
    
    /// <summary>
    /// Retrieve a list of currently live accounts
    /// </summary>
    /// <param name="token">Cancellation token</param>
    /// <returns>List of liveresources</returns>
    /// <exception cref="TokenExpiredException">Thrown when pococha token is expired</exception>
    public async Task<LivesResource> GetCurrentlyLive(CancellationToken token = default)
    {
        ThrowIfTokenInvalid();
        using var msg = BuildRequestMessage(HttpMethod.Get, "/v6/lives/hots?page=0");
        using var res = await Send(msg, token);
        return await ReadJsonContent<LivesResource>(res);
    }

    /// <summary>
    /// Retrieve a single live resource, contains stream url etc.
    /// </summary>
    /// <param name="liveId">Id of the live</param>
    /// <param name="token">Cancellation token</param>
    /// <returns>Single live resource</returns>
    /// <exception cref="TokenExpiredException">Thrown when pococha token is expired</exception>
    public async Task<LiveResource> GetLive(
        int liveId,
        CancellationToken token = default
    )
    {
        ThrowIfTokenInvalid();
        using var msg = BuildRequestMessage(HttpMethod.Get, $"/v1/lives/{liveId}");
        using var res = await Send(msg, token);
        return await ReadJsonContent<LiveResource>(res);
    }

    /// <summary>
    /// Invoke the given endpoint directly, basically acting as a proxy for pococha calls
    /// </summary>
    /// <param name="method">HTTP method</param>
    /// <param name="uri">URI, expected to include querystring parameters</param>
    /// <param name="body">Optional request body</param>
    /// <returns>Raw pococha response</returns>
    public async Task<JsonNode> Proxy(
        string method,
        string uri,
        object? body = null
    )
    {
        using var msg = BuildRequestMessage(
            HttpMethod.Parse(method),
            uri,
            body
        );

        using var res = await Send(msg);
        return await ReadJsonContent<JsonNode>(res);
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
            var json = JsonSerializer.SerializeToNode(body, PocochaJsonSerializerOptions)!.AsObject();
            var content = new Dictionary<string, string>();

            foreach (var (key, value) in json) content[key] = value!.ToString();
            
            // pococha uses formurlencodedcontent for at least comments
            msg.Content = new FormUrlEncodedContent(content);
            msg.Headers.TryAddWithoutValidation("Content-Type", "application/x-www-form-urlencoded");
        }

        return msg;
    }

    private async Task<T> ReadJsonContent<T>(HttpResponseMessage res)
    {
        var responseStream = await AsJsonDeserializableStream();
        
        return await JsonSerializer.DeserializeAsync<T>(
            responseStream,
            PocochaJsonSerializerOptions    
        );

        async Task<Stream> AsJsonDeserializableStream()
        {
            var contentStream = await res.Content.ReadAsStreamAsync();
            if (res.Content.Headers.ContentEncoding.Contains("gzip"))
            {
                return new GZipStream(contentStream, CompressionMode.Decompress);
            }
            return contentStream;
        }
    }


    private async Task<HttpResponseMessage> Send(
        HttpRequestMessage request,
        CancellationToken cancellationToken = default
    )
    {
        // log requests made + statuscode responses, level = info
        // include header level = debug
        
        var response = await _client.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        
        _logger.LogInformation("Received statuscode {StatusCode} from request to {Url}", response.StatusCode, request.RequestUri);
        
        // not authenticated anymore -> error
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            _headerStore.Invalidate();
            ThrowIfTokenInvalid();
        }
        
        // for other errors, log and bubble
        if (!response.IsSuccessStatusCode)
        {
            string content = String.Empty;
            try
            {
                content = (await ReadJsonContent<JsonNode>(response)).ToString();
            } catch { /* Ignore, no json body */}
            
            response.Dispose();
            
            _logger.LogError(
                "Request to {Url} failed with statuscode {StatusCode} and message {Message}, see body for more information",
                request.RequestUri,
                response.StatusCode,
                content
            );

            response.EnsureSuccessStatusCode();
        }

        return response;
    }

    public void Dispose()
    {
        _client.Dispose();
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
    
    public Profile Profile { get; set; }

    public User User { get; set; }
}

public class Live
{
    public int Id { get; set; }
    
    // [JsonPropertyName("thumbnail_image_url")]
    public string ThumbnailImageUrl { get; set; }
    
    public string Title { get; set; }
    
    public string Url { get; set; }
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