using SmoothLib;
using System.Text.Json;

namespace NotificationServer.Notifications.DTO;

/// <summary>
/// Represents a request to send a notification, containing a title and text body.
/// </summary>
public class NotificationRequest
{
    public string Title { get; set; }

    public string Text { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, BasicTools.IndentedJsonSerializerOptions);
    }
}
