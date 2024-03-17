using Bogers.Chapoco.Api.Pushover;

namespace Bogers.Chapoco.Api.Pococha;

public class PocochaAuthenticationStateMonitor : TimedBackgroundService
{
    private readonly PocochaClient _pococha;
    private readonly PushoverClient _pushover;
    private bool _previous = true;

    public PocochaAuthenticationStateMonitor(PocochaClient pococha, PushoverClient pushover)
    {
        _pococha = pococha;
        _pushover = pushover;
    }

    protected override TimeSpan Period { get; } = TimeSpan.FromMinutes(1);

    protected override async Task Run(CancellationToken stoppingToken)
    {
        var isAuthenticated = await _pococha.IsAuthenticated();
        var becameUnauthenticated = _previous && !isAuthenticated;
            
        if (becameUnauthenticated) await _pushover.SendMessage(PushoverMessage.Text("Pococha token invalidated", "Please open up the pococha app for a token refresh"));

        _previous = isAuthenticated;
    }
}