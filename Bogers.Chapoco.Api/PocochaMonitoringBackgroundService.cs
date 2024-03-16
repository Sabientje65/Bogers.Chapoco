namespace Bogers.Chapoco.Api;

public class PocochaConfiguration
{
    /// <summary>
    /// Directory where mitm flows are kept, flows contain the information required to access the pococha api
    /// </summary>
    public string FlowsDirectory { get; set; }
}

public class PocochaMonitoringBackgroundService : BackgroundService
{
    private readonly ILogger _logger;
    
    private readonly PushoverClient _pushover;
    private readonly PocochaHeaderStore _pocochaHeaderStore;
    private readonly PocochaConfiguration _pocochaConfiguration;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (true)
        {
            try
            {

            }
            catch (TaskCanceledException)
            {
                if (stoppingToken.IsCancellationRequested) break;
                throw;
            }
        }
    }

    private async Task UpdateHeaders()
    {
        try
        {
            if (!Directory.Exists(_pocochaConfiguration.FlowsDirectory)) return;
            
            // filenames are assumed to be sortable by date
            var flowParser = new MitmFlowParser();
            var files = Directory.EnumerateFiles(_pocochaConfiguration.FlowsDirectory)
                .OrderBy(f => f);

            foreach (var flowFile in files)
            {
                try
                {
                    var har = await flowParser.ParseToHar(flowFile);
                    var didUpdate = _pocochaHeaderStore.UpdateFromHar(har);
                    
                    // send pushover notification on successful update?
                }
                catch (Exception e)
                {
                    // log
                }
            }
        }
    }

    private async Task<bool> PollSessionValidity()
    {
        // validate session validity, when state differs from previous, send update?
    }

    private async Task PollCurrentlyLive()
    {
        // send pushover notification when someone went live (= diff with previous live now)
    }


    private async Task AlertLive(string who)
    {
        // todo, include url
        await _pushover.SendMessage(new PushoverMessage($"{who} went live on pococha!"));
    }
}