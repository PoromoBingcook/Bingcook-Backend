namespace BingCook.Api.Dtos.Bookings;

public sealed record BookingDraftResponse(
    Guid BookingId,
    Guid PropertyId,
    string PropertyName,
    Guid RoomId,
    string RoomName,
    string RoomType,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Nights,
    int Adults,
    int Children,
    int TotalGuests,
    int RoomQuantity,
    int MaxGuests,
    int AvailableRooms,
    decimal RoomSubtotal,
    decimal AddOnSubtotal,
    decimal TotalPrice,
    IReadOnlyList<BookingAddOnResponse> AddOns,
    string? Note,
    string NextAction);

public sealed record BookingAddOnResponse(
    string Code,
    string Name,
    string PricingType,
    decimal UnitPrice,
    decimal TotalPrice);
