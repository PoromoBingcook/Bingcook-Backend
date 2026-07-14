using BingCook.Api.Data;
using BingCook.Api.Models;
using BingCook.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BingCook.Api.Tests;

public sealed class BookingServiceTests
{
    [Fact]
    public async Task CreateDraftAsync_ReturnsExistingPendingPaymentForSameStay()
    {
        var existingBookingId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var repository = new FakeBookingRepository
        {
            ExistingPendingPaymentId = existingBookingId
        };
        var service = CreateService(repository, new FakePayOSPaymentGateway());

        var result = await service.CreateDraftAsync(
            new BookingSelectionCommand(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                new DateOnly(2026, 7, 16),
                new DateOnly(2026, 7, 17),
                2,
                0,
                1,
                Array.Empty<string>(),
                null),
            CancellationToken.None);

        Assert.Equal(BookingDraftOutcomeStatus.ExistingPayment, result.Status);
        Assert.Equal(existingBookingId, result.ExistingBookingId);
        Assert.Equal(0, repository.CreateDraftCalls);
    }

    [Fact]
    public async Task CheckoutAsync_ReturnsValidationErrorWhenDraftExpired()
    {
        var repository = new FakeBookingRepository
        {
            CheckoutQuote = CreateCheckoutQuote(
                BookingStatuses.Pending,
                DateTime.UtcNow.AddMinutes(-1))
        };
        var gateway = new FakePayOSPaymentGateway();
        var service = CreateService(repository, gateway);

        var result = await service.CheckoutAsync(
            CreatePayOSCheckoutCommand(),
            CancellationToken.None);

        Assert.Equal(BookingCheckoutOutcomeStatus.ValidationError, result.Status);
        Assert.Equal(0, gateway.CreatePaymentLinkCalls);
    }

    [Fact]
    public async Task CheckoutAsync_ReusesActivePayOSPayment()
    {
        var bookingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var expiresAt = DateTime.UtcNow.AddMinutes(10);
        var repository = new FakeBookingRepository
        {
            CheckoutQuote = CreateCheckoutQuote(
                BookingStatuses.PendingPayment,
                expiresAt,
                bookingId),
            ActivePayment = new ActiveBookingPayment(
                bookingId,
                PaymentStatuses.Pending,
                "PayOS",
                950000m,
                "123456789",
                "paylink-1",
                "https://pay.payos.vn/link",
                "qr",
                expiresAt)
        };
        var gateway = new FakePayOSPaymentGateway();
        var service = CreateService(repository, gateway);

        var result = await service.CheckoutAsync(
            CreatePayOSCheckoutCommand(bookingId),
            CancellationToken.None);

        Assert.Equal(BookingCheckoutOutcomeStatus.Success, result.Status);
        Assert.Equal("123456789", result.Result?.TransactionCode);
        Assert.Equal("paylink-1", result.Result?.PaymentLinkId);
        Assert.Equal(0, gateway.CreatePaymentLinkCalls);
    }

    [Fact]
    public async Task CheckoutAsync_CreatesConfirmedNotificationForPayAtProperty()
    {
        var bookingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var userId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var notificationRepository = new FakeNotificationRepository();
        var repository = new FakeBookingRepository
        {
            CheckoutQuote = CreateCheckoutQuote(
                BookingStatuses.Pending,
                DateTime.UtcNow.AddMinutes(10),
                bookingId)
        };
        var service = CreateService(
            repository,
            new FakePayOSPaymentGateway(),
            notificationRepository);

        var result = await service.CheckoutAsync(
            new BookingCheckoutCommand(
                userId,
                bookingId,
                "PayAtProperty",
                "Jane Cook",
                "jane@example.com",
                "+84901234567",
                null),
            CancellationToken.None);

        Assert.Equal(BookingCheckoutOutcomeStatus.Success, result.Status);
        var notification = Assert.Single(notificationRepository.Created);
        Assert.Equal(userId, notification.UserId);
        Assert.Equal("Booking Confirmed", notification.Title);
        Assert.Contains("BingCook Central Hotel", notification.Message);
    }

