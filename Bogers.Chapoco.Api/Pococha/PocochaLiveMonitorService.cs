using System.Net;
using System.Net.Mime;
using Bogers.Chapoco.Api.Pushover;

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
        await using var serviceScope = _serviceProvider.CreateAsyncScope();
        
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
            
            _logger.LogInformation("Found {NewLiveUsers} new live users, currently {CurrentLiveUsers} live", newLiveUsers.Length, currentlyLiveUsers.Count);
                
            // clear previous run, we'll replace it with our current collection
            _previous.Clear();

            foreach (var liveUser in currentlyLiveUsers) _previous.Add(liveUser);
            
            foreach (var userId in newLiveUsers)
            {    
                var liveResource = currentlyLive.LiveResources
                    .First(x => x.User.Id == userId);

                var notification = PushoverMessage.Text($"{liveResource.User.Name} went live!", liveResource.Live.Title)
                    .WithUrl($"https://chapoco.bogers.online/view/{liveResource.Live.Id}", "View Stream");

                try
                {
                    var (thumbnailContent, thumbnailMimeType) = await DownloadThumbnail(liveResource);
                    notification = notification.WithImage(thumbnailContent, thumbnailMimeType);
                }
                catch
                {
                    // swallow
                }
                
                
                await pushover.SendMessage(notification);
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

    private async Task<(byte[] content, string mimeType)> DownloadThumbnail(LiveResource liveResource)
    {
        _logger.LogDebug("Attempting to download thumbnail for live: {LiveId}", liveResource.Live.Id);

        // low timeout - thumbnail simply not important
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(3);

        try
        {
            using var res = await client.GetAsync(liveResource.Live.ThumbnailImageUrl);
            return (
                await res.Content.ReadAsByteArrayAsync(),
                res.Headers.TryGetValues("Content-Type", out var contentType)
                    ? contentType.ToString()
                    : "image/png" // assume png for now, easy
            );
        }
        catch
        {
            _logger.LogWarning("Failed to download thumbnail for live: {LiveId} from {LiveThumbnail}", liveResource.Live.Id, liveResource.Live.ThumbnailImageUrl);
            throw;
        }
    }
}