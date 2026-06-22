using BingCook.Api.Dtos.Payments;
using BingCook.Api.Models;
using BingCook.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BingCook.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly IPayOSPaymentGateway _payOSPaymentGateway;

    public PaymentsController(
        IBookingService bookingService,
        IPayOSPaymentGateway payOSPaymentGateway)
    {
        _bookingService = bookingService;
        _payOSPaymentGateway = payOSPaymentGateway;
    }

    [HttpPost("payos/webhook")]
    public async Task<IActionResult> PayOSWebhook(
        PayOSWebhookRequest request,
        CancellationToken cancellationToken)
    {
        var data = ToStringDictionary(request.Data);
        if (string.IsNullOrWhiteSpace(request.Signature)
            || !_payOSPaymentGateway.VerifyWebhookSignature(
                data,
                request.Signature))
        {
            return BadRequest(new { message = "Invalid PayOS signature." });
        }

        if (!TryReadOrderCode(data, out var transactionCode))
        {
            return BadRequest(new { message = "Missing PayOS orderCode." });
        }

        var status = ReadStatus(data);
        var paymentStatus = status.Equals("PAID", StringComparison.OrdinalIgnoreCase)
            ? "Success"
            : "Pending";
        var bookingStatus = paymentStatus == "Success" ? "Paid" : "PendingPayment";

        var updated = await _bookingService.UpdatePayOSPaymentAsync(
            new PayOSPaymentUpdateCommand(
                transactionCode,
                paymentStatus,
                bookingStatus),
            cancellationToken);

        return updated
            ? Ok(new { success = true })
            : NotFound(new { message = "PayOS payment not found." });
    }

    [HttpGet("payos/return")]
    public async Task<IActionResult> PayOSReturn(CancellationToken cancellationToken)
    {
        var status = Request.Query["status"].ToString();
        if (!TryReadOrderCode(Request.Query, out var transactionCode)
            || !status.Equals("PAID", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new { message = "PayOS return received.", status });
        }

        await _bookingService.UpdatePayOSPaymentAsync(
            new PayOSPaymentUpdateCommand(
                transactionCode,
                "Success",
                "Paid"),
            cancellationToken);

        return Ok(new { message = "Payment completed.", status = "Paid" });
    }

    [HttpGet("payos/cancel")]
    public async Task<IActionResult> PayOSCancel(CancellationToken cancellationToken)
    {
        if (TryReadOrderCode(Request.Query, out var transactionCode))
        {
            await _bookingService.UpdatePayOSPaymentAsync(
                new PayOSPaymentUpdateCommand(
                    transactionCode,
                    "Cancelled",
                    "Cancelled"),
                cancellationToken);
        }

        return Ok(new { message = "Payment cancelled.", status = "Cancelled" });
    }

    private static bool TryReadOrderCode(
        IReadOnlyDictionary<string, string?> data,
        out string transactionCode)
    {
        transactionCode = string.Empty;
        if (!data.TryGetValue("orderCode", out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        transactionCode = value;
        return true;
    }

    private static bool TryReadOrderCode(
        IQueryCollection query,
        out string transactionCode)
    {
        transactionCode = query["orderCode"].ToString();
        return !string.IsNullOrWhiteSpace(transactionCode);
    }

    private static string ReadStatus(IReadOnlyDictionary<string, string?> data)
    {
        return data.TryGetValue("status", out var status)
            && !string.IsNullOrWhiteSpace(status)
                ? status
                : string.Empty;
    }

    private static Dictionary<string, string?> ToStringDictionary(
        IReadOnlyDictionary<string, System.Text.Json.JsonElement> data)
    {
        return data.ToDictionary(
            item => item.Key,
            item => item.Value.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => item.Value.GetString(),
                System.Text.Json.JsonValueKind.Number => item.Value.GetRawText(),
                System.Text.Json.JsonValueKind.True => "true",
                System.Text.Json.JsonValueKind.False => "false",
                System.Text.Json.JsonValueKind.Null => null,
                _ => item.Value.GetRawText()
            });
    }
}
