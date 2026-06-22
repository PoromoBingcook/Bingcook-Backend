using System.ComponentModel.DataAnnotations;

namespace BingCook.Api.Dtos.Bookings;

public sealed class CreateBookingDraftRequest
{
    [Required]
    public Guid PropertyId { get; init; }

    [Required]
    public Guid RoomId { get; init; }

    public DateOnly CheckIn { get; init; }

    public DateOnly CheckOut { get; init; }

    [Range(0, 50)]
    public int Adults { get; init; } = 1;

    [Range(0, 50)]
    public int Children { get; init; }

    [Range(1, 20)]
    public int RoomQuantity { get; init; } = 1;

    public IReadOnlyList<string>? AddOns { get; init; }

    [MaxLength(1000)]
    public string? Note { get; init; }
}
