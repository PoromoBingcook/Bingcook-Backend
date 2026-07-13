using BingCook.Api.Models;
using Npgsql;
using NpgsqlTypes;

namespace BingCook.Api.Data;

public sealed class PostgresBookingRepository : IBookingRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresBookingRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<BookingRoomQuote?> GetRoomQuoteAsync(
        Guid propertyId,
        Guid roomId,
        DateOnly checkIn,
        DateOnly checkOut,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                p.id AS propertyid,
                p.name AS propertyname,
                r.id AS roomid,
                r.name AS roomname,
                COALESCE(pt.name, 'Stay') AS roomtype,
                r.capacity,
                COALESCE(r.totalroom, 1) AS totalrooms,
                GREATEST(
                    COALESCE(r.totalroom, 1) - COALESCE(booked.bookedrooms, 0),
                    0) AS availablerooms,
                r.price AS pricepernight
            FROM room r
            INNER JOIN property p ON p.id = r.propertyid
            LEFT JOIN propertytype pt ON pt.id = p.typeid
            LEFT JOIN LATERAL (
                SELECT COALESCE(SUM(COALESCE(b.roomquantity, 1)), 0)::integer AS bookedrooms
                FROM booking b
                WHERE b.roomid = r.id
                  AND b.checkin < @checkOut
                  AND b.checkout > @checkIn
                  AND b.status::text NOT IN ('Cancelled', 'Canceled')
            ) booked ON TRUE
            WHERE p.id = @propertyId
              AND r.id = @roomId
              AND p.status::text = 'Active'
            LIMIT 1;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("propertyId", propertyId);
        command.Parameters.AddWithValue("roomId", roomId);
        AddDate(command, "checkIn", checkIn);
        AddDate(command, "checkOut", checkOut);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BookingRoomQuote(
            reader.GetGuid(reader.GetOrdinal("propertyid")),
            reader.GetString(reader.GetOrdinal("propertyname")),
            reader.GetGuid(reader.GetOrdinal("roomid")),
            reader.GetString(reader.GetOrdinal("roomname")),
            reader.GetString(reader.GetOrdinal("roomtype")),
            reader.GetInt32(reader.GetOrdinal("capacity")),
            reader.GetInt32(reader.GetOrdinal("totalrooms")),
            reader.GetInt32(reader.GetOrdinal("availablerooms")),
            reader.GetDecimal(reader.GetOrdinal("pricepernight")));
    }

    public async Task<Guid> CreateDraftAsync(
        CreateBookingDraftCommand command,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO booking (
                userid,
                propertyid,
                roomid,
                checkin,
                checkout,
                guest,
                totalprice,
                status,
                note,
                roomquantity,
                adultguest,
                childguest,
                selectedaddons,
                expiresat)
            VALUES (
                @userId,
                @propertyId,
                @roomId,
                @checkIn,
                @checkOut,
                @guest,
                @totalPrice,
                'Pending',
                @note,
                @roomQuantity,
                @adultGuest,
                @childGuest,
                @selectedAddOns,
                @expiresAt)
            RETURNING id;
            """;

        await using var dbCommand = _dataSource.CreateCommand(sql);
        dbCommand.Parameters.AddWithValue("userId", command.UserId);
        dbCommand.Parameters.AddWithValue("propertyId", command.PropertyId);
        dbCommand.Parameters.AddWithValue("roomId", command.RoomId);
        AddDate(dbCommand, "checkIn", command.CheckIn);
        AddDate(dbCommand, "checkOut", command.CheckOut);
        dbCommand.Parameters.AddWithValue("guest", command.TotalGuests);
        dbCommand.Parameters.AddWithValue("totalPrice", command.TotalPrice);
        AddNullableText(dbCommand, "note", command.Note);
        dbCommand.Parameters.AddWithValue("roomQuantity", command.RoomQuantity);
        dbCommand.Parameters.AddWithValue("adultGuest", command.Adults);
        dbCommand.Parameters.AddWithValue("childGuest", command.Children);

        var addOnsParameter = dbCommand.Parameters.Add(
            "selectedAddOns",
            NpgsqlDbType.Array | NpgsqlDbType.Text);
        addOnsParameter.Value = command.AddOns.ToArray();
        dbCommand.Parameters.AddWithValue("expiresAt", command.ExpiresAt);

        var result = await dbCommand.ExecuteScalarAsync(cancellationToken);
        return result is Guid id
            ? id
            : throw new InvalidOperationException("Booking creation did not return an id.");
    }

    public async Task<BookingCheckoutQuote?> GetCheckoutQuoteAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                b.id,
                b.userid,
                b.propertyid,
                p.name AS propertyname,
                b.roomid,
                r.name AS roomname,
                b.checkin,
                b.checkout,
                b.guest,
                COALESCE(b.roomquantity, 1) AS roomquantity,
                COALESCE(b.totalprice, 0) AS totalprice,
                b.status::text AS status,
                b.expiresat
            FROM booking b
            INNER JOIN property p ON p.id = b.propertyid
            INNER JOIN room r ON r.id = b.roomid
            WHERE b.id = @bookingId
              AND b.userid = @userId
            LIMIT 1;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("bookingId", bookingId);
        command.Parameters.AddWithValue("userId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BookingCheckoutQuote(
            reader.GetGuid(reader.GetOrdinal("id")),
            reader.GetGuid(reader.GetOrdinal("userid")),
            reader.GetGuid(reader.GetOrdinal("propertyid")),
            reader.GetString(reader.GetOrdinal("propertyname")),
            reader.GetGuid(reader.GetOrdinal("roomid")),
            reader.GetString(reader.GetOrdinal("roomname")),
            DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("checkin"))),
            DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("checkout"))),
            reader.GetInt32(reader.GetOrdinal("guest")),
            reader.GetInt32(reader.GetOrdinal("roomquantity")),
            reader.GetDecimal(reader.GetOrdinal("totalprice")),
            reader.GetString(reader.GetOrdinal("status")),
            reader.IsDBNull(reader.GetOrdinal("expiresat"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("expiresat")));
    }

    public async Task<bool> CompleteCheckoutAsync(
        CompleteBookingCheckoutCommand command,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH updated_booking AS (
                UPDATE booking
                SET
                    status = @bookingStatus,
                    contactfullname = @customerName,
                    contactemail = @customerEmail,
                    contactphone = @customerPhone,
                    identitynumber = @identityNumber
                WHERE id = @bookingId
                  AND userid = @userId
                  AND status::text IN ('Pending', 'PendingPayment')
                RETURNING id
            ),
            inserted_payment AS (
                INSERT INTO payment (
                    bookingid,
                    method,
                    amount,
                    status,
                    provider,
                    transactioncode,
                    paymentlinkid,
                    checkouturl,
                    qrcode)
                SELECT
                    id,
                    @paymentMethod,
                    @amount,
                    @paymentStatus,
                    @provider,
                    @transactionCode,
                    @paymentLinkId,
                    @checkoutUrl,
                    @qrCode
                FROM updated_booking
                RETURNING id
            )
            SELECT EXISTS (SELECT 1 FROM inserted_payment);
            """;

        await using var dbCommand = _dataSource.CreateCommand(sql);
        dbCommand.Parameters.AddWithValue("bookingId", command.BookingId);
        dbCommand.Parameters.AddWithValue("userId", command.UserId);
        dbCommand.Parameters.AddWithValue("bookingStatus", command.BookingStatus);
        dbCommand.Parameters.AddWithValue("paymentMethod", command.PaymentMethod);
        dbCommand.Parameters.AddWithValue("paymentStatus", command.PaymentStatus);
        AddNullableText(dbCommand, "provider", command.Provider);
        dbCommand.Parameters.AddWithValue("amount", command.Amount);
        AddNullableText(dbCommand, "transactionCode", command.TransactionCode);
        AddNullableText(dbCommand, "paymentLinkId", command.PaymentLinkId);
        AddNullableText(dbCommand, "checkoutUrl", command.CheckoutUrl);
        AddNullableText(dbCommand, "qrCode", command.QrCode);
        AddNullableText(dbCommand, "customerName", command.CustomerName);
        AddNullableText(dbCommand, "customerEmail", command.CustomerEmail);
        AddNullableText(dbCommand, "customerPhone", command.CustomerPhone);
        AddNullableText(dbCommand, "identityNumber", command.IdentityNumber);

        var result = await dbCommand.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    public async Task<ActiveBookingPayment?> GetActivePaymentByBookingIdAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                b.id AS bookingid,
                p.status::text AS paymentstatus,
                p.method AS paymentmethod,
                p.amount,
                p.transactioncode,
                p.paymentlinkid,
                p.checkouturl,
                p.qrcode,
                b.expiresat
            FROM booking b
            INNER JOIN payment p ON p.bookingid = b.id
            WHERE b.id = @bookingId
              AND b.userid = @userId
              AND b.status::text = 'PendingPayment'
              AND p.provider = 'PayOS'
              AND p.status::text = 'Pending'
              AND (b.expiresat IS NULL OR b.expiresat > now())
            ORDER BY p.createdat DESC
            LIMIT 1;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("bookingId", bookingId);
        command.Parameters.AddWithValue("userId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ActiveBookingPayment(
            reader.GetGuid(reader.GetOrdinal("bookingid")),
            reader.GetString(reader.GetOrdinal("paymentstatus")),
            reader.GetString(reader.GetOrdinal("paymentmethod")),
            reader.GetDecimal(reader.GetOrdinal("amount")),
            reader.GetString(reader.GetOrdinal("transactioncode")),
            reader.IsDBNull(reader.GetOrdinal("paymentlinkid"))
                ? null
                : reader.GetString(reader.GetOrdinal("paymentlinkid")),
            reader.IsDBNull(reader.GetOrdinal("checkouturl"))
                ? null
                : reader.GetString(reader.GetOrdinal("checkouturl")),
            reader.IsDBNull(reader.GetOrdinal("qrcode"))
                ? null
                : reader.GetString(reader.GetOrdinal("qrcode")),
            reader.IsDBNull(reader.GetOrdinal("expiresat"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("expiresat")));
    }

    public async Task<BookingPaymentStatus?> GetBookingPaymentStatusAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                b.id AS bookingid,
                b.status::text AS bookingstatus,
                p.method AS paymentmethod,
                p.status::text AS paymentstatus,
                p.amount,
                p.transactioncode,
                p.paymentlinkid,
                p.checkouturl,
                b.expiresat,
                p.paidat,
                p.updatedat
            FROM booking b
            LEFT JOIN LATERAL (
                SELECT *
                FROM payment
                WHERE bookingid = b.id
                ORDER BY createdat DESC
                LIMIT 1
            ) p ON TRUE
            WHERE b.id = @bookingId
              AND b.userid = @userId
            LIMIT 1;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("bookingId", bookingId);
        command.Parameters.AddWithValue("userId", userId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BookingPaymentStatus(
            reader.GetGuid(reader.GetOrdinal("bookingid")),
            reader.GetString(reader.GetOrdinal("bookingstatus")),
            reader.IsDBNull(reader.GetOrdinal("paymentmethod"))
                ? null
                : reader.GetString(reader.GetOrdinal("paymentmethod")),
            reader.IsDBNull(reader.GetOrdinal("paymentstatus"))
                ? null
                : reader.GetString(reader.GetOrdinal("paymentstatus")),
            reader.IsDBNull(reader.GetOrdinal("amount"))
                ? null
                : reader.GetDecimal(reader.GetOrdinal("amount")),
            reader.IsDBNull(reader.GetOrdinal("transactioncode"))
                ? null
                : reader.GetString(reader.GetOrdinal("transactioncode")),
            reader.IsDBNull(reader.GetOrdinal("paymentlinkid"))
                ? null
                : reader.GetString(reader.GetOrdinal("paymentlinkid")),
            reader.IsDBNull(reader.GetOrdinal("checkouturl"))
                ? null
                : reader.GetString(reader.GetOrdinal("checkouturl")),
            reader.IsDBNull(reader.GetOrdinal("expiresat"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("expiresat")),
            reader.IsDBNull(reader.GetOrdinal("paidat"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("paidat")),
            reader.IsDBNull(reader.GetOrdinal("updatedat"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("updatedat")));
    }

    public async Task<int> ExpireStaleBookingsAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH expired_payments AS (
                UPDATE payment p
                SET status = 'Expired',
                    updatedat = now()
                FROM booking b
                WHERE b.id = p.bookingid
                  AND b.status::text IN ('Pending', 'PendingPayment')
                  AND b.expiresat IS NOT NULL
                  AND b.expiresat <= @now
                  AND p.status::text = 'Pending'
                RETURNING p.id
            ),
            expired_bookings AS (
                UPDATE booking
                SET status = 'Expired'
                WHERE status::text IN ('Pending', 'PendingPayment')
                  AND expiresat IS NOT NULL
                  AND expiresat <= @now
                RETURNING id
            )
            SELECT COUNT(*) FROM expired_bookings;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("now", now);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    public async Task<bool> UpdatePayOSPaymentAsync(
        PayOSPaymentUpdateCommand command,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH updated_payment AS (
                UPDATE payment
                SET
                    status = @paymentStatus,
                    paidat = CASE
                        WHEN @paymentStatus = 'Success' THEN COALESCE(paidat, now())
                        ELSE paidat
                    END,
                    updatedat = now()
                WHERE transactioncode = @transactionCode
                  AND provider = 'PayOS'
                RETURNING bookingid
            ),
            updated_booking AS (
                UPDATE booking b
                SET status = @bookingStatus
                FROM updated_payment p
                WHERE b.id = p.bookingid
                RETURNING b.id
            )
            SELECT EXISTS (SELECT 1 FROM updated_booking);
            """;

        await using var dbCommand = _dataSource.CreateCommand(sql);
        dbCommand.Parameters.AddWithValue("transactionCode", command.TransactionCode);
        dbCommand.Parameters.AddWithValue("paymentStatus", command.PaymentStatus);
        dbCommand.Parameters.AddWithValue("bookingStatus", command.BookingStatus);

        var result = await dbCommand.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    private static void AddDate(NpgsqlCommand command, string name, DateOnly value)
    {
        command.Parameters.AddWithValue(
            name,
            NpgsqlDbType.Date,
            value.ToDateTime(TimeOnly.MinValue));
    }

    private static void AddNullableText(
        NpgsqlCommand command,
        string name,
        string? value)
    {
        var parameter = command.Parameters.Add(name, NpgsqlDbType.Text);
        parameter.Value = value is null ? DBNull.Value : value;
    }
}
