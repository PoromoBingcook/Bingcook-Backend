namespace BingCook.Api.Dtos.Chat;

public sealed record ChatConversationResponse(
    Guid Id,
    Guid PropertyId,
    string PropertyName,
    Guid? BookingId,
    Guid CustomerUserId,
    string CustomerName,
    Guid? HostUserId,
    string Status,
    DateTime? LastMessageAt,
    DateTime? CustomerLastReadAt,
    DateTime? HostLastReadAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
