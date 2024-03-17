namespace Bogers.Chapoco.Api.Pushover;

public class PushoverMessage
{
    public string UrlTitle { get; set; }
    public string Url { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    
    private PushoverMessage() { }

    public static PushoverMessage Text(string title, string message) => new PushoverMessage
    {
        Message = message, 
        Title = title
    };

    public static PushoverMessage Text(string message) => new PushoverMessage
    {
        Message = message
    };
}