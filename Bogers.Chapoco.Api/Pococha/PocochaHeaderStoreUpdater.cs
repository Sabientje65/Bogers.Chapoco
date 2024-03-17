using System.Diagnostics;
using Bogers.Chapoco.Api.Pushover;
using Microsoft.Extensions.Options;

namespace Bogers.Chapoco.Api.Pococha;

/// <summary>
/// Store tracking current set of pococha headers, headers are automatically appended when sending requests to pococha
/// </summary>
public class PocochaHeaderStoreUpdater : TimedBackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public PocochaHeaderStoreUpdater(
        ILogger<PocochaHeaderStoreUpdater> logger,
        IServiceProvider serviceProvider
    ) : base(logger)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override TimeSpan Interval { get; } = TimeSpan.FromSeconds(10);
    protected override async Task Run(CancellationToken stoppingToken)
    {
        // should migrate to filesystemwatcher
        
        using var serviceScope = _serviceProvider.CreateScope();
        
        var pocochaConfiguration = serviceScope.ServiceProvider.GetRequiredService<IOptions<PocochaConfiguration>>().Value;
        var pocochaHeaderStore = serviceScope.ServiceProvider.GetRequiredService<PocochaHeaderStore>();
        var pococha = serviceScope.ServiceProvider.GetRequiredService<PocochaClient>();
        var pushover = serviceScope.ServiceProvider.GetRequiredService<PushoverClient>();
        
        try
        {
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

            var didUpdate = false;
            var wasValid = pocochaHeaderStore.IsValid;

            foreach (var flowFile in files)
            {
                // skip files greater than ~50-100mb -> likely garbage for our purposes

                try
                {
                    var har = await flowParser
                        .ParseToHar(flowFile); // <-- add ability to filter flows (only requests to pococha?)
                    didUpdate = pocochaHeaderStore.UpdateFromHar(har) || didUpdate;
                    
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
            
            // log updated status

            if (didUpdate && !wasValid)
            {
                var isValid = await pococha.IsAuthenticated();
                var authenticatedLabel = isValid ?
                    "Authenticated" :
                    "Unauthenticated";
                
                _logger.LogInformation("Successfully updated pococha headers, status: {Label}", authenticatedLabel);
                
                // only send for 'became authenticated', should merge with auth state monitor, one place to track + update
                if (isValid)
                {
                    await pushover.SendMessage(
                        PushoverMessage.Text("Pococha token updated", $"Current authentication status: {authenticatedLabel}")    
                    );   
                }
            }
        }
        catch (Exception e)
        {
            // log
        }
    }
}