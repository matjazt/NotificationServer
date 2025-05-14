using Microsoft.Net.Http.Headers;
using Serilog;
using System.Net;
using System.Text;
using System.Text.Json;

namespace SmoothLib;

/// <summary>
/// Middleware for handling HTTP requests and responses in a smooth and consistent manner.
/// <para>
/// Responsibilities include:
/// <list type="bullet">
/// <item>Logging incoming requests and outgoing responses, including headers and bodies.</item>
/// <item>Enforcing a maximum request size and buffering request/response bodies for logging.</item>
/// <item>Validating an optional shared secret from request headers.</item>
/// <item>Handling exceptions and returning standardized JSON error responses.</item>
/// <item>Applying a watchdog timeout to detect long-running (frozen) requests.</item>
/// </list>
/// </para>
/// <para>
/// Configuration is read from the application's configuration source, including:
/// <list type="bullet">
/// <item><c>MaxRequestSize</c>: Maximum allowed request body size in bytes.</item>
/// <item><c>SharedSecret</c>: Optional shared secret for request validation.</item>
/// <item><c>WatchDogTimeout</c>: Timeout in seconds for frozen callbacks detection.</item>
/// </list>
/// </para>
/// </summary>
public class SmoothMiddleware
{
    private readonly RequestDelegate _next;
    private readonly int _maxRequestSize;
    private readonly string _sharedSecret;
    private readonly string _section = "SmoothMiddleware";
    private readonly int _watchDogTimeout;

    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        // Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public SmoothMiddleware(RequestDelegate next)
    {
        _next = next;
        _maxRequestSize = Config.Main.GetInt32(_section, "MaxRequestSize", 1024 * 1024);
        _sharedSecret = Config.Main.GetEncryptedString(_section, "SharedSecret");
        _watchDogTimeout = Config.Main.GetInt32(_section, "WatchDogTimeout", 300);  // 5 minutes should be enough
        Lg.Information("done");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await LogRequest(context.Request);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SmoothMiddleware: error while logging request");
        }

        var responseBodyStream = context.Response.Body;

        using var timeoutDetector = new TimeoutDetector("HttpRequest", _watchDogTimeout);