    [Fact]
    public async Task CheckoutAsync_CreatesPendingPaymentNotificationForPayOS()
    {
        var bookingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var userId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var notificationRepository = new FakeNotificationRepository();
        var repository = new FakeBookingRepository
        {
            CheckoutQuote = CreateCheckoutQuote(
                BookingStatuses.Pending,
                DateTime.UtcNow.AddMinutes(10),
                bookingId)
        };
        var service = CreateService(
            repository,
            new FakePayOSPaymentGateway(),
            notificationRepository);

        var result = await service.CheckoutAsync(
            CreatePayOSCheckoutCommand(bookingId),
            CancellationToken.None);

        Assert.Equal(BookingCheckoutOutcomeStatus.Success, result.Status);
        var notification = Assert.Single(notificationRepository.Created);
        Assert.Equal(userId, notification.UserId);
        Assert.Equal("Payment Pending", notification.Title);
        Assert.Contains("BingCook Central Hotel", notification.Message);
    }

    [Fact]
    public async Task CheckoutAsync_PassesBookingExpiryToPayOS()
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(10);
        var repository = new FakeBookingRepository
        {
            CheckoutQuote = CreateCheckoutQuote(
                BookingStatuses.Pending,
                expiresAt)
        };
        var gateway = new FakePayOSPaymentGateway();
        var service = CreateService(repository, gateway);

        var result = await service.CheckoutAsync(
            CreatePayOSCheckoutCommand(),
            CancellationToken.None);

