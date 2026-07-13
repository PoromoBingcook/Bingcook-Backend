using BingCook.Api.Data;
using BingCook.Api.Models;

namespace BingCook.Api.Services;

public sealed class ChatService : IChatService
{
    private const int MaxMessageLength = 2000;
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;

    private readonly IChatRepository _chatRepository;

    public ChatService(IChatRepository chatRepository)
    {
        _chatRepository = chatRepository;
    }

    public async Task<CreateChatConversationResult> CreateConversationAsync(
        CreateChatConversationCommand command,
        CancellationToken cancellationToken)
    {
        if (command.PropertyId == Guid.Empty)
        {
            return CreateChatConversationResult.ValidationError("Property is required.");
        }

        if (command.BookingId is not null && command.BookingId.Value == Guid.Empty)
        {
            return CreateChatConversationResult.ValidationError("Booking is invalid.");
        }

        var property = await _chatRepository.GetPropertyContextAsync(
            command.PropertyId,
            cancellationToken);
        if (property is null)
        {
            return CreateChatConversationResult.NotFound("Property not found.");
        }

        if (property.HostUserId == command.UserId)
        {
            return CreateChatConversationResult.ValidationError(
                "Cannot start a customer chat with your own property.");
        }

        if (command.BookingId is not null)
        {
            var booking = await _chatRepository.GetBookingContextAsync(
                command.BookingId.Value,
                cancellationToken);
            if (booking is null)
            {
                return CreateChatConversationResult.NotFound("Booking not found.");
            }

            if (booking.CustomerUserId != command.UserId)
            {
                return CreateChatConversationResult.ValidationError(
                    "Booking does not belong to this customer.");
            }

            if (booking.PropertyId != command.PropertyId)
            {
                return CreateChatConversationResult.ValidationError(
                    "Booking does not belong to this property.");
            }
        }

        var existing = await _chatRepository.FindOpenConversationAsync(
            command.PropertyId,
            command.UserId,
            command.BookingId,
            cancellationToken);
        if (existing is not null)
        {
            return CreateChatConversationResult.Success(existing);
        }

        var conversation = await _chatRepository.CreateConversationAsync(
            command.PropertyId,
            command.BookingId,
            command.UserId,
            cancellationToken);

        return CreateChatConversationResult.Success(conversation);
    }

    public Task<IReadOnlyList<ChatConversation>> GetConversationsAsync(
        ChatUserContext user,
        CancellationToken cancellationToken)
    {
        return _chatRepository.GetConversationsAsync(
            user.UserId,
            IsAdmin(user),
            cancellationToken);
    }

    public Task<ChatConversationAccess?> GetConversationAccessAsync(
        ChatUserContext user,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        if (conversationId == Guid.Empty)
        {
            return Task.FromResult<ChatConversationAccess?>(null);
        }

        return _chatRepository.GetConversationAccessAsync(
            conversationId,
            user.UserId,
            IsAdmin(user),
            cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessage>?> GetMessagesAsync(
        ChatUserContext user,
        Guid conversationId,
        DateTime? before,
        int take,
        CancellationToken cancellationToken)
    {
        var access = await GetConversationAccessAsync(
            user,
            conversationId,
            cancellationToken);
        if (access is null)
        {
            return null;
        }

        return await _chatRepository.GetMessagesAsync(
            conversationId,
            before,
            NormalizePageSize(take),
            cancellationToken);
    }

    public async Task<SendChatMessageResult> SendMessageAsync(
        ChatUserContext user,
        SendChatMessageCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ConversationId == Guid.Empty)
        {
            return SendChatMessageResult.ValidationError("Conversation is required.");
        }

        var body = NormalizeBody(command.Body);
        if (body is null)
        {
            return SendChatMessageResult.ValidationError("Message body is required.");
        }

        if (body.Length > MaxMessageLength)
        {
            return SendChatMessageResult.ValidationError(
                $"Message body cannot exceed {MaxMessageLength} characters.");
        }

        var access = await GetConversationAccessAsync(
            user,
            command.ConversationId,
            cancellationToken);
        if (access is null)
        {
            return SendChatMessageResult.NotFound("Conversation not found.");
        }

        if (access.Conversation.Status != "Open")
        {
            return SendChatMessageResult.Forbidden(
                $"Conversation is {access.Conversation.Status.ToLowerInvariant()}.");
        }

        var senderIsCustomer = access.IsCustomer && !access.IsAdmin;
        var message = await _chatRepository.AddMessageAsync(
            command.ConversationId,
            user.UserId,
            body,
            senderIsCustomer,
            cancellationToken);

        return SendChatMessageResult.Success(message);
    }

    public async Task<bool> MarkReadAsync(
        ChatUserContext user,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var access = await GetConversationAccessAsync(
            user,
            conversationId,
            cancellationToken);
        if (access is null)
        {
            return false;
        }

        await _chatRepository.MarkReadAsync(
            conversationId,
            access.IsCustomer && !access.IsAdmin,
            cancellationToken);
        return true;
    }

    private static int NormalizePageSize(int take)
    {
        if (take <= 0)
        {
            return DefaultPageSize;
        }

        return Math.Min(take, MaxPageSize);
    }

    private static string? NormalizeBody(string? body)
    {
        var normalized = body?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static bool IsAdmin(ChatUserContext user)
    {
        return string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase);
    }
}
