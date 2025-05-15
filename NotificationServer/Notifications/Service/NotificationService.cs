using NotificationServer.Notifications.DTO;
using SmoothLib;

namespace NotificationServer.Notifications.Service;

using System;

/// <summary>
/// Provides notification services, including sending messages to user sessions and service health checks.
/// </summary>
public class NotificationService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationService"/> class.
    /// </summary>
    public NotificationService()
    {

    }

    /// <summary>
    /// Gets or sets the main instance of the <see cref="NotificationService"/>.
    /// </summary>
    public static NotificationService Main { get; set; }

    /// <summary>
    /// Checks if the service is running by logging a simple informational message.
    /// </summary>
    public void Ping()
    {
        // this is just a ping method to check if the service is running
        // it does not do anything
        Lg.Information("doing nothing");
    }

    /// <summary>
    /// Sends a message box notification to user sessions using the WTSSendMessage Windows API.
    /// </summary>
    /// <param name="req">The notification request containing the title and text to display.</param>
    public void NotifyUsers(NotificationRequest req)
    {
        if (req == null)
        {
            throw new SmoothException(Err.InternalError, "null NotificationRequest");
        }
        // this is just a ping method to check if the service is running
        // it does not do anything
        Lg.Information(req.ToString());

        // MB_OK            0x00000000L
        // MB_SYSTEMMODAL   0x00001000L
        // MB_ICONWARNING   0x00000030L
        // MB_SETFOREGROUND 0x00010000L
        // MB_TOPMOST       0x00040000L

        var users = WindowsSessions.GetLoggedInUsers(null);
        foreach (var (sessionId, username) in users)
        {
            WindowsSessions.WTSSendMessage(IntPtr.Zero, sessionId, req.Title, req.Title.Length, req.Text, req.Text.Length, 0x00051030, 0, out int response, false);
        }

        // testing code: if title is a number, wait for that many seconds, possibly triggering a timeout
        if (int.TryParse(req.Title, out int delaySec))
        {
            Thread.Sleep(delaySec * 1000);
        }

        Lg.Information($"done");
    }
}
