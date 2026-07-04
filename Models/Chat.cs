namespace BingCook.Api.Models;

public sealed record ChatUserContext(
    Guid UserId,
    string? Role);

public sealed record CreateChatConversationCommand(
    Guid UserId,
    Guid PropertyId,
    Guid? BookingId);

public sealed record SendChatMessageCommand(
    Guid UserId,
    Guid ConversationId,
    string Body);

public sealed record ChatConversation(
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

public sealed record ChatConversationAccess(
    ChatConversation Conversation,
    bool IsCustomer,
    bool IsHost,
    bool IsAdmin);

public sealed record ChatMessage(
    Guid Id,
    Guid ConversationId,
    Guid SenderUserId,
    string SenderName,
    string Body,
    DateTime CreatedAt);

public sealed record PropertyChatContext(
    Guid PropertyId,
    string PropertyName,
    Guid? HostUserId);

public sealed record BookingChatContext(
    Guid BookingId,
    Guid PropertyId,
    Guid CustomerUserId);

public sealed record CreateChatConversationResult(
    ChatConversationOutcomeStatus Status,
    ChatConversation? Conversation,
    string? Error)
{
    public static CreateChatConversationResult Success(ChatConversation conversation)
    {
        return new CreateChatConversationResult(
            ChatConversationOutcomeStatus.Success,
            conversation,
            null);
    }

    public static CreateChatConversationResult NotFound(string error)
    {
        return new CreateChatConversationResult(
            ChatConversationOutcomeStatus.NotFound,
            null,
            error);
    }

    public static CreateChatConversationResult ValidationError(string error)
    {
        return new CreateChatConversationResult(
            ChatConversationOutcomeStatus.ValidationError,
            null,
            error);
    }
}

public sealed record SendChatMessageResult(
    ChatMessageOutcomeStatus Status,
    ChatMessage? Message,
    string? Error)
{
    public static SendChatMessageResult Success(ChatMessage message)
    {
        return new SendChatMessageResult(
            ChatMessageOutcomeStatus.Success,
            message,
            null);
    }

    public static SendChatMessageResult NotFound(string error)
    {
        return new SendChatMessageResult(
            ChatMessageOutcomeStatus.NotFound,
            null,
            error);
    }

    public static SendChatMessageResult Forbidden(string error)
    {
        return new SendChatMessageResult(
            ChatMessageOutcomeStatus.Forbidden,
            null,
            error);
    }

    public static SendChatMessageResult ValidationError(string error)
    {
        return new SendChatMessageResult(
            ChatMessageOutcomeStatus.ValidationError,
            null,
            error);
    }
}

public enum ChatConversationOutcomeStatus
{
    Success,
    ValidationError,
    NotFound
}

public enum ChatMessageOutcomeStatus
{
    Success,
    ValidationError,
    NotFound,
    Forbidden
}