        Assert.Equal(BookingCheckoutOutcomeStatus.Success, result.Status);
        Assert.Equal(expiresAt, gateway.LastCreateCommand?.ExpiresAt);
    }

    [Fact]
    public async Task CheckoutAsync_NormalizesDatabaseExpiryToUtcBeforePayOS()
    {
        var expiresAt = DateTime.SpecifyKind(
            DateTime.UtcNow.AddMinutes(10),
            DateTimeKind.Unspecified);
        var repository = new FakeBookingRepository
        {
            CheckoutQuote = CreateCheckoutQuote(
                BookingStatuses.Pending,
                expiresAt)
        };
        var gateway = new FakePayOSPaymentGateway();
        var service = CreateService(repository, gateway);

        var result = await service.CheckoutAsync(
            CreatePayOSCheckoutCommand(),
            CancellationToken.None);

        Assert.Equal(BookingCheckoutOutcomeStatus.Success, result.Status);
        Assert.Equal(DateTimeKind.Utc, gateway.LastCreateCommand?.ExpiresAt.Kind);
        Assert.Equal(expiresAt.Ticks, gateway.LastCreateCommand?.ExpiresAt.Ticks);
    }

    [Fact]
    public async Task UpdatePayOSPaymentAsync_CreatesOneSuccessfulBookingNotification()
    {
        var userId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var notificationRepository = new FakeNotificationRepository();
        var repository = new FakeBookingRepository
        {
            PayOSUpdateResult = new PayOSPaymentUpdateResult(
                userId,
                "BingCook Central Hotel",
                PaymentStatuses.Success,
                BookingStatuses.Paid,
                true)
        };
        var service = CreateService(
            repository,
            new FakePayOSPaymentGateway(),
            notificationRepository);

        var found = await service.UpdatePayOSPaymentAsync(
            new PayOSPaymentUpdateCommand(
                "123456789",
                PaymentStatuses.Success,
                BookingStatuses.Paid),
            CancellationToken.None);

        Assert.True(found);
        var notification = Assert.Single(notificationRepository.Created);
        Assert.Equal(userId, notification.UserId);
        Assert.Equal("Booking Successful", notification.Title);
        Assert.Contains("BingCook Central Hotel", notification.Message);
    }

    [Fact]
    public async Task UpdatePayOSPaymentAsync_DoesNotDuplicateNotificationForNoOp()
    {
        var notificationRepository = new FakeNotificationRepository();
        var repository = new FakeBookingRepository
        {
            PayOSUpdateResult = new PayOSPaymentUpdateResult(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                "BingCook Central Hotel",
                PaymentStatuses.Success,
                BookingStatuses.Paid,
                false)
        };
        var service = CreateService(
            repository,
            new FakePayOSPaymentGateway(),
            notificationRepository);

        var found = await service.UpdatePayOSPaymentAsync(
            new PayOSPaymentUpdateCommand(
                "123456789",
                PaymentStatuses.Success,
                BookingStatuses.Paid),
            CancellationToken.None);

        Assert.True(found);
        Assert.Empty(notificationRepository.Created);
    }

    [Fact]
    public async Task CancelAsync_CancelsPaidBookingWithoutChangingSuccessfulPayment()
    {
        var bookingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var userId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var notificationRepository = new FakeNotificationRepository();
        var repository = new FakeBookingRepository
        {
            CancellationCandidate = new BookingCancellationCandidate(
                bookingId,
                userId,
                "BingCook Central Hotel",
                new DateOnly(2026, 7, 16),
                BookingStatuses.Paid,
                PaymentStatuses.Success),
            CancellationResult = new BookingCancellationResult(
                bookingId,
                BookingStatuses.Cancelled,
                PaymentStatuses.Success,
                "Booking cancelled. Your successful payment is unchanged; contact support about refunds.")
        };
        var service = CreateService(
            repository,
            new FakePayOSPaymentGateway(),
            notificationRepository,
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero)));

        var outcome = await service.CancelAsync(
            bookingId,
            userId,
            CancellationToken.None);

        Assert.Equal(BookingCancellationOutcomeStatus.Success, outcome.Status);
        Assert.Equal(BookingStatuses.Cancelled, outcome.Result?.BookingStatus);
        Assert.Equal(PaymentStatuses.Success, outcome.Result?.PaymentStatus);
        var notification = Assert.Single(notificationRepository.Created);
        Assert.Equal("Booking Cancelled", notification.Title);
    }

    [Fact]
    public async Task CancelAsync_CancelsPendingPaymentTogetherWithBooking()
    {
        var bookingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var userId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var repository = new FakeBookingRepository
        {
            CancellationCandidate = new BookingCancellationCandidate(
                bookingId,
                userId,
                "BingCook Central Hotel",
                new DateOnly(2026, 7, 16),
                BookingStatuses.PendingPayment,
                PaymentStatuses.Pending,
                "123456789",
                new DateTime(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc)),
            CancellationResult = new BookingCancellationResult(
                bookingId,
                BookingStatuses.Cancelled,
                PaymentStatuses.Cancelled,
                "Booking cancelled.")
        };
        var gateway = new FakePayOSPaymentGateway();
        var service = CreateService(
            repository,
            gateway,
            timeProvider: new FixedTimeProvider(
                new DateTimeOffset(2026, 7, 16, 6, 30, 0, TimeSpan.Zero)));

        var outcome = await service.CancelAsync(
            bookingId,
            userId,
            CancellationToken.None);

        Assert.Equal(BookingCancellationOutcomeStatus.Success, outcome.Status);
        Assert.Equal(PaymentStatuses.Cancelled, outcome.Result?.PaymentStatus);
        Assert.Equal(1, gateway.CancelPaymentLinkCalls);
        Assert.Equal(123456789, gateway.LastCancelledOrderCode);
    }

    [Fact]
    public async Task CancelAsync_RefusesPendingCancellationWhenPayOSReportsPaid()
    {
        var bookingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var userId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var repository = new FakeBookingRepository
        {
            CancellationCandidate = new BookingCancellationCandidate(
                bookingId,
                userId,
                "BingCook Central Hotel",
                new DateOnly(2026, 7, 16),
                BookingStatuses.PendingPayment,
                PaymentStatuses.Pending,
                "123456789",
                new DateTime(2026, 7, 16, 8, 0, 0, DateTimeKind.Utc)),
            PayOSUpdateResult = new PayOSPaymentUpdateResult(
                userId,
                "BingCook Central Hotel",
                PaymentStatuses.Success,
                BookingStatuses.Paid,
                true)
        };
        var gateway = new FakePayOSPaymentGateway
        {
            CancellationStatus = "PAID"
        };
        var service = CreateService(
            repository,
            gateway,
            timeProvider: new FixedTimeProvider(
                new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero)));

        var outcome = await service.CancelAsync(
            bookingId,
            userId,
            CancellationToken.None);

        Assert.Equal(BookingCancellationOutcomeStatus.Conflict, outcome.Status);
        Assert.Contains("already been paid", outcome.Error);
        Assert.Equal(1, repository.UpdatePayOSPaymentCalls);
        Assert.Equal(0, repository.CompleteCancellationCalls);
    }

    [Fact]
    public async Task CancelAsync_ReturnsConflictAtOrAfterTwentyFourHourCutoff()
    {
        var bookingId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var userId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var repository = new FakeBookingRepository
        {
            CancellationCandidate = new BookingCancellationCandidate(
                bookingId,
                userId,
                "BingCook Central Hotel",
                new DateOnly(2026, 7, 16),
                BookingStatuses.Paid,
                PaymentStatuses.Success)
        };
        var service = CreateService(
            repository,
            new FakePayOSPaymentGateway(),
            timeProvider: new FixedTimeProvider(
                new DateTimeOffset(2026, 7, 15, 7, 0, 0, TimeSpan.Zero)));

        var outcome = await service.CancelAsync(
            bookingId,
            userId,
            CancellationToken.None);

        Assert.Equal(BookingCancellationOutcomeStatus.Conflict, outcome.Status);
        Assert.Contains("24 hours", outcome.Error);
        Assert.Equal(0, repository.CompleteCancellationCalls);
    }

    [Fact]
    public void BookingStatuses_DoNotAllowPaidToDowngrade()
    {
        Assert.False(BookingStatuses.CanTransition(
            BookingStatuses.Paid,
            BookingStatuses.Cancelled));
        Assert.False(BookingStatuses.CanTransition(
            BookingStatuses.Paid,
            BookingStatuses.Expired));
    }

    private static BookingService CreateService(
        IBookingRepository repository,
        IPayOSPaymentGateway gateway,
        INotificationRepository? notificationRepository = null,
        TimeProvider? timeProvider = null)
    {
        return new BookingService(
            repository,
            gateway,
            notificationRepository ?? new FakeNotificationRepository(),
            Options.Create(new BookingOptions { HoldMinutes = 15 }),
            NullLogger<BookingService>.Instance,
            timeProvider ?? TimeProvider.System);
    }

    private static BookingCheckoutCommand CreatePayOSCheckoutCommand(
        Guid? bookingId = null)
    {
        return new BookingCheckoutCommand(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            bookingId ?? Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "PayOS",
            "Jane Cook",
            "jane@example.com",
            "+84901234567",
            null);
    }

    private static BookingCheckoutQuote CreateCheckoutQuote(
        string status,
        DateTime? expiresAt,
        Guid? bookingId = null)
    {
        return new BookingCheckoutQuote(
            bookingId ?? Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            "BingCook Central Hotel",
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            "Deluxe King Room",
            DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1)),
            DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(2)),
            2,
            1,
            950000m,
            status,
            expiresAt);
    }

    private sealed class FakeBookingRepository : IBookingRepository
    {
        public BookingCheckoutQuote? CheckoutQuote { get; init; }

        public ActiveBookingPayment? ActivePayment { get; init; }

        public PayOSPaymentUpdateResult? PayOSUpdateResult { get; init; }

        public BookingCancellationCandidate? CancellationCandidate { get; init; }

        public BookingCancellationResult? CancellationResult { get; init; }

        public Guid? ExistingPendingPaymentId { get; init; }

        public int CreateDraftCalls { get; private set; }

        public int CompleteCancellationCalls { get; private set; }

        public int UpdatePayOSPaymentCalls { get; private set; }

        public Task<BookingRoomQuote?> GetRoomQuoteAsync(
            Guid propertyId,
            Guid roomId,
            DateOnly checkIn,
            DateOnly checkOut,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<BookingRoomQuote?>(null);
        }

        public Task<Guid> CreateDraftAsync(
            CreateBookingDraftCommand command,
            CancellationToken cancellationToken)
        {
            CreateDraftCalls++;
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<Guid?> FindActivePendingPaymentAsync(
            Guid userId,
            Guid roomId,
            DateOnly checkIn,
            DateOnly checkOut,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ExistingPendingPaymentId);
        }

        public Task<BookingCheckoutQuote?> GetCheckoutQuoteAsync(
            Guid bookingId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(CheckoutQuote);
        }

        public Task<bool> CompleteCheckoutAsync(
            CompleteBookingCheckoutCommand command,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task<ActiveBookingPayment?> GetActivePaymentByBookingIdAsync(
            Guid bookingId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ActivePayment);
        }

        public Task<BookingPaymentStatus?> GetBookingPaymentStatusAsync(
            Guid bookingId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<BookingPaymentStatus?>(null);
        }

        public Task<int> ExpireStaleBookingsAsync(
            DateTime now,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        public Task<PayOSPaymentUpdateResult?> UpdatePayOSPaymentAsync(
            PayOSPaymentUpdateCommand command,
            CancellationToken cancellationToken)
        {
            UpdatePayOSPaymentCalls++;
            return Task.FromResult(PayOSUpdateResult);
        }

        public Task<BookingCancellationCandidate?> GetCancellationCandidateAsync(
            Guid bookingId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(CancellationCandidate);
        }

        public Task<BookingCancellationResult?> CompleteCancellationAsync(
            CompleteBookingCancellationCommand command,
            CancellationToken cancellationToken)
        {
            CompleteCancellationCalls++;
            return Task.FromResult(CancellationResult);
        }
    }

    private sealed class FakePayOSPaymentGateway : IPayOSPaymentGateway
    {
        public int CreatePaymentLinkCalls { get; private set; }
        public CreateOnlinePaymentLinkCommand? LastCreateCommand { get; private set; }
        public int CancelPaymentLinkCalls { get; private set; }
        public long? LastCancelledOrderCode { get; private set; }
        public string CancellationStatus { get; init; } = "CANCELLED";

        public Task<OnlinePaymentLink> CreatePaymentLinkAsync(
            CreateOnlinePaymentLinkCommand command,
            CancellationToken cancellationToken)
        {
            CreatePaymentLinkCalls++;
            LastCreateCommand = command;
            return Task.FromResult(
                new OnlinePaymentLink(
                    123456789,
                    "paylink-new",
                    "https://pay.payos.vn/new",
                    "qr",
                    "PENDING"));
        }

        public Task<OnlinePaymentStatus> GetPaymentLinkAsync(
            long orderCode,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new OnlinePaymentStatus(orderCode, "paylink", "PAID", 950000m));
        }

        public Task<OnlinePaymentStatus> CancelPaymentLinkAsync(
            long orderCode,
            string reason,
            CancellationToken cancellationToken)
        {
            CancelPaymentLinkCalls++;
            LastCancelledOrderCode = orderCode;
            return Task.FromResult(
                new OnlinePaymentStatus(orderCode, "paylink", CancellationStatus, 950000m));
        }

        public bool VerifyWebhookSignature(
            IReadOnlyDictionary<string, string?> data,
            string signature)
        {
            return true;
        }
    }

    private sealed class FakeNotificationRepository : INotificationRepository
    {
        public List<CreateNotificationCommand> Created { get; } = new();

        public Task<IReadOnlyList<UserNotification>> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<UserNotification>>(Array.Empty<UserNotification>());
        }

        public Task CreateAsync(
            CreateNotificationCommand command,
            CancellationToken cancellationToken)
        {
            Created.Add(command);
            return Task.CompletedTask;
        }

        public Task<bool> MarkReadAsync(
            Guid notificationId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task<int> MarkAllReadAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
