using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Bogers.Chapoco.Api;

public static class DebugHelper
{
    private static Regex PocochaApiUrlExp = new Regex("^https?://api.pococha.com");
    
    
    /// <summary>
    /// Find all requests to pococha's api
    /// </summary>
    /// <param name="har"></param>
    /// <returns></returns>
    public static JsonNode[] FindPocochaRequests(JsonNode har)
    {
        return har["log"]["entries"].AsArray()
            .Select(entry => entry["request"])
            .Where(request => PocochaApiUrlExp.IsMatch(request["url"].GetValue<string>()))
            .ToArray();
    }

    /// <summary>
    /// Map the headers of the given har node to a format understood by Postman
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public static string HarHeadersToPostman(JsonNode request)
    {
        var headers = request["headers"].AsArray();
        var sb = new StringBuilder();
        foreach (var header in headers) sb.AppendLine($"{header["name"]}: {header["value"]}");
        return sb.ToString();
    }
    
}