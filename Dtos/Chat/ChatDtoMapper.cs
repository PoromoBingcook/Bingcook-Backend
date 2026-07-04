using BingCook.Api.Models;

namespace BingCook.Api.Dtos.Chat;

internal static class ChatDtoMapper
{
    public static ChatConversationResponse ToResponse(ChatConversation conversation)
    {
        return new ChatConversationResponse(
            conversation.Id,
            conversation.PropertyId,
            conversation.PropertyName,
            conversation.BookingId,
            conversation.CustomerUserId,
            conversation.CustomerName,
            conversation.HostUserId,
            conversation.Status,
            conversation.LastMessageAt,
            conversation.CustomerLastReadAt,
            conversation.HostLastReadAt,
            conversation.CreatedAt,
            conversation.UpdatedAt);
    }

    public static ChatMessageResponse ToResponse(ChatMessage message)
    {
        return new ChatMessageResponse(
            message.Id,
            message.ConversationId,
            message.SenderUserId,
            message.SenderName,
            message.Body,
            message.CreatedAt);
    }
}
