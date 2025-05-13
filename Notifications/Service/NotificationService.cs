using NotificationServer.Notifications.DTO;
using SmoothLib;

namespace NotificationServer.Notifications.Service;

using System;
using System.Runtime.InteropServices;

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

        // This loop is basically rubbish, we should instead properly figure out the session ids of the users. But since this whole project
        // is just a test, we don't care about that right now.
        for (int sessionId = 1; sessionId < 10; sessionId++)
        {
            WTSSendMessage(IntPtr.Zero, sessionId, req.Title, req.Title.Length, req.Text, req.Text.Length, 0x00051030, 0, out int response, false);
        }

        // testing code: if title is a number, wait for that many seconds, possibly triggering a timeout
        if (int.TryParse(req.Title, out int delaySec))
        {
            Thread.Sleep(delaySec * 1000);
        }

        Lg.Information($"done");
    }

    /// <summary>
    /// Sends a message box to a specified session using the WTSSendMessage Windows API.
    /// </summary>
    /// <param name="hServer">A handle to the server. Use IntPtr.Zero for the current server.</param>
    /// <param name="SessionId">The session identifier.</param>
    /// <param name="pTitle">The title of the message box.</param>
    /// <param name="TitleLength">The length of the title string.</param>
    /// <param name="pMessage">The message to display.</param>
    /// <param name="MessageLength">The length of the message string.</param>
    /// <param name="Style">The style of the message box.</param>
    /// <param name="Timeout">The time-out interval, in seconds.</param>
    /// <param name="pResponse">The user's response.</param>
    /// <param name="bWait">Whether to wait for the user's response.</param>
    /// <returns>True if the message was sent successfully; otherwise, false.</returns>
    [DllImport("wtsapi32.dll")]
    static extern bool WTSSendMessage(
        IntPtr hServer,
        int SessionId,
        string pTitle,
        int TitleLength,
        string pMessage,
        int MessageLength,
        int Style,
        int Timeout,
        out int pResponse,
        bool bWait
    );
}
