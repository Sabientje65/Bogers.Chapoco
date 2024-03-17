using Bogers.Chapoco.Api.Pushover;

namespace Bogers.Chapoco.Api.Pococha;

public class PocochaHeaderStoreUpdater : TimedBackgroundService
{
    private readonly PocochaConfiguration _pocochaConfiguration;
    private readonly PocochaHeaderStore _pocochaHeaderStore;
    private readonly PocochaClient _pococha;
    private readonly PushoverClient _pushover;

    public PocochaHeaderStoreUpdater(PocochaConfiguration pocochaConfiguration, PocochaHeaderStore pocochaHeaderStore, PocochaClient pococha, PushoverClient pushover)
    {
        _pocochaConfiguration = pocochaConfiguration;
        _pocochaHeaderStore = pocochaHeaderStore;
        _pococha = pococha;
        _pushover = pushover;
    }

    protected override TimeSpan Period { get; } = TimeSpan.FromMinutes(1);
    protected override async Task Run(CancellationToken stoppingToken)
    {
        // should migrate to filesystemwatcher
        
        try
        {
            if (!Directory.Exists(_pocochaConfiguration.FlowsDirectory)) return;

            // filenames are assumed to be sortable by date
            var flowParser = new MitmFlowParser();
            var files = Directory.EnumerateFiles(_pocochaConfiguration.FlowsDirectory)
                .OrderBy(f => f);

            var didUpdate = false;

            foreach (var flowFile in files)
            {
                // skip files greater than ~50-100mb -> likely garbage for our purposes

                try
                {
                    var har = await flowParser
                        .ParseToHar(flowFile); // <-- add ability to filter flows (only requests to pococha?)
                    didUpdate = _pocochaHeaderStore.UpdateFromHar(har) || didUpdate;
                    
                    // delete processed
                    File.Delete(flowFile);

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
                var authenticatedLabel = await _pococha.IsAuthenticated() ?
                    "Authenticated" :
                    "Unauthenticated";
                
                await _pushover.SendMessage(
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