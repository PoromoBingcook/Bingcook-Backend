using BingCook.Api.Models;

namespace BingCook.Api.Services;

public interface IChatService
{
    Task<CreateChatConversationResult> CreateConversationAsync(
        CreateChatConversationCommand command,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatConversation>> GetConversationsAsync(
        ChatUserContext user,
        CancellationToken cancellationToken);

    Task<ChatConversationAccess?> GetConversationAccessAsync(
        ChatUserContext user,
        Guid conversationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatMessage>?> GetMessagesAsync(
        ChatUserContext user,
        Guid conversationId,
        DateTime? before,
        int take,
        CancellationToken cancellationToken);

    Task<SendChatMessageResult> SendMessageAsync(
        ChatUserContext user,
        SendChatMessageCommand command,
        CancellationToken cancellationToken);

    Task<bool> MarkReadAsync(
        ChatUserContext user,
        Guid conversationId,
        CancellationToken cancellationToken);
}
