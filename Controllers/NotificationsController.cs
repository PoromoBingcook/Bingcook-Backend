using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BingCook.Api.Data;
using BingCook.Api.Dtos.Notifications;
using BingCook.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BingCook.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationRepository _notificationRepository;

    public NotificationsController(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationResponse>>> GetMine(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var notifications = await _notificationRepository.GetByUserIdAsync(
            userId.Value,
            cancellationToken);

        return Ok(notifications.Select(ToResponse).ToList());
    }

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var updated = await _notificationRepository.MarkReadAsync(
            notificationId,
            userId.Value,
            cancellationToken);

        return updated
            ? NoContent()
            : NotFound(new { message = "Notification not found." });
    }

    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        await _notificationRepository.MarkAllReadAsync(
            userId.Value,
            cancellationToken);

        return NoContent();
    }

    private Guid? GetUserId()
    {
        var id = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var userId) ? userId : null;
    }

    private static NotificationResponse ToResponse(UserNotification notification)
    {
        return new NotificationResponse(
            notification.Id,
            notification.Title,
            notification.Message,
            notification.IsRead,
            notification.CreatedAt);
    }
}
