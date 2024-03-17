using Bogers.Chapoco.Api.Pushover;

namespace Bogers.Chapoco.Api.Pococha;

public class PocochaLiveMonitor : TimedBackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISet<int> _previous = new HashSet<int>();

    public PocochaLiveMonitor(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    protected override TimeSpan Interval { get; } = TimeSpan.FromMinutes(1);
    
    protected override async Task Run(CancellationToken stoppingToken)
    {
        using var serviceScope = _serviceProvider.CreateScope();
        
        var pococha = serviceScope.ServiceProvider.GetRequiredService<PocochaClient>();
        var pushover = serviceScope.ServiceProvider.GetRequiredService<PushoverClient>();
        
        try
        {
            var currentlyLive = await pococha.GetCurrentlyLive(stoppingToken);
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
                await pushover.SendMessage(
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