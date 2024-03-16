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


        try
        {
            foreach (var (name, value) in headerStore.Read())
            {
                _client.DefaultRequestHeaders.TryAddWithoutValidation(name, value);
            }
        }
        catch(Exception e)
        {
            // log
        }

        // _client.DefaultRequestHeaders
    }

    /// <summary>
    /// Validate whether the current service can make requests authenticated requests to the pococha api
    /// </summary>
    /// <returns>True when authenticated</returns>
    public async Task<bool> IsValid()
    {
        // simply request /me or w/e, check for token being expired
        //v1/app_launch <-- check for 401
        return false;
    }

    public async Task GetCurrentlyLive()
    {
        //query:
        //  on_air: boolean
        //  page: number
        ///v5/lives/followings
        
        
    }


    // public Task Get()
    // {
    //     _client.GetAsync(new )
    // }
    
}

public class LivesResource
{
    
}

public class LiveResource
{
    
}