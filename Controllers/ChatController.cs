using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BingCook.Api.Dtos.Chat;
using BingCook.Api.Hubs;
using BingCook.Api.Models;
using BingCook.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace BingCook.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/chat")]
public sealed class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IHubContext<ChatHub> _hubContext;

    public ChatController(
        IChatService chatService,
        IHubContext<ChatHub> hubContext)
    {
        _chatService = chatService;
        _hubContext = hubContext;
    }

    [HttpPost("conversations")]
    public async Task<ActionResult<ChatConversationResponse>> CreateConversation(
        CreateChatConversationRequest request,
        CancellationToken cancellationToken)
    {
        var user = GetUserContext();
        if (user is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var result = await _chatService.CreateConversationAsync(
            new CreateChatConversationCommand(
                user.UserId,
                request.PropertyId,
                request.BookingId),
            cancellationToken);

        return result.Status switch
        {
            ChatConversationOutcomeStatus.Success => Ok(ChatDtoMapper.ToResponse(result.Conversation!)),
            ChatConversationOutcomeStatus.ValidationError => BadRequest(new { message = result.Error }),
            ChatConversationOutcomeStatus.NotFound => NotFound(new { message = result.Error }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<IReadOnlyList<ChatConversationResponse>>> GetConversations(
        CancellationToken cancellationToken)
    {
        var user = GetUserContext();
        if (user is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var conversations = await _chatService.GetConversationsAsync(
            user,
            cancellationToken);

        return Ok(conversations.Select(ChatDtoMapper.ToResponse).ToList());
    }

    [HttpGet("conversations/{conversationId:guid}/messages")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageResponse>>> GetMessages(
        Guid conversationId,
        [FromQuery] DateTime? before,
        [FromQuery] int take,
        CancellationToken cancellationToken)
    {
        var user = GetUserContext();
        if (user is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var messages = await _chatService.GetMessagesAsync(
            user,
            conversationId,
            before,
            take,
            cancellationToken);

        return messages is null
            ? NotFound(new { message = "Conversation not found." })
            : Ok(messages.Select(ChatDtoMapper.ToResponse).ToList());
    }

    [HttpPost("conversations/{conversationId:guid}/messages")]
    public async Task<ActionResult<ChatMessageResponse>> SendMessage(
        Guid conversationId,
        SendChatMessageRequest request,
        CancellationToken cancellationToken)
    {
        var user = GetUserContext();
        if (user is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var result = await _chatService.SendMessageAsync(
            user,
            new SendChatMessageCommand(
                user.UserId,
                conversationId,
                request.Body),
            cancellationToken);

        if (result.Status != ChatMessageOutcomeStatus.Success)
        {
            return result.Status switch
            {
                ChatMessageOutcomeStatus.ValidationError => BadRequest(new { message = result.Error }),
                ChatMessageOutcomeStatus.NotFound => NotFound(new { message = result.Error }),
                ChatMessageOutcomeStatus.Forbidden => Forbid(),
                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }

        var response = ChatDtoMapper.ToResponse(result.Message!);
        await _hubContext.Clients
            .Group(ChatHub.ConversationGroup(conversationId))
            .SendAsync("message.created", response, cancellationToken);

        return Ok(response);
    }

    [HttpPost("conversations/{conversationId:guid}/read")]
    public async Task<IActionResult> MarkRead(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var user = GetUserContext();
        if (user is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var updated = await _chatService.MarkReadAsync(
            user,
            conversationId,
            cancellationToken);

        return updated
            ? NoContent()
            : NotFound(new { message = "Conversation not found." });
    }

    private ChatUserContext? GetUserContext()
    {
        var id = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(id, out var userId)
            ? new ChatUserContext(userId, User.FindFirstValue(ClaimTypes.Role))
            : null;
    }
}
