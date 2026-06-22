namespace BingCook.Api.Models;

public sealed record BookingSelectionCommand(
    Guid UserId,
    Guid PropertyId,
    Guid RoomId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Adults,
    int Children,
    int RoomQuantity,
    IReadOnlyList<string> AddOns,
    string? Note);

public sealed record CreateBookingDraftCommand(
    Guid UserId,
    Guid PropertyId,
    Guid RoomId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Adults,
    int Children,
    int RoomQuantity,
    int TotalGuests,
    decimal TotalPrice,
    IReadOnlyList<string> AddOns,
    string? Note);

public sealed record BookingRoomQuote(
    Guid PropertyId,
    string PropertyName,
    Guid RoomId,
    string RoomName,
    string RoomType,
    int Capacity,
    int TotalRooms,
    int AvailableRooms,
    decimal PricePerNight);

public sealed record BookingDraft(
    Guid Id,
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
    IReadOnlyList<BookingAddOn> AddOns,
    string? Note);

public sealed record BookingAddOn(
    string Code,
    string Name,
    string PricingType,
    decimal UnitPrice,
    decimal TotalPrice);
