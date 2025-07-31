# NotificationServer

[**NotificationServer**](https://github.com/matjazt/NotificationServer) is a demonstration project showcasing the integration of a C# application with [SvcWatchDog](https://github.com/matjazt/SvcWatchDog). It functions as an independent REST service, offering a method to display message box notifications to all logged-in users.

## Challenge

The objective was to design a monitoring framework capable of reliably detecting application freezes or infinite loops across most of the codebase. Theoretically, when developing without libraries that autonomously spawn new threads, freeze detection is feasible throughout the entire code. However, real-world development often relies on libraries that manage their own threading, making them impossible to monitor from the outset. Despite this, the framework should provide solid coverage of nearly all custom-written code.

## Result

A `Thread.Sleep(Timeout.Infinite)` (for example) should be detected throughout the code, except at the start of the `SmoothMiddleware.InvokeAsync` method, before the `TimeoutDetector` has been initialized.
This basically means that no matter where the code freezes, **SvcWatchDogClient** or **SvcWatchDog** will detect it and make sure the application is restarted, ensuring that the service is always available.

## How it works

**NotificationServer** is monitored by an internal **SvcWatchDogClient** and an external **SvcWatchDog**, working collaboratively. **SvcWatchDogClient** detects internal issues such as frozen threads and timeouts, and when a problem is identified, it initiates an application shutdown, relying on **SvcWatchDog** to restart it. If the shutdown process fails for any reason, **SvcWatchDog** detects the issue through missing **UDP pings** and forcefully restarts the service.

If your're interested, search for `TimeoutDetector` and `SvcWatchDogClient.Main` in the code to see how the monitoring is implemented.

## How to install service

Preparation steps:
- Build release version of **NotificationServer**. **Visual Studio 2022** is recommended, but you can use any other IDE that supports .NET 9.0.
- Download [SvcWatchDog](https://github.com/matjazt/SvcWatchDog) - binary or source, it's up to you
- Customize `scripts\pack.bat` to match your SvcWatchDog folder
- Run `pack.bat` to prepare the distribution folder (**dist**)
- copy **dist** contents to your preffered location

Installation steps (**Admin credentials required**):
- install service: `service\NotificationServerService -i`
- start service: `net start NotificationServerService`
- stop service: `net stop NotificationServerService`
- uninstall service: `service\NotificationServerService -u`

## How to test

First you need to either run the software, either install it as a Windows service. You might want to fix email configuration or disable it (set `EmailOutputEnabled` to false in `NotificationServer.json`).
Then you can use Curl to send a REST request:  

`curl -X POST "http://localhost:7051/NotifyUsers" -H "sharedSecret: Q20mSdspXdnNwFEkY0eJ" -H "Content-Type: application/json" --data "{ \"title\": \"Test message\", \"text\": \"Have a great day!\"}"`

A message box should pop up, containing the title and message from your test request.

Both **NotificationServer** and **SvcWatchDog** generate detailed log files, which you are encouraged to review.

## Dependencies

This project relies on several NuGet packages to enhance logging, API documentation, and OpenAPI integration. Below is a list of the included dependencies:
- **Microsoft.AspNetCore.OpenApi**: Provides OpenAPI support for ASP.NET Core applications, enabling seamless API documentation and integration.
- **Serilog.Extensions.Logging**: Integrates Serilog with ASP.NET Core’s logging framework, allowing structured logging across the application.
- **Serilog.Sinks.Console**: A Serilog sink that outputs logs to the console for easy debugging and real-time monitoring.
- **Serilog.Sinks.File**: Enables log storage in a file, providing persistent logging capabilities for audit and analysis.
- **Swashbuckle.AspNetCore**: Simplifies API documentation and testing by integrating Swagger UI with ASP.NET Core applications.

## Contact

If you have any questions about the demo, I encourage you to [open an issue on GitHub](https://github.com/matjazt/SvcWatchDog/issues).
In case you would like to contact me directly, you can do so at: mt.dev[at]gmx.com .
