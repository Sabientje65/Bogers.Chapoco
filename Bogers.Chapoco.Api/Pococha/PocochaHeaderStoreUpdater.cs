using System.Diagnostics;
using Bogers.Chapoco.Api.Pushover;
using Microsoft.Extensions.Options;

namespace Bogers.Chapoco.Api.Pococha;

public class PocochaHeaderStoreUpdater : TimedBackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public PocochaHeaderStoreUpdater(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    protected override TimeSpan Period { get; } = TimeSpan.FromMinutes(1);
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
            if (!Directory.Exists(pocochaConfiguration.FlowsDirectory)) return;

            // filenames are assumed to be sortable by date
            var flowParser = new MitmFlowParser();
            var files = Directory.EnumerateFiles(pocochaConfiguration.FlowsDirectory)
                .OrderBy(f => f);

            var didUpdate = false;

            foreach (var flowFile in files)
            {
                // skip files greater than ~50-100mb -> likely garbage for our purposes

                try
                {
                    var har = await flowParser
                        .ParseToHar(flowFile); // <-- add ability to filter flows (only requests to pococha?)
                    didUpdate = pocochaHeaderStore.UpdateFromHar(har) || didUpdate;
                    
                    // delete processed
                    if (!DebugHelper.IsDebugMode) File.Delete(flowFile); // <-- dry mode?
                    else { /* log */ }
                    

                    // send pushover notification on successful update?
                }
                catch (Exception e)
                {
                    // log
                }
            }
            
            // log updated status

            if (didUpdate)
            {
                var authenticatedLabel = await pococha.IsAuthenticated() ?
                    "Authenticated" :
                    "Unauthenticated";
                
                await pushover.SendMessage(
                    PushoverMessage.Text("Pococha token updated", $"Current authentication status: {authenticatedLabel}")    
                );
            }
        }
        catch (Exception e)
        {
            // log
        }
    }
}