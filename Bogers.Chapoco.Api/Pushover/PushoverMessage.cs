using System.Buffers.Text;
using System.Text;

namespace Bogers.Chapoco.Api.Pushover;

public class PushoverMessage
{
    public string UrlTitle { get; private set; }
    public string Url { get; private set; }
    public string Attachment_Base64 { get; private set; }
    public string AttachmentType { get; private set; }
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

    public PushoverMessage WithUrl(string url)
    {
        Url = url;
        return this;
    }

    public PushoverMessage WithUrl(string url, string urlTitle)
    {
        Url = url;
        UrlTitle = urlTitle;
        return this;
    }

    public PushoverMessage WithImage(byte[] image, string mimeType)
    {
        Attachment_Base64 = Convert.ToBase64String(image);
        AttachmentType = mimeType;
        return this;
    }
}