using BingCook.Api.Models;
using Xunit;

namespace BingCook.Api.Tests;

public sealed class BookingStatusTests
{
    [Fact]
    public void PendingDraftIsNotVisibleInReservations()
    {
        Assert.False(BookingStatuses.IsVisibleReservation(BookingStatuses.Pending));
    }

    [Theory]
    [InlineData(BookingStatuses.PendingPayment)]
    [InlineData(BookingStatuses.Confirmed)]
    [InlineData(BookingStatuses.Paid)]
    [InlineData(BookingStatuses.Cancelled)]
    [InlineData(BookingStatuses.Expired)]
    public void PostCheckoutAndHistoricalStatusesAreVisibleInReservations(string status)
    {
        Assert.True(BookingStatuses.IsVisibleReservation(status));
    }

    [Fact]
    public void ReservationContractIncludesPayOSResumeFields()
    {
        var properties = typeof(Controllers.UserReservationResponse)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet();

        Assert.Contains("TransactionCode", properties);
        Assert.Contains("CheckoutUrl", properties);
        Assert.Contains("ExpiresAt", properties);
    }
}
