namespace Bogers.Chapoco.Api;

public class PocochaClient
{
    private readonly HttpClient _client;

    public PocochaClient()
    {
        _client = new HttpClient { BaseAddress = new Uri("https://api.pococha.com") };
    }



    // public Task Get()
    // {
    //     _client.GetAsync(new )
    // }
    
}

public class PocochaHeaders
{
    
    
}