namespace BingCook.Api.Models;

public static class BookingStatuses
{
    public const string Pending = "Pending";
    public const string PendingPayment = "PendingPayment";
    public const string Confirmed = "Confirmed";
    public const string Paid = "Paid";
    public const string Cancelled = "Cancelled";
    public const string Expired = "Expired";

    public static bool IsActiveHold(string status)
    {
        return status is Pending or PendingPayment or Confirmed or Paid;
    }

    public static bool CanTransition(string currentStatus, string nextStatus)
    {
        if (currentStatus == nextStatus)
        {
            return true;
        }

        return currentStatus switch
        {
            Pending => nextStatus is PendingPayment or Confirmed or Cancelled or Expired,
            PendingPayment => nextStatus is Paid or Cancelled or Expired,
            Confirmed => nextStatus is Cancelled,
            Paid => false,
            Cancelled => false,
            Expired => false,
            _ => false
        };
    }
}

public static class PaymentStatuses
{
    public const string Pending = "Pending";
    public const string Success = "Success";
    public const string Cancelled = "Cancelled";
    public const string Expired = "Expired";
    public const string Failed = "Failed";

    public static bool CanTransition(string currentStatus, string nextStatus)
    {
        if (currentStatus == nextStatus)
        {
            return true;
        }

        return currentStatus switch
        {
            Pending => nextStatus is Success or Cancelled or Expired or Failed,
            Success => false,
            Cancelled => false,
            Expired => false,
            Failed => false,
            _ => false
        };
    }
}
