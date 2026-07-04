using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BingCook.Api.Dtos.Chat;
using BingCook.Api.Models;
using BingCook.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BingCook.Api.Hubs;

[Authorize]
public sealed class ChatHub : Hub
{
    private readonly IChatService _chatService;

    public ChatHub(IChatService chatService)
    {
        _chatService = chatService;
    }

    public static string ConversationGroup(Guid conversationId)
    {
        return $"chat:conversation:{conversationId}";
    }

    public async Task JoinConversation(Guid conversationId)
    {
        var user = GetUserContext();
        var access = await _chatService.GetConversationAccessAsync(
            user,
            conversationId,
            Context.ConnectionAborted);

        if (access is null)
        {
            throw new HubException("Conversation not found.");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            ConversationGroup(conversationId),
            Context.ConnectionAborted);
    }

    public Task LeaveConversation(Guid conversationId)
    {
        return Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            ConversationGroup(conversationId),
            Context.ConnectionAborted);
    }

    public async Task<ChatMessageResponse> SendMessage(
        Guid conversationId,
        string body)
    {
        var user = GetUserContext();
        var result = await _chatService.SendMessageAsync(
            user,
            new SendChatMessageCommand(
                user.UserId,
                conversationId,
                body),
            Context.ConnectionAborted);

        if (result.Status != ChatMessageOutcomeStatus.Success)
        {
            throw new HubException(result.Error ?? "Unable to send message.");
        }

        var response = ChatDtoMapper.ToResponse(result.Message!);
        await Clients
            .Group(ConversationGroup(conversationId))
            .SendAsync("message.created", response, Context.ConnectionAborted);

        return response;
    }

    public async Task MarkRead(Guid conversationId)
    {
        var user = GetUserContext();
        var updated = await _chatService.MarkReadAsync(
            user,
            conversationId,
            Context.ConnectionAborted);

        if (!updated)
        {
            throw new HubException("Conversation not found.");
        }

        await Clients
            .Group(ConversationGroup(conversationId))
            .SendAsync(
                "conversation.read",
                new { conversationId, userId = user.UserId },
                Context.ConnectionAborted);
    }

    private ChatUserContext GetUserContext()
    {
        var id = Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(id, out var userId))
        {
            throw new HubException("Invalid access token.");
        }

        return new ChatUserContext(
            userId,
            Context.User?.FindFirstValue(ClaimTypes.Role));
    }
}
