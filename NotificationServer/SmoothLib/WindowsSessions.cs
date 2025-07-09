using System.Runtime.InteropServices;

namespace SmoothLib;

public static class WindowsSessions
{
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
    public static extern bool WTSSendMessage(
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

    /// <summary>
    /// Opens a handle to a specified Remote Desktop Session Host (RD Session Host) server.
    /// </summary>
    /// <param name="pServerName">
    /// The name of the server to open. Use <c>null</c> or an empty string to specify the local server.
    /// </param>
    /// <returns>
    /// A handle to the specified server, or <see cref="IntPtr.Zero"/> if the operation fails.
    /// </returns>
    /// <remarks>
    /// The returned handle should be closed using <see cref="WTSCloseServer(IntPtr)"/> when it is no longer needed.
    /// </remarks>
    [DllImport("wtsapi32.dll")]
    static extern IntPtr WTSOpenServer([MarshalAs(UnmanagedType.LPStr)] string pServerName);

    /// <summary>
    /// Closes an open handle to a Remote Desktop Session Host (RD Session Host) server.
    /// </summary>
    /// <param name="hServer">
    /// A handle to the server to close. This handle must have been opened by <see cref="WTSOpenServer(string)"/>.
    /// </param>
    /// <remarks>
    /// After calling this method, the handle is no longer valid and should not be used.
    /// </remarks>
    [DllImport("wtsapi32.dll")]
    static extern void WTSCloseServer(IntPtr hServer);

    /// <summary>
    /// Enumerates the sessions on a specified Remote Desktop Session Host (RD Session Host) server.
    /// </summary>
    /// <param name="hServer">
    /// A handle to the server whose sessions are to be enumerated. Use <see cref="IntPtr.Zero"/> for the current server.
    /// </param>
    /// <param name="Reserved">
    /// Reserved; must be zero.
    /// </param>
    /// <param name="Version">
    /// The version of the enumeration request. Must be 1.
    /// </param>
    /// <param name="ppSessionInfo">
    /// On output, receives a pointer to an array of <see cref="WTS_SESSION_INFO"/> structures that represent the sessions on the server.
    /// </param>
    /// <param name="pCount">
    /// On output, receives the number of <see cref="WTS_SESSION_INFO"/> structures returned in <paramref name="ppSessionInfo"/>.
    /// </param>
    /// <returns>
    /// If the function succeeds, the return value is nonzero. If the function fails, the return value is zero.
    /// </returns>
    /// <remarks>
    /// The memory returned in <paramref name="ppSessionInfo"/> must be freed by calling <see cref="WTSFreeMemory(IntPtr)"/>.
    /// </remarks>
    [DllImport("wtsapi32.dll")]
    static extern Int32 WTSEnumerateSessions(
        IntPtr hServer,
        [MarshalAs(UnmanagedType.U4)] Int32 Reserved,
        [MarshalAs(UnmanagedType.U4)] Int32 Version,
        ref IntPtr ppSessionInfo,
        [MarshalAs(UnmanagedType.U4)] ref Int32 pCount);

    /// <summary>
    /// Frees memory allocated by a WTS API function, such as WTSEnumerateSessions or WTSQuerySessionInformation.
    /// </summary>
    /// <param name="pMemory">
    /// A pointer to the memory block to be freed. This pointer must have been returned by a WTS API function.
    /// </param>
    /// <remarks>
    /// After calling this method, the memory pointed to by <paramref name="pMemory"/> is no longer valid and should not be used.
    /// </remarks>
    [DllImport("wtsapi32.dll")]
    static extern void WTSFreeMemory(IntPtr pMemory);

    /// <summary>
    /// Retrieves session information for a specified session on a Remote Desktop Session Host (RD Session Host) server.
    /// </summary>
    /// <param name="hServer">
    /// A handle to the server on which the session resides. Use <see cref="IntPtr.Zero"/> for the current server.
    /// </param>
    /// <param name="sessionId">
    /// The identifier of the session for which information is to be retrieved.
    /// </param>
    /// <param name="wtsInfoClass">
    /// A value from the <see cref="WTS_INFO_CLASS"/> enumeration that specifies the type of information to retrieve.
    /// </param>
    /// <param name="ppBuffer">
    /// On output, receives a pointer to a buffer containing the requested information. The format and contents of this buffer depend on the value of <paramref name="wtsInfoClass"/>.
    /// The buffer must be freed by calling <see cref="WTSFreeMemory(IntPtr)"/> when it is no longer needed.
    /// </param>
    /// <param name="pBytesReturned">
    /// On output, receives the number of bytes returned in <paramref name="ppBuffer"/>.
    /// </param>
    /// <returns>
    /// <c>true</c> if the function succeeds; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// This function can be used to retrieve various types of information about a session, such as the username, domain, client name, and more.
    /// The caller is responsible for freeing the memory allocated for <paramref name="ppBuffer"/> by calling <see cref="WTSFreeMemory(IntPtr)"/>.
    /// </remarks>
    [DllImport("wtsapi32.dll")]
    static extern bool WTSQuerySessionInformation(
        IntPtr hServer, int sessionId, WTS_INFO_CLASS wtsInfoClass, out IntPtr ppBuffer, out uint pBytesReturned);

    [StructLayout(LayoutKind.Sequential)]
    private struct WTS_SESSION_INFO
    {
        public Int32 SessionID;

        [MarshalAs(UnmanagedType.LPStr)]
        public string pWinStationName;

        public WTS_CONNECTSTATE_CLASS State;
    }

    public enum WTS_INFO_CLASS
    {
        WTSInitialProgram,
        WTSApplicationName,
        WTSWorkingDirectory,
        WTSOEMId,
        WTSSessionId,
        WTSUserName,
        WTSWinStationName,
        WTSDomainName,
        WTSConnectState,
        WTSClientBuildNumber,
        WTSClientName,
        WTSClientDirectory,
        WTSClientProductId,
        WTSClientHardwareId,
        WTSClientAddress,
        WTSClientDisplay,
        WTSClientProtocolType
    }

    public enum WTS_CONNECTSTATE_CLASS
    {
        WTSActive,
        WTSConnected,
        WTSConnectQuery,
        WTSShadow,
        WTSDisconnected,
        WTSIdle,
        WTSListen,
        WTSReset,
        WTSDown,
        WTSInit
    }

    private static void AutoWTSFreeMemory(IntPtr pMemory)
    {
        if (pMemory != IntPtr.Zero)
        {
            WTSFreeMemory(pMemory);
        }
    }

    /// <summary>
    /// Retrieves a list of currently logged-in users on the specified Windows server.
    /// </summary>
    /// <param name="serverName">The name of the server to query. Use <c>null</c> or an empty string for the local server.</param>
    /// <returns>
    /// A list of tuples, each containing the session ID and the username (in the format "DOMAIN\Username") of a logged-in user.
    /// </returns>
    /// <remarks>
    /// This method uses the WTSEnumerateSessions and WTSQuerySessionInformation Windows APIs to enumerate sessions and retrieve user information.
    /// </remarks>
    public static List<(int sessionId, string username)> GetLoggedInUsers(string serverName)
    {
        nint sessionInfoPtr = IntPtr.Zero;
        nint serverHandle = WTSOpenServer(serverName);
        try
        {
            var userList = new List<(int sessionId, string username)>();
            Int32 sessionCount = 0;
            int retVal = WTSEnumerateSessions(serverHandle, 0, 1, ref sessionInfoPtr, ref sessionCount);
            nint currentSession = sessionInfoPtr;

            if (retVal != 0)
            {
                for (int i = 0; i < sessionCount; i++)
                {
                    var si = (WTS_SESSION_INFO)Marshal.PtrToStructure(currentSession, typeof(WTS_SESSION_INFO));
                    currentSession += Marshal.SizeOf(typeof(WTS_SESSION_INFO));

                    nint userPtr = IntPtr.Zero;
                    nint domainPtr = IntPtr.Zero;

                    try
                    {
                        WTSQuerySessionInformation(serverHandle, si.SessionID, WTS_INFO_CLASS.WTSUserName, out userPtr, out _);
                        WTSQuerySessionInformation(serverHandle, si.SessionID, WTS_INFO_CLASS.WTSDomainName, out domainPtr, out _);

                        string domain = Marshal.PtrToStringAnsi(domainPtr);
                        string userName = Marshal.PtrToStringAnsi(userPtr);

                        if (!string.IsNullOrWhiteSpace(userName))
                        {
                            userList.Add((si.SessionID, $"{domain}\\{userName}"));
                        }
                    }
                    finally
                    {
                        AutoWTSFreeMemory(userPtr);
                        AutoWTSFreeMemory(domainPtr);
                    }
                }
            }

            return userList;
        }
        finally
        {
            AutoWTSFreeMemory(sessionInfoPtr);

            WTSCloseServer(serverHandle);
        }
    }
}