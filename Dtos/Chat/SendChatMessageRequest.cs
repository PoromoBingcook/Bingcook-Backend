using System.ComponentModel.DataAnnotations;

namespace BingCook.Api.Dtos.Chat;

public sealed class SendChatMessageRequest
{
    [Required]
    [MaxLength(2000)]
    public string Body { get; init; } = string.Empty;
}
