using Bogers.Chapoco.Api.Pushover;

namespace Bogers.Chapoco.Api.Pococha;

public class PocochaAuthenticationStateMonitor : TimedBackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private bool _previous = true;

    public PocochaAuthenticationStateMonitor(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    protected override TimeSpan Interval { get; } = TimeSpan.FromMinutes(1);

    protected override async Task Run(CancellationToken stoppingToken)
    {
        // alternative: define services as properties -> mark as injected via attribute, have base class manage injection
        // too much magic for now though
        using var serviceScope = _serviceProvider.CreateScope();
        
        var pococha = serviceScope.ServiceProvider.GetRequiredService<PocochaClient>();
        var pushover = serviceScope.ServiceProvider.GetRequiredService<PushoverClient>();
        
        var isAuthenticated = await pococha.IsAuthenticated();
        var becameUnauthenticated = _previous && !isAuthenticated;
            
        if (becameUnauthenticated) await pushover.SendMessage(PushoverMessage.Text("Pococha token invalidated", "Please open up the pococha app for a token refresh"));

        _previous = isAuthenticated;
    }
}