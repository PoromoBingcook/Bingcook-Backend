namespace BingCook.Api.Dtos.Chat;

public sealed record ChatMessageResponse(
    Guid Id,
    Guid ConversationId,
    Guid SenderUserId,
    string SenderName,
    string Body,
    DateTime CreatedAt);
