using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Bogers.Chapoco.Api.Pococha;

public class PocochaHeaderStore
{
    // also of interest: x-pokota-device-session-id
    
    private const string TokenHeader = "x-pokota-token";
    private static Regex PocochaApiUrlExp = new Regex("^https?://api.pococha.com");
    
    private object _mutex = new object();
    public DateTime LastUpdate { get; private set; }

    public string? CurrentToken => _headers.TryGetValue(TokenHeader, out var token) ? token : null; 
    public bool IsValid => !String.IsNullOrEmpty(CurrentToken);
    
    private IDictionary<string, string> _headers = new Dictionary<string, string>();
    
    public void Invalidate()
    {
        var now = DateTime.UtcNow;

        // skip invalidation when headers have been updated post-invocation
        lock (_mutex)
        {
            if (now > LastUpdate)
            {
                _headers = new Dictionary<string, string>();   
            }
            
            // should we fire an event on invalidation? _headerStore.OnInvalidated += (...)
            // makes it easier to detect invalidation the moment it happens
        }
    }
    
    public bool UpdateFromHar(JsonNode har)
    {
        // for reference, see: http://www.softwareishard.com/blog/har-12-spec
        var entries = har["log"]["entries"].AsArray();
        var parsableEntry = entries
            .LastOrDefault(CanUseForUpdate);
        
        // no valid entries found, no pococha requests made?
        if ( parsableEntry == null ) return false;

        var headers = parsableEntry["headers"].AsArray();
        var headersDict = new Dictionary<string, string>();

        foreach (var header in headers)
        {
            var name = header["name"].GetValue<string>();
            var value = header["value"].GetValue<string>();

            headersDict[name] = value;
        }

        // override at once to prevent off-chance of update taking place during read
        lock (_mutex)
        {
            LastUpdate = DateTime.UtcNow;
            _headers = headersDict;
        }
        
        return true;
    }

    public void ApplyTo(HttpRequestMessage request)
    {
        IDictionary<string, string> headers;
        lock (_mutex) headers = _headers;
        
        foreach (var (name, value) in headers)
        {
            // skip content-type etc. those are determined by our consumer
            if (name.Equals("content-type", StringComparison.OrdinalIgnoreCase)) continue;
            
            request.Headers.TryAddWithoutValidation(name, value);
        }
    }

    private bool CanUseForUpdate(JsonNode request)
    {
        var url = request["url"].GetValue<string>();
        var headers = request["headers"].AsArray();
        var tokenHeader = headers
            .FirstOrDefault(header => header["name"].GetValue<string>() == TokenHeader);

        // should have a token header
        if (tokenHeader == null) return false;
        
        // token header value should be new
        if (tokenHeader["value"].Equals(CurrentToken)) return false;

        return PocochaApiUrlExp.IsMatch(url);
    }
}