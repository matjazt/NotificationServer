using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Serilog;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

namespace SmoothLib;

public class SmoothAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    public Config Config { get; set; }
    public string ConfigSection { get; set; }
}

public class UsernameAndPasswordAuthHandler : AuthenticationHandler<SmoothAuthenticationSchemeOptions>
{
    public UsernameAndPasswordAuthHandler(IOptionsMonitor<SmoothAuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Username", out var username)
            || string.IsNullOrWhiteSpace(username)
            || !Request.Headers.TryGetValue("X-Password", out var password)
            || string.IsNullOrWhiteSpace(password))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing or empty X-Username or X-Password authentication headers"));
        }

        string hash = BasicTools.ComputeSha256(password);

        if (hash == Options.Config.GetString(Options.ConfigSection, $"Password.{username}"))
        {
            string ipAddress = BasicTools.NormalizeIpAddress(Context.Connection.RemoteIpAddress?.ToString());
            string ipFilter = Options.Config.GetString(Options.ConfigSection, $"IpFilter.{username}");
            if (!string.IsNullOrWhiteSpace(ipFilter) && !Regex.IsMatch(ipAddress, ipFilter))
            {
                return Task.FromResult(AuthenticateResult.Fail($"Source IP address {ipAddress} does not match the filter {ipFilter} for user {username}"));
            }

            var claims = new[] { new Claim(ClaimTypes.Name, username) };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        return Task.FromResult(AuthenticateResult.Fail($"Invalid X-Username ({username}) and/or X-Password (hash: {hash})"));
    }
}

public class SharedSecretAuthHandler : AuthenticationHandler<SmoothAuthenticationSchemeOptions>
{
    public SharedSecretAuthHandler(IOptionsMonitor<SmoothAuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-SharedSecret", out var sharedSecret)
            || string.IsNullOrEmpty(sharedSecret))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing or empty X-SharedSecret authentication header"));
        }

        string hash = BasicTools.ComputeSha256(sharedSecret);
        if (hash == Options.Config.GetString(Options.ConfigSection, "SharedSecret"))
        {
            var claims = new[] { new Claim(ClaimTypes.Name, "SharedSecretClient") };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        return Task.FromResult(AuthenticateResult.Fail($"Invalid X-SharedSecret header (hash: {hash})"));
    }
}

public class RestService
{
    public WebApplication App { get; private set; }

    private string _section;

    public RestService(string section)
    {
        _section = section;

        var builder = WebApplication.CreateBuilder();

        /*
        The following line loads configuration directly from configuration file:
        builder.Configuration.AddConfiguration(Config.Main.Configuration);

        Configuration example:
        "Kestrel": {
            "EndPoints": {
              "Http": {
                "Url": "http://localhost:7090"
              },
              "Https": {
                "Url": "https://localhost:7091"
              }
            },
            "Limits": {
              "MaxConcurrentConnections": 10,
              "MaxRequestBodySize": 10
            },
            "Certificates": {
              "Default": {
                "Path": "etc/zen-cert.p12",
                "Password": "pass"
              }
            }
          },

        All this would be wonderful if it really worked, but it seems the Limits section is ignored. The following line doesn't solve it:
        builder.Services.Configure<KestrelServerOptions>(Config.Main.Configuration.GetSection("Kestrel"));

        Besides, certificate passwords have to be in plain text if using the default configuration method.
        Final conclusion: do it yourself.
        */

        // builder.Configuration.AddEnvironmentVariables();

        if (BasicTools.DevelopmentMode)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        }

        // Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "");

        builder.Logging.ClearProviders(); // Clears all default providers like ConsoleLogger
        // builder.Logging.AddConsole(); // Re-add ConsoleLogger if needed
        // builder.Logging.AddDebug();   // (Optional) Add other providers if required

        // This isn't working—Serilog probably overrides the local settings. I don't fully understand, but I could try chaining
        // loggers to enable separate log level configuration for my code and Microsoft's framework
        // builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);
        // builder.Logging.SetMinimumLevel(LogLevel.Error);

        builder.Logging.AddSerilog();

        // Configure application URL and optional certificate
        int tcpPort = Config.Main.GetInt32(_section, "TcpPort", 0);
        string certPath = Config.Main.GetString(_section, "CertPath");
        string certPassword = Config.Main.GetEncryptedString(_section, "CertPassword");

        // builder.WebHost.UseUrls($"https://[::]:{tcpPort}");

        builder.WebHost.ConfigureKestrel(options =>
        {
            // NOTE: when running from Visual Studio, a warning is shown, for example:
            // Microsoft.AspNetCore.Server.Kestrel: Overriding address(es) 'https://localhost:5001/, http://localhost:5000/'.Binding to
            // endpoints defined via IConfiguration and/ or UseKestrel() instead.
            // There is no issue when running from outside of Visual Studio (no launchSettings.json...)
            options.ListenAnyIP(tcpPort, listenOptions =>
            {
                if (!string.IsNullOrWhiteSpace(certPath) || !string.IsNullOrWhiteSpace(certPassword))
                {
                    listenOptions.UseHttps(certPath, certPassword);
                }
            });
        });

        // Add Swagger services
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // Add CORS policy
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("DefaultCorsPolicy", builder =>
            {
                string[] origins = Config.Main.GetString(_section, "CorsOrigins", "http://localhost:3000")
                    .Split([';', ' ', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (origins.Length == 1 && origins[0] == "*")
                {
                    builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
                }
                else if (origins.Length > 0)
                {
                    builder.WithOrigins(origins)
                       .AllowAnyMethod()
                       .AllowAnyHeader();
                }
            });
        });

        bool authNeeded = false;
        string sharedSecretSection = Config.Main.GetString(_section, "SharedSecretSection");
        if (!string.IsNullOrWhiteSpace(sharedSecretSection))
        {
            string authName = "SharedSecretAuthentication";
            builder.Services.AddAuthentication(authName)
                .AddScheme<SmoothAuthenticationSchemeOptions, SharedSecretAuthHandler>(authName, options =>
                {
                    options.Config = Config.Main;
                    options.ConfigSection = sharedSecretSection;
                });
            authNeeded = true;
        }

        string usernameAndPasswordSection = Config.Main.GetString(_section, "UsernameAndPasswordSection");
        if (!string.IsNullOrWhiteSpace(usernameAndPasswordSection))
        {
            string authName = "UsernameAndPasswordAuthentication";
            builder.Services.AddAuthentication(authName)
                .AddScheme<SmoothAuthenticationSchemeOptions, UsernameAndPasswordAuthHandler>(authName, options =>
                {
                    options.Config = Config.Main;
                    options.ConfigSection = usernameAndPasswordSection;
                });
            authNeeded = true;
        }

        // Add MVC controllers
        builder.Services.AddControllers();

        // Add services to the container.
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        App = builder.Build();

        if (BasicTools.DevelopmentMode) // also works: app.Environment.IsDevelopment()
        {
            Lg.Information("running in development mode");
            App.MapOpenApi();
            App.UseSwagger();   // Generates Swagger/OpenAPI documentation
            App.UseSwaggerUI(); // Adds a web interface to test the API
        }

        App.UseCors("DefaultCorsPolicy");

        // app.UseHttpsRedirection();

        if (authNeeded)
        {
            App.UseAuthentication();
            App.UseAuthorization();
        }

        // Register your custom middleware
        App.UseMiddleware<SmoothMiddleware>();
        App.MapDefaultControllerRoute();
    }

    public void Stop(Task webServiceTask)
    {
        Lg.Information("stopping");
        if (webServiceTask?.IsCompleted == false)
        {
            try
            {
                App?.StopAsync()?.Wait();
            }
            catch { }
        }

        webServiceTask?.Wait(2000);
        Lg.Information("stopped");
    }
}

