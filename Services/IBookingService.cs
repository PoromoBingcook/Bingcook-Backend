using BingCook.Api.Models;

namespace BingCook.Api.Services;

public interface IBookingService
{
    Task<BookingDraftOutcome> CreateDraftAsync(
        BookingSelectionCommand command,
        CancellationToken cancellationToken);

    Task<BookingCheckoutOutcome> CheckoutAsync(
        BookingCheckoutCommand command,
        CancellationToken cancellationToken);

    Task<bool> UpdatePayOSPaymentAsync(
        PayOSPaymentUpdateCommand command,
        CancellationToken cancellationToken);
}
