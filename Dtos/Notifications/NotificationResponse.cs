namespace BingCook.Api.Dtos.Notifications;

public sealed record NotificationResponse(
    Guid Id,
    string Title,
    string Message,
    bool IsRead,
    DateTime CreatedAt);
