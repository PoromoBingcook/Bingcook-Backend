using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BingCook.Api.Dtos.Bookings;
using BingCook.Api.Models;
using BingCook.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BingCook.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/bookings")]
public sealed class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingsController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    [HttpPost("draft")]
    public async Task<ActionResult<BookingDraftResponse>> CreateDraft(
        CreateBookingDraftRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var result = await _bookingService.CreateDraftAsync(
            new BookingSelectionCommand(
                userId.Value,
                request.PropertyId,
                request.RoomId,
                request.CheckIn,
                request.CheckOut,
                request.Adults,
                request.Children,
                request.RoomQuantity,
                request.AddOns ?? Array.Empty<string>(),
                request.Note),
            cancellationToken);

        return result.Status switch
        {
            BookingDraftOutcomeStatus.Success => Ok(ToResponse(result.Draft!)),
            BookingDraftOutcomeStatus.ValidationError => BadRequest(new { message = result.Error }),
            BookingDraftOutcomeStatus.NotFound => NotFound(new { message = result.Error }),
            BookingDraftOutcomeStatus.Unavailable => Conflict(new { message = result.Error }),
            BookingDraftOutcomeStatus.ExistingPayment => Conflict(new
            {
                message = result.Error,
                code = "PendingPaymentExists",
                bookingId = result.ExistingBookingId
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("checkout")]
    public async Task<ActionResult<BookingCheckoutResponse>> Checkout(
        CheckoutBookingRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var result = await _bookingService.CheckoutAsync(
            new BookingCheckoutCommand(
                userId.Value,
                request.BookingId,
                request.PaymentMethod,
                request.CustomerName,
                request.CustomerEmail,
                request.CustomerPhone,
                request.IdentityNumber),
            cancellationToken);

        return result.Status switch
        {
            BookingCheckoutOutcomeStatus.Success => Ok(ToResponse(result.Result!)),
            BookingCheckoutOutcomeStatus.ValidationError => BadRequest(new { message = result.Error }),
            BookingCheckoutOutcomeStatus.NotFound => NotFound(new { message = result.Error }),
            BookingCheckoutOutcomeStatus.GatewayError => StatusCode(
                StatusCodes.Status502BadGateway,
                new { message = result.Error }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpGet("{bookingId:guid}/status")]
    public async Task<ActionResult<BookingStatusResponse>> GetStatus(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var status = await _bookingService.GetStatusAsync(
            bookingId,
            userId.Value,
            cancellationToken);

        return status is null
            ? NotFound(new { message = "Booking not found." })
            : Ok(ToResponse(status));
    }

    [HttpPost("{bookingId:guid}/repay")]
    public async Task<ActionResult<BookingCheckoutResponse>> Repay(
        Guid bookingId,
        RepayBookingRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var result = await _bookingService.CheckoutAsync(
            new BookingCheckoutCommand(
                userId.Value,
                bookingId,
                "PayOS",
                request.CustomerName,
                request.CustomerEmail,
                request.CustomerPhone,
                request.IdentityNumber),
            cancellationToken);

        return result.Status switch
        {
            BookingCheckoutOutcomeStatus.Success => Ok(ToResponse(result.Result!)),
            BookingCheckoutOutcomeStatus.ValidationError => BadRequest(new { message = result.Error }),
            BookingCheckoutOutcomeStatus.NotFound => NotFound(new { message = result.Error }),
            BookingCheckoutOutcomeStatus.GatewayError => StatusCode(
                StatusCodes.Status502BadGateway,
                new { message = result.Error }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPost("{bookingId:guid}/cancel")]
    public async Task<ActionResult<BookingCancellationResponse>> Cancel(
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var outcome = await _bookingService.CancelAsync(
            bookingId,
            userId.Value,
            cancellationToken);

        return outcome.Status switch
        {
            BookingCancellationOutcomeStatus.Success => Ok(
                ToResponse(outcome.Result!)),
            BookingCancellationOutcomeStatus.NotFound => NotFound(
                new { message = outcome.Error }),
            BookingCancellationOutcomeStatus.Conflict => Conflict(
                new { message = outcome.Error }),
            BookingCancellationOutcomeStatus.GatewayError => StatusCode(
                StatusCodes.Status502BadGateway,
                new { message = outcome.Error }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private Guid? GetUserId()
    {
        var id = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(id, out var userId) ? userId : null;
    }

    private static BookingDraftResponse ToResponse(BookingDraft draft)
    {
        return new BookingDraftResponse(
            draft.Id,
            draft.PropertyId,
            draft.PropertyName,
            draft.RoomId,
            draft.RoomName,
            draft.RoomType,
            draft.CheckIn,
            draft.CheckOut,
            draft.Nights,
            draft.Adults,
            draft.Children,
            draft.TotalGuests,
            draft.RoomQuantity,
            draft.MaxGuests,
            draft.AvailableRooms,
            draft.RoomSubtotal,
            draft.AddOnSubtotal,
            draft.TotalPrice,
            draft.AddOns.Select(ToResponse).ToList(),
            draft.Note,
            draft.ExpiresAt,
            "ProceedToConfirmationPayment");
    }

    private static BookingAddOnResponse ToResponse(BookingAddOn addOn)
    {
        return new BookingAddOnResponse(
            addOn.Code,
            addOn.Name,
            addOn.PricingType,
            addOn.UnitPrice,
            addOn.TotalPrice);
    }

    private static BookingCheckoutResponse ToResponse(BookingCheckoutResult result)
    {
        return new BookingCheckoutResponse(
            result.BookingId,
            result.BookingStatus,
            result.PaymentMethod,
            result.PaymentStatus,
            result.Amount,
            result.TransactionCode,
            result.PaymentLinkId,
            result.CheckoutUrl,
            result.QrCode,
            result.ExpiresAt,
            result.Message);
    }

    private static BookingStatusResponse ToResponse(BookingPaymentStatus status)
    {
        return new BookingStatusResponse(
            status.BookingId,
            status.BookingStatus,
            status.PaymentMethod,
            status.PaymentStatus,
            status.Amount,
            status.TransactionCode,
            status.PaymentLinkId,
            status.CheckoutUrl,
            status.ExpiresAt,
            status.PaidAt,
            status.UpdatedAt);
    }

    private static BookingCancellationResponse ToResponse(
        BookingCancellationResult result)
    {
        return new BookingCancellationResponse(
            result.BookingId,
            result.BookingStatus,
            result.PaymentStatus,
            result.Message);
    }
}
