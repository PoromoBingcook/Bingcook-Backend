using BingCook.Api.Models;

namespace BingCook.Api.Data;

public interface IBookingRepository
{
    Task<BookingRoomQuote?> GetRoomQuoteAsync(
        Guid propertyId,
        Guid roomId,
        DateOnly checkIn,
        DateOnly checkOut,
        CancellationToken cancellationToken);

    Task<Guid> CreateDraftAsync(
        CreateBookingDraftCommand command,
        CancellationToken cancellationToken);

    Task<BookingCheckoutQuote?> GetCheckoutQuoteAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> CompleteCheckoutAsync(
        CompleteBookingCheckoutCommand command,
        CancellationToken cancellationToken);

    Task<bool> UpdatePayOSPaymentAsync(
        PayOSPaymentUpdateCommand command,
        CancellationToken cancellationToken);
}
