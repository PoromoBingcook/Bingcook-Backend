using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BingCook.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace BingCook.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/bookings")]
public sealed class UserReservationsController : ControllerBase
{
    private readonly SqlConnectionFactory _connectionFactory;

    public UserReservationsController(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserReservationResponse>>> GetMine(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        const string sql = """
            SELECT
                b.Id AS BookingId,
                b.PropertyId,
                p.[Name] AS PropertyName,
                propertyImage.ImageUrl AS PropertyImageUrl,
                p.Latitude,
                p.Longitude,
                b.RoomId,
                r.[Name] AS RoomName,
                roomImage.ImageUrl AS RoomImageUrl,
                b.CheckIn,
                b.CheckOut,
                b.AdultGuest AS Adults,
                b.ChildGuest AS Children,
                b.RoomQuantity,
                b.TotalPrice,
                b.[Status] AS BookingStatus,
                latestPayment.[Status] AS PaymentStatus,
                latestPayment.Method AS PaymentMethod
            FROM dbo.Booking b
            INNER JOIN dbo.Property p ON p.Id = b.PropertyId
            INNER JOIN dbo.Room r ON r.Id = b.RoomId
            OUTER APPLY (
                SELECT TOP (1) ImageUrl
                FROM dbo.PropertyImage
                WHERE PropertyId = p.Id
                ORDER BY Id
            ) propertyImage
            OUTER APPLY (
                SELECT TOP (1) ImageUrl
                FROM dbo.RoomImage
                WHERE RoomId = r.Id
                ORDER BY Id
            ) roomImage
            OUTER APPLY (
                SELECT TOP (1) [Status], Method
                FROM dbo.Payment
                WHERE BookingId = b.Id
                ORDER BY CreatedAt DESC
            ) latestPayment
            WHERE b.UserId = @userId
            ORDER BY b.CheckIn DESC;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId.Value;

        var reservations = new List<UserReservationResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            reservations.Add(new UserReservationResponse(
                reader.GetGuid(reader.GetOrdinal("BookingId")),
                reader.GetGuid(reader.GetOrdinal("PropertyId")),
                reader.GetString(reader.GetOrdinal("PropertyName")),
                ReadNullableString(reader, "PropertyImageUrl"),
                ReadNullableDouble(reader, "Latitude"),
                ReadNullableDouble(reader, "Longitude"),
                reader.GetGuid(reader.GetOrdinal("RoomId")),
                reader.GetString(reader.GetOrdinal("RoomName")),
                ReadNullableString(reader, "RoomImageUrl"),
                DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("CheckIn"))),
                DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("CheckOut"))),
                reader.GetInt32(reader.GetOrdinal("Adults")),
                reader.GetInt32(reader.GetOrdinal("Children")),
                reader.GetInt32(reader.GetOrdinal("RoomQuantity")),
                reader.GetDecimal(reader.GetOrdinal("TotalPrice")),
                reader.GetString(reader.GetOrdinal("BookingStatus")),
                ReadNullableString(reader, "PaymentStatus"),
                ReadNullableString(reader, "PaymentMethod")));
        }

        return Ok(reservations);
    }

    private Guid? GetUserId()
    {
        var id = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var userId) ? userId : null;
    }

    private static string? ReadNullableString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static double? ReadNullableDouble(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToDouble(reader.GetValue(ordinal));
    }
}

public sealed record UserReservationResponse(
    Guid BookingId,
    Guid PropertyId,
    string PropertyName,
    string? PropertyImageUrl,
    double? Latitude,
    double? Longitude,
    Guid RoomId,
    string RoomName,
    string? RoomImageUrl,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Adults,
    int Children,
    int RoomQuantity,
    decimal TotalPrice,
    string BookingStatus,
    string? PaymentStatus,
    string? PaymentMethod);
