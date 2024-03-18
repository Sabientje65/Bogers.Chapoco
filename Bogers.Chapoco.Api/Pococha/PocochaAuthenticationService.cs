using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bogers.Chapoco.Api.Pushover;
using Microsoft.Extensions.Options;

namespace Bogers.Chapoco.Api.Pococha;

/// <summary>
/// Service for managing pococha authentication state
///
/// Authentication is handled via a set of headers, this service periodically updates them and notifies upon becoming authenticated/unauthenticated
/// </summary>
public class PocochaAuthenticationService : TimedBackgroundService
{
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private bool _wasAuthenticated;
    private bool _isStartup = true;

    public PocochaAuthenticationService(ILogger<PocochaAuthenticationService> logger, IServiceProvider serviceProvider) : base(logger)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override TimeSpan Interval { get; } = TimeSpan.FromMinutes(1);

    protected override async Task Run(CancellationToken stoppingToken)
    {
        // alternative: define services as properties -> mark as injected via attribute, have base class manage injection
        // too much magic for now though
        using var serviceScope = _serviceProvider.CreateScope();
        
        var pococha = serviceScope.ServiceProvider.GetRequiredService<PocochaClient>();
        var pushover = serviceScope.ServiceProvider.GetRequiredService<PushoverClient>();
        var pocochaConfiguration = serviceScope.ServiceProvider.GetRequiredService<IOptions<PocochaConfiguration>>().Value;
        var pocochaHeaderStore = serviceScope.ServiceProvider.GetRequiredService<PocochaHeaderStore>();
        
        await UpdateHeaderStore(pocochaConfiguration, pocochaHeaderStore);
        await NotifyAuthenticationStateChanges(pococha, pushover);
        
        _isStartup = false;
    }

    private async Task NotifyAuthenticationStateChanges(
        PocochaClient pococha,
        PushoverClient pushover
    )
    {
        _logger.LogInformation("Checking pococha authentication state");
        
        var isAuthenticated = await pococha.IsAuthenticated();
        
        // nothing changed
        // upon starting up we should always alert
        if (
            !_isStartup &&
            isAuthenticated == _wasAuthenticated
        )
        {
            return;
        }

        if (!isAuthenticated)
        {
            _logger.LogInformation("Pococha token became invalid");
            await pushover.SendMessage(PushoverMessage.Text("Pococha became unauthenticated"));
        }
        else
        {
            _logger.LogInformation("Pococha token succesfully updated");
            await pushover.SendMessage(PushoverMessage.Text("Pococha became authenticated"));
        }

        _wasAuthenticated = isAuthenticated;
    }

    private async Task UpdateHeaderStore(
        PocochaConfiguration pocochaConfiguration,
        PocochaHeaderStore pocochaHeaderStore
    )
    {
        // should migrate to filesystemwatcher
        
        _logger.LogDebug("Attempting to update pococha headers");
            
        // directory may need to still be created
        if (!Directory.Exists(pocochaConfiguration.FlowsDirectory))
        {
            _logger.LogWarning("Failed to update pococha headers, no flows directory found at {FlowsDirectory}", pocochaConfiguration.FlowsDirectory);
            return;
        }
        
        // filenames are assumed to be sortable by date
        var files = Directory.EnumerateFiles(pocochaConfiguration.FlowsDirectory)
            .OrderBy(f => f)
            .ToList();

        // during our first run, try to take the last processed file from our archive
        // this may still contain a valid token
        if (
            _isStartup &&
            Directory.Exists(pocochaConfiguration.HarArchiveDirectory)
        )
        {
            var lastProcessedHar = Directory.EnumerateFiles(pocochaConfiguration.HarArchiveDirectory)
                .MaxBy(f => f);
            
            if (!String.IsNullOrEmpty(lastProcessedHar)) files.Add(lastProcessedHar);
        }

        _logger.LogInformation("Found {FlowFileCount} flow files", files.Count);

        foreach (var flowFile in files)
        {
            // skip files greater than ~50-100mb -> likely garbage for our purposes

            try
            {
                _logger.LogInformation("Attempting to update pococha headers from flow file: {FlowFile}", flowFile);

                var har = await ReadHar(flowFile);
                var didUpdate = pocochaHeaderStore.UpdateFromHar(har);
                
                _logger.LogInformation("Cleaning flow file: {FlowFile}", flowFile);
                
                // delete processed
                try
                {
                    File.Delete(flowFile); // <-- dry mode?
                    
                    // ensure directory exists
                    Directory.CreateDirectory(pocochaConfiguration.HarArchiveDirectory);
                
                    var harLocation = Path.Combine(pocochaConfiguration.HarArchiveDirectory, $"{Path.GetFileNameWithoutExtension(flowFile)}.har.gz");
                    _logger.LogInformation("Archiving har at: {HarLocation}", harLocation);
                
                    // todo: automatically clean old files
                    if (didUpdate)
                    {
                        await using var gzip = new GZipStream(File.Create(harLocation), CompressionLevel.SmallestSize, false);
                        await JsonSerializer.SerializeAsync(gzip, har);
                        await gzip.FlushAsync();
                    }
                }
                catch(Exception)
                {
                    // likely still in use
                    _logger.LogInformation("Failed to clean flow file: {FlowFile}, likely still in use", flowFile);
                }

                // send pushover notification on successful update?
            }
            catch (ApplicationException e) {
                _logger.LogWarning(e, "Failed to parse flowfile to har: {FlowFile}", flowFile);
                
                // should likely still attempt to delete, lets just see how this plays out in production
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Failed to process flow file with unknown error: {FlowFile}", flowFile);
            }
        }
    }

    private async Task<JsonNode> ReadHar(string file)
    {
        return Path.GetExtension(file) switch
        {
            ".gz" => await FromGzip(),
            ".har" => await FromHar(),
            _ => await FromFlow()
        };

        async Task<JsonNode> FromGzip()
        {
            await using var stream = new GZipStream(File.OpenRead(file), CompressionMode.Decompress, false);
            return await JsonSerializer.DeserializeAsync<JsonNode>(stream);
        }

        async Task<JsonNode> FromHar()
        {
            await using var stream = File.OpenRead(file);
            return await JsonSerializer.DeserializeAsync<JsonNode>(stream);
        }

        async Task<JsonNode> FromFlow()
        {
            var flowParser = new MitmFlowParser();
            return await flowParser.ParseToHar(file);
        }
    }
}
