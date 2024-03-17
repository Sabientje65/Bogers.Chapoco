using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Bogers.Chapoco.Api.Pococha;

public class PocochaHeaderStore
{
    // also of interest: x-pokota-device-session-id
    
    private const string TokenHeader = "x-pokota-token";
    private static Regex PocochaApiUrlExp = new Regex("^https?://api.pococha.com");
    
    private readonly object _mutex = new object();
    private readonly ILogger _logger;
    
    /// <summary>
    /// Time of last header update
    /// </summary>
    public DateTime LastUpdate { get; private set; }

    /// <summary>
    /// Active pococha token
    /// </summary>
    public string? CurrentToken => _headers.TryGetValue(TokenHeader, out var token) ? token : null;
    
    /// <summary>
    /// Indicates whether the current headers are usable or not, invalidation should happen after pococha sends an unauthenticated response
    /// </summary>
    public bool IsValid => !String.IsNullOrEmpty(CurrentToken);
    
    private IDictionary<string, string> _headers = new Dictionary<string, string>();

    public PocochaHeaderStore(ILogger<PocochaHeaderStore> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Invalidate the current set of headers by disposing them
    /// </summary>
    public void Invalidate()
    {
        var now = DateTime.UtcNow;

        // skip invalidation when headers have been updated post-invocation
        lock (_mutex)
        {
            if (now > LastUpdate)
            {
                _logger.LogDebug("Invalidating pococha headers");
                _headers = new Dictionary<string, string>();   
            }
            
            LastUpdate = DateTime.UtcNow;
            
            // should we fire an event on invalidation? _headerStore.OnInvalidated += (...)
            // makes it easier to detect invalidation the moment it happens
        }
    }
    
    /// <summary>
    /// Update the current headers with the most recent valid pococha request in the given <see cref="har"/>
    ///
    /// For more information, see: <see cref="http://www.softwareishard.com/blog/har-12-spec"/>
    /// </summary>
    /// <param name="har">json shaped as har</param>
    /// <returns>True when an update took place</returns>
    public bool UpdateFromHar(JsonNode har)
    {
        // for reference, see: http://www.softwareishard.com/blog/har-12-spec
        var requests = har["log"]["entries"]
            .AsArray()
            .Select(entry => entry["request"]);
        
        var parsableEntry = requests
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
            _logger.LogDebug("Updating pococha headers");
            LastUpdate = DateTime.UtcNow;
            _headers = headersDict;
        }
        
        return true;
    }

    /// <summary>
    /// Write the current set of headers to the given request
    /// </summary>
    /// <param name="request">Request to write headers to</param>
    public void WriteTo(HttpRequestMessage request)
    {
        IDictionary<string, string> headers;
        lock (_mutex) headers = _headers;
        
        foreach (var (name, value) in headers)
        {
            // skip content-type etc. those are determined by our consumer
            if (name.Equals("content-type", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Equals("content-length", StringComparison.OrdinalIgnoreCase)) continue;
            
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