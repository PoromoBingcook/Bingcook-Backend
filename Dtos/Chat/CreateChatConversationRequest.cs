using System.ComponentModel.DataAnnotations;

namespace BingCook.Api.Dtos.Chat;

public sealed class CreateChatConversationRequest
{
    [Required]
    public Guid PropertyId { get; init; }

    public Guid? BookingId { get; init; }
}
