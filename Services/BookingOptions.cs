namespace BingCook.Api.Services;

public sealed class BookingOptions
{
    public const string SectionName = "Booking";

    public int HoldMinutes { get; init; } = 15;
}
