﻿using Bogers.Chapoco.Api.Pushover;

namespace Bogers.Chapoco.Api.Pococha;

/// <summary>
/// Service for monitoring current followed users live status, sends alerts when a new user goes live
/// </summary>
public class PocochaLiveMonitorService : TimedBackgroundService
{
    private readonly ILogger _logger;
    
    private readonly IServiceProvider _serviceProvider;
    private readonly ISet<int> _previous = new HashSet<int>();

    public PocochaLiveMonitorService(ILogger<PocochaLiveMonitorService> logger, IServiceProvider serviceProvider) : base(logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override TimeSpan Interval { get; } = TimeSpan.FromMinutes(1);
    
    protected override async Task Run(CancellationToken stoppingToken)
    {
        using var serviceScope = _serviceProvider.CreateScope();
        
        var pococha = serviceScope.ServiceProvider.GetRequiredService<PocochaClient>();
        var pushover = serviceScope.ServiceProvider.GetRequiredService<PushoverClient>();
        
        try
        {
            _logger.LogInformation("Checking live status of following users");
            
            var currentlyLive = await pococha.GetFollowingCurrentlyLive(stoppingToken);
            var currentlyLiveUsers = currentlyLive.LiveResources
                .Select(x => x.User.Id)
                .ToHashSet();

            var newLiveUsers = currentlyLiveUsers
                .Except(_previous)
                .ToArray();
            
            _logger.LogInformation("Found {NewLiveUsers} new live users, currently {CurrentLiveUsers} live", newLiveUsers.Length, newLiveUsers.Length);
                
            // clear previous run, we'll replace it with our current collection
            _previous.Clear();
                
            foreach (var userId in newLiveUsers)
            {
                _previous.Add(userId);
                    
                var liveResource = currentlyLive.LiveResources
                    .First(x => x.User.Id == userId);

                // todo: link to chapoco.bogers.online
                await pushover.SendMessage(PushoverMessage.Text($"{liveResource.User.Name} went live!", liveResource.Live.Title) with
                {
                    UrlTitle = "View stream",
                    Url = $"https://chapoco.bogers.online/view/{liveResource.Live.Id}"
                });
            }
        }
        catch (TokenExpiredException)
        {
            // swallow
        }
        catch (Exception e)
        {
            // log
            _logger.LogWarning(e, "Failed to retrieve live users");
        }
    }
}