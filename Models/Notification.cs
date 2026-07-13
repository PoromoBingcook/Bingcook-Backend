namespace BingCook.Api.Models;

public sealed record CreateNotificationCommand(
    Guid UserId,
    string Title,
    string Message);

public sealed record UserNotification(
    Guid Id,
    Guid? UserId,
    string Title,
    string Message,
    bool IsRead,
    DateTime CreatedAt);
