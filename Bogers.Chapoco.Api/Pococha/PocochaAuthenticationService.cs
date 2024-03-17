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
            await pushover.SendMessage(PushoverMessage.Text("Pococha token invalidated", "Currently unauthenticated. Please open up the pococha app for a token refresh"));
        }
        else
        {
            _logger.LogInformation("Pococha token succesfully updated");
            await pushover.SendMessage(PushoverMessage.Text("Pococha token updated", "Currently authenticated"));
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
        var flowParser = new MitmFlowParser();
        var files = Directory.EnumerateFiles(pocochaConfiguration.FlowsDirectory)
            .OrderBy(f => f);

        foreach (var flowFile in files)
        {
            // skip files greater than ~50-100mb -> likely garbage for our purposes

            try
            {
                var har = await flowParser
                    .ParseToHar(flowFile); // <-- add ability to filter flows (only requests to pococha?)
                pocochaHeaderStore.UpdateFromHar(har);
                
                _logger.LogInformation("Cleaning flow file: {FlowFile}", flowFile);
                
                // delete processed
                if (!DebugHelper.IsDebugMode) File.Delete(flowFile); // <-- dry mode?
                else { /* log */ }
                

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
}