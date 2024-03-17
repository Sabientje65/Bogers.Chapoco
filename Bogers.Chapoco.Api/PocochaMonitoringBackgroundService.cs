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
    private readonly PocochaClient _pococha;
    private readonly PocochaHeaderStore _pocochaHeaderStore;
    private readonly PocochaConfiguration _pocochaConfiguration;
    
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        StartMonitoringPocochaFlows(stoppingToken);
        StartMonitoringValidity(stoppingToken);
        StartMonitoringLives(stoppingToken);

        var tsc = new TaskCompletionSource();
        stoppingToken.Register(() => tsc.SetResult());
        
        return tsc.Task;
    }

    private void StartMonitoringValidity(CancellationToken token)
    {
        var alerter = new PocochaAuthenticationAlerter(
            _pococha, 
            _pushover
        );
        
        var timer = new Timer(
            _ => alerter.Run().Wait()
        );

        timer.Change(TimeSpan.Zero, TimeSpan.FromMinutes(1));
        token.Register(() => timer.Dispose());
    }

    private void StartMonitoringPocochaFlows(CancellationToken token)
    {
        var flowMonitor = new PocochaFlowUpdater(
            _pocochaConfiguration,
            _pocochaHeaderStore,
            _pococha,
            _pushover
        );
        
        var timer = new Timer(
            _ => flowMonitor.Run().Wait()
        );

        // start immediately, then run every minute
        timer.Change(TimeSpan.Zero, TimeSpan.FromMinutes(1));
        token.Register(() => timer.Dispose());
    }

    private void StartMonitoringLives(CancellationToken token)
    {
        var alerter = new PocochaLiveAlerter(
            _pococha,
            _pushover
        );
        
        var timer = new Timer(
            _ => alerter.Run().Wait()
        );
        
        timer.Change(TimeSpan.Zero, TimeSpan.FromMinutes(1));
        token.Register(() => timer.Dispose());
    }


    class PocochaFlowUpdater
    {
        private readonly PocochaConfiguration _pocochaConfiguration;
        private readonly PocochaHeaderStore _pocochaHeaderStore;
        private readonly PocochaClient _pococha;
        private readonly PushoverClient _pushover;

        public PocochaFlowUpdater(
            PocochaConfiguration pocochaConfiguration, 
            PocochaHeaderStore pocochaHeaderStore, 
            PocochaClient pococha, 
            PushoverClient pushover
        )
        {
            _pocochaConfiguration = pocochaConfiguration;
            _pocochaHeaderStore = pocochaHeaderStore;
            _pococha = pococha;
            _pushover = pushover;
        }

        public async Task Run()
        {
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

    class PocochaLiveAlerter
    {
        private readonly PocochaClient _pococha;
        private readonly PushoverClient _pushover;

        private readonly ISet<int> _previous = new HashSet<int>();

        public PocochaLiveAlerter(PocochaClient pococha, PushoverClient pushover)
        {
            _pococha = pococha;
            _pushover = pushover;
        }

        public async Task Run()
        {
            try
            {
                var currentlyLive = await _pococha.GetCurrentlyLive();
                var currentlyLiveUsers = currentlyLive.LiveResources
                    .Select(x => x.Live.User.Id)
                    .ToHashSet();

                var newLiveUsers = currentlyLiveUsers
                    .Except(_previous)
                    .ToArray();
                
                // clear previous run, we'll replace it with our current collection
                _previous.Clear();
                
                foreach (var userId in newLiveUsers)
                {
                    _previous.Add(userId);
                    
                    var liveResource = currentlyLive.LiveResources
                        .First(x => x.Live.User.Id == userId);

                    // todo: link to chapoco.bogers.online
                    await _pushover.SendMessage(
                        PushoverMessage.Text($"{liveResource.Live.User.Name} went live!", liveResource.Live.Title)
                    );
                }
            }
            catch (TokenExpiredException)
            {
                // swallow
            }
            catch (Exception)
            {
                // log
            }
        }
    }

    class PocochaAuthenticationAlerter
    {
        private readonly PocochaClient _pococha;
        private readonly PushoverClient _pushover;
        private bool _previous = true;
        
        public PocochaAuthenticationAlerter(PocochaClient pococha, PushoverClient pushover)
        {
            _pococha = pococha;
            _pushover = pushover;
        }

        public async Task Run()
        {
            var isAuthenticated = await _pococha.IsAuthenticated();
            var becameUnauthenticated = _previous && !isAuthenticated;
            
            if (becameUnauthenticated) await _pushover.SendMessage(PushoverMessage.Text("Pococha token invalidated", "Please open up the pococha app for a token refresh"));

            _previous = isAuthenticated;
        }
    }
}