using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationServer.Notifications.DTO;
using NotificationServer.Notifications.Service;
using SmoothLib;
using System.ComponentModel.DataAnnotations;
using System.Net;

namespace NotificationServer.Notifications.Controller;

[ApiController]
//[Route("/Notifications")]

/// <summary>
/// Provides REST endpoints for sending notifications to users and checking service health.
/// </summary>
public class MessageBoxController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("Ping")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(SmoothResponse), (int)HttpStatusCode.OK)]
    public SmoothResponse Ping()
    {
        NotificationService.Main.Ping();
        return new SmoothResponse
        {
            ReturnCode = 0,
            Message = "OK",
        };
    }

    [Authorize]
    [HttpPost("NotifyUsers")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(SmoothResponse), (int)HttpStatusCode.OK)]
    public SmoothResponse NotifyUsers([FromBody][Required] NotificationRequest req)
    {
        NotificationService.Main.NotifyUsers(req);
        return new SmoothResponse
        {
            ReturnCode = 0,
            Message = "OK",
        };
    }
}