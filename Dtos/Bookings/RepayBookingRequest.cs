using System.ComponentModel.DataAnnotations;

namespace BingCook.Api.Dtos.Bookings;

public sealed class RepayBookingRequest
{
    [MaxLength(100)]
    public string? CustomerName { get; init; }

    [EmailAddress]
    [MaxLength(100)]
    public string? CustomerEmail { get; init; }

    [MaxLength(20)]
    public string? CustomerPhone { get; init; }

    [MaxLength(50)]
    public string? IdentityNumber { get; init; }
}