        try
        {
            // we need to buffer the response so we can log it
            using var memStream = new MemoryStream();
            context.Response.Body = memStream;

            // Verify whether the shared secret is valid; keep in mind that an empty or missing shared secret is permitted, as its necessity is defined by the controller
            if (_sharedSecret != null
                && context.Request.Headers.TryGetValue("SharedSecret", out var sharedSecret)
                && string.IsNullOrEmpty(sharedSecret) == false
                && sharedSecret != _sharedSecret)
            {
                throw new SmoothException(Err.InvalidRequest, "invalid shared secret");
            }

            // Since we have a default CORS policy, this is not necessary.
            // context.Response.Headers.Append("Access-Control-Allow-Origin", "*");

            // Call the next middleware in the pipeline
            await _next(context);

            // log the response
            try
            {
                memStream.Position = 0;
                using var reader = new StreamReader(memStream, leaveOpen: true);
                LogResponse(context.Response, await reader.ReadToEndAsync());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SmoothMiddleware: error while logging response");
            }

            // copy the output to the original output stream
            memStream.Position = 0;
            await memStream.CopyToAsync(responseBodyStream);
        }
        catch (Exception ex)
        {
            var responseBase = HandleException(ex);

            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.Headers.Remove(HeaderNames.ETag);

            string responseText = JsonSerializer.Serialize(responseBase, _jsonOptions);

            context.Response.Headers[HeaderNames.CacheControl] = "no-cache";
            context.Response.Headers[HeaderNames.Pragma] = "no-cache";
            context.Response.Headers[HeaderNames.Expires] = "-1";
            context.Response.Headers.Remove(HeaderNames.ContentLength); // if it's wrong, we have an issue, but if it's missing, it's ok - server uses chunked transfer.

            if (context.Response.HasStarted)
            {
                // this shouldn't happen
                Log.Error(ex, "SmoothMiddleware: response has already started, cannot modify it");
                throw;
            }

            context.Response.Body = responseBodyStream;
            await context.Response.WriteAsync(responseText);

            LogResponse(context.Response, responseText);
        }
    }

    private static SmoothResponse HandleException(Exception topExc)
    {
        // let's put the exception(s) into a simple list, so we can later process them the same way
        List<Exception> allExceptions = (topExc is AggregateException) ? [.. (topExc as AggregateException).InnerExceptions] : [topExc];

        SmoothResponse r = null;

        foreach (var exc in allExceptions)
        {
            if (exc is SmoothException)
            {
                var m = exc as SmoothException;

                r = new SmoothResponse
                {
                    ReturnCode = (exc as SmoothException).ErrorCode,
                    Message = $"exception: {m.Message}"
                };

                break;
            }
        }

        if (r == null)
        {
            // SmoothException not found, so let's search for other (sort of) known cases

            r = new SmoothResponse();

            foreach (var exc in allExceptions)
            {
                if (exc is ArgumentException or ArgumentOutOfRangeException or ArgumentNullException or InvalidCastException)
                {
                    r.ReturnCode = Err.InvalidArgument;
                }
                else
                {
                    string lowCaseExc = exc.ToString().ToLower();

                    if (lowCaseExc.Contains("unique key") || lowCaseExc.Contains("unique index") || lowCaseExc.Contains("constraint"))
                    {
                        r.ReturnCode = Err.ConstraintViolation;
                    }
                    else if (lowCaseExc.Contains("dapper") || lowCaseExc.Contains("database"))
                    {
                        r.ReturnCode = Err.DatabaseError;
                    }
                    else if (lowCaseExc.Contains("validation failed"))
                    {
                        r.ReturnCode = Err.InvalidRequest;
                    }
                    else if (lowCaseExc.Contains("connectionresetexception"))
                    {
                        // System.Runtime.InteropServices.COMException: The specified network name is no longer available. (0x80070040)
                        // Microsoft.AspNetCore.Connections.ConnectionResetException: The client has disconnected

                        // it happens if client disconnects before the request has been sent completely. Test:
                        //
                        // openssl s_client -connect your.host:443
                        // POST /tralala/ HTTP/1.1
                        // Host: your.host
                        // Connection: close
                        // Content-Length: 344
                        //
                        // and then ctrl-c
                        r.ReturnCode = Err.NonCriticalError;
                    }
                    else
                    {
                        // unrecognizable exception, let's try the next one
                        continue;
                    }
                }

                // we found something recognizable, so we can break the loop
                r.Message = $"exception: {exc.Message}";
                break;
            }

            r.Message ??= $"exception: {topExc.Message}";
        }

        // TODO: determine if it's really an error
        Log.Error(topExc, $"returning ReturnCode {r.ReturnCode} ({r.Message}):");

        return r;
    }

    private async Task LogRequest(HttpRequest request)
    {
        request.EnableBuffering(_maxRequestSize, _maxRequestSize);

        List<string> log =
        [
            "Method: " + request.Method,
            "Path: " + request.Path.Value,
            "Protocol: " + request.Protocol,
            "RemoteIpAddress: " + request.HttpContext?.Connection?.RemoteIpAddress
        ];
        if (!string.IsNullOrEmpty(request.ContentType))
        {
            log.Add("Content type: " + request.ContentType);
        }

        AddKeyValuePairs(request.Headers, "Headers", log);
        AddKeyValuePairs(request.RouteValues, "RouteValues", log);
        AddKeyValuePairs(request.Query, "Query", log);

        request.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        string body = await reader.ReadToEndAsync();
        log.Add("Body: " + body);

        Log.Information("REST request received:\n{apiRequest}", string.Join("\n", log));

        // Reset stream position for further processing
        request.Body.Seek(0, SeekOrigin.Begin);
    }

    private static void LogResponse(HttpResponse response, string responseBody)
    {
        List<string> log =
        [
            "StatusCode: " + response.StatusCode,
            "ContentType: " + response.ContentType
        ];

        AddKeyValuePairs(response.Headers, "Headers", log);
        log.Add("Body: " + responseBody);

        Log.Information("sending REST response:\n{apiResponse}", string.Join("\n", log));
    }

    private static void AddKeyValuePairs<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> pairs, string name, List<string> log)
    {
        if (pairs.Any())
        {
            log.Add(name + ":");
            foreach (var kv in pairs)
            {
                log.Add($"    {kv.Key}: {kv.Value}");
            }
        }
    }
}
