using BingCook.Api.Models;

namespace BingCook.Api.Data;

public interface IChatRepository
{
    Task<PropertyChatContext?> GetPropertyContextAsync(
        Guid propertyId,
        CancellationToken cancellationToken);

    Task<BookingChatContext?> GetBookingContextAsync(
        Guid bookingId,
        CancellationToken cancellationToken);

    Task<ChatConversation?> FindOpenConversationAsync(
        Guid propertyId,
        Guid customerUserId,
        Guid? bookingId,
        CancellationToken cancellationToken);

    Task<ChatConversation> CreateConversationAsync(
        Guid propertyId,
        Guid? bookingId,
        Guid customerUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatConversation>> GetConversationsAsync(
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken);

    Task<ChatConversationAccess?> GetConversationAccessAsync(
        Guid conversationId,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(
        Guid conversationId,
        DateTime? before,
        int take,
        CancellationToken cancellationToken);

    Task<ChatMessage> AddMessageAsync(
        Guid conversationId,
        Guid senderUserId,
        string body,
        bool senderIsCustomer,
        CancellationToken cancellationToken);

    Task MarkReadAsync(
        Guid conversationId,
        bool readerIsCustomer,
        CancellationToken cancellationToken);
}
