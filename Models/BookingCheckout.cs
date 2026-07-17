namespace BingCook.Api.Models;

public sealed record BookingCheckoutCommand(
    Guid UserId,
    Guid BookingId,
    string PaymentMethod,
    string? CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? IdentityNumber);

public sealed record BookingCheckoutQuote(
    Guid BookingId,
    Guid UserId,
    Guid PropertyId,
    string PropertyName,
    Guid RoomId,
    string RoomName,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Guest,
    int RoomQuantity,
    decimal TotalPrice,
    string Status,
    DateTime? ExpiresAt);

public sealed record CompleteBookingCheckoutCommand(
    Guid UserId,
    Guid BookingId,
    string BookingStatus,
    string PaymentMethod,
    string PaymentStatus,
    string? Provider,
    decimal Amount,
    string? TransactionCode,
    string? PaymentLinkId,
    string? CheckoutUrl,
    string? QrCode,
    string? CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? IdentityNumber);

public sealed record ActiveBookingPayment(
    Guid BookingId,
    string PaymentStatus,
    string PaymentMethod,
    decimal Amount,
    string TransactionCode,
    string? PaymentLinkId,
    string? CheckoutUrl,
    string? QrCode,
    DateTime? ExpiresAt);

public sealed record BookingPaymentStatus(
    Guid BookingId,
    string BookingStatus,
    string? PaymentMethod,
    string? PaymentStatus,
    decimal? Amount,
    string? TransactionCode,
    string? PaymentLinkId,
    string? CheckoutUrl,
    DateTime? ExpiresAt,
    DateTime? PaidAt,
    DateTime? UpdatedAt);

public sealed record CreateOnlinePaymentLinkCommand(
    Guid BookingId,
    string PropertyName,
    string RoomName,
    decimal Amount,
    DateTime ExpiresAt);

public sealed record OnlinePaymentLink(
    long OrderCode,
    string PaymentLinkId,
    string CheckoutUrl,
    string? QrCode,
    string Status);

public sealed record OnlinePaymentStatus(
    long OrderCode,
    string? PaymentLinkId,
    string Status,
    decimal? Amount);

public sealed record BookingCheckoutResult(
    Guid BookingId,
    string BookingStatus,
    string PaymentMethod,
    string PaymentStatus,
    decimal Amount,
    string? TransactionCode,
    string? PaymentLinkId,
    string? CheckoutUrl,
    string? QrCode,
    DateTime? ExpiresAt,
    string Message);

public sealed record PayOSPaymentUpdateCommand(
    string TransactionCode,
    string PaymentStatus,
    string BookingStatus);

public sealed record PayOSPaymentUpdateResult(
    Guid UserId,
    string PropertyName,
    string PaymentStatus,
    string BookingStatus,
    bool StateChanged);
