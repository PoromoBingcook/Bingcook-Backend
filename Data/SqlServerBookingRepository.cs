using System.Data;
using System.Text.Json;
using BingCook.Api.Models;
using Microsoft.Data.SqlClient;

namespace BingCook.Api.Data;

public sealed class SqlServerBookingRepository : IBookingRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public SqlServerBookingRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<BookingRoomQuote?> GetRoomQuoteAsync(
        Guid propertyId,
        Guid roomId,
        DateOnly checkIn,
        DateOnly checkOut,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                p.Id AS propertyid,
                p.[Name] AS propertyname,
                r.Id AS roomid,
                r.[Name] AS roomname,
                COALESCE(pt.[Name], N'Stay') AS roomtype,
                r.Capacity AS capacity,
                COALESCE(r.TotalRoom, 1) AS totalrooms,
                CASE
                    WHEN COALESCE(r.TotalRoom, 1) - COALESCE(booked.BookedRooms, 0) > 0
                        THEN COALESCE(r.TotalRoom, 1) - COALESCE(booked.BookedRooms, 0)
                    ELSE 0
                END AS availablerooms,
                r.Price AS pricepernight
            FROM dbo.Room r
            INNER JOIN dbo.Property p ON p.Id = r.PropertyId
            LEFT JOIN dbo.PropertyType pt ON pt.Id = p.TypeId
            OUTER APPLY (
                SELECT COALESCE(SUM(COALESCE(b.RoomQuantity, 1)), 0) AS BookedRooms
                FROM dbo.Booking b
                WHERE b.RoomId = r.Id
                  AND b.CheckIn < @checkOut
                  AND b.CheckOut > @checkIn
                  AND (
                      b.[Status] IN (N'Confirmed', N'Paid')
                      OR (
                          b.[Status] IN (N'Pending', N'PendingPayment')
                          AND (b.ExpiresAt IS NULL OR b.ExpiresAt > SYSUTCDATETIME())
                      )
                  )
            ) booked
            WHERE p.Id = @propertyId
              AND r.Id = @roomId
              AND p.[Status] = N'Active';
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@propertyId", SqlDbType.UniqueIdentifier).Value = propertyId;
        command.Parameters.Add("@roomId", SqlDbType.UniqueIdentifier).Value = roomId;
        AddDate(command, "@checkIn", checkIn);
        AddDate(command, "@checkOut", checkOut);

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
            INSERT INTO dbo.Booking (
                UserId,
                PropertyId,
                RoomId,
                CheckIn,
                CheckOut,
                Guest,
                TotalPrice,
                [Status],
                Note,
                RoomQuantity,
                AdultGuest,
                ChildGuest,
                SelectedAddOns,
                ExpiresAt)
            OUTPUT INSERTED.Id
            VALUES (
                @userId,
                @propertyId,
                @roomId,
                @checkIn,
                @checkOut,
                @guest,
                @totalPrice,
                N'Pending',
                @note,
                @roomQuantity,
                @adultGuest,
                @childGuest,
                @selectedAddOns,
                @expiresAt);
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var dbCommand = connection.CreateCommand();
        dbCommand.CommandText = sql;
        dbCommand.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = command.UserId;
        dbCommand.Parameters.Add("@propertyId", SqlDbType.UniqueIdentifier).Value = command.PropertyId;
        dbCommand.Parameters.Add("@roomId", SqlDbType.UniqueIdentifier).Value = command.RoomId;
        AddDate(dbCommand, "@checkIn", command.CheckIn);
        AddDate(dbCommand, "@checkOut", command.CheckOut);
        dbCommand.Parameters.Add("@guest", SqlDbType.Int).Value = command.TotalGuests;
        AddMoney(dbCommand, "@totalPrice", command.TotalPrice);
        AddNullableText(dbCommand, "@note", command.Note);
        dbCommand.Parameters.Add("@roomQuantity", SqlDbType.Int).Value = command.RoomQuantity;
        dbCommand.Parameters.Add("@adultGuest", SqlDbType.Int).Value = command.Adults;
        dbCommand.Parameters.Add("@childGuest", SqlDbType.Int).Value = command.Children;
        AddNullableText(
            dbCommand,
            "@selectedAddOns",
            JsonSerializer.Serialize(command.AddOns));
        AddDateTime(dbCommand, "@expiresAt", command.ExpiresAt);

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
            SELECT TOP (1)
                b.Id AS id,
                b.UserId AS userid,
                b.PropertyId AS propertyid,
                p.[Name] AS propertyname,
                b.RoomId AS roomid,
                r.[Name] AS roomname,
                b.CheckIn AS checkin,
                b.CheckOut AS checkout,
                b.Guest AS guest,
                COALESCE(b.RoomQuantity, 1) AS roomquantity,
                COALESCE(b.TotalPrice, 0) AS totalprice,
                b.[Status] AS status,
                b.ExpiresAt AS expiresat
            FROM dbo.Booking b
            INNER JOIN dbo.Property p ON p.Id = b.PropertyId
            INNER JOIN dbo.Room r ON r.Id = b.RoomId
            WHERE b.Id = @bookingId
              AND b.UserId = @userId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@bookingId", SqlDbType.UniqueIdentifier).Value = bookingId;
        command.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId;

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
            ReadNullableDateTime(reader, "expiresat"));
    }

    public async Task<bool> CompleteCheckoutAsync(
        CompleteBookingCheckoutCommand command,
        CancellationToken cancellationToken)
    {
        const string updateBookingSql = """
            UPDATE dbo.Booking
            SET
                [Status] = @bookingStatus,
                ContactFullName = @customerName,
                ContactEmail = @customerEmail,
                ContactPhone = @customerPhone,
                IdentityNumber = @identityNumber
            WHERE Id = @bookingId
              AND UserId = @userId
              AND [Status] IN (N'Pending', N'PendingPayment')
              AND (ExpiresAt IS NULL OR ExpiresAt > SYSUTCDATETIME())
              AND EXISTS (
                  SELECT 1
                  FROM dbo.Room r
                  OUTER APPLY (
                      SELECT COALESCE(SUM(COALESCE(other.RoomQuantity, 1)), 0) AS BookedRooms
                      FROM dbo.Booking other
                      WHERE other.RoomId = r.Id
                        AND other.Id <> @bookingId
                        AND other.CheckIn < (SELECT CheckOut FROM dbo.Booking WHERE Id = @bookingId)
                        AND other.CheckOut > (SELECT CheckIn FROM dbo.Booking WHERE Id = @bookingId)
                        AND (
                            other.[Status] IN (N'Confirmed', N'Paid')
                            OR (
                                other.[Status] IN (N'Pending', N'PendingPayment')
                                AND (other.ExpiresAt IS NULL OR other.ExpiresAt > SYSUTCDATETIME())
                            )
                        )
                  ) booked
                  WHERE r.Id = dbo.Booking.RoomId
                    AND COALESCE(r.TotalRoom, 1) - COALESCE(booked.BookedRooms, 0) >= dbo.Booking.RoomQuantity
              );
            """;

        const string upsertPaymentSql = """
            IF @provider = N'PayOS'
               AND EXISTS (
                   SELECT 1
                   FROM dbo.Payment
                   WHERE BookingId = @bookingId
                     AND Provider = N'PayOS'
                     AND [Status] = N'Pending'
               )
            BEGIN
                UPDATE dbo.Payment
                SET
                    Method = @paymentMethod,
                    Amount = @amount,
                    [Status] = @paymentStatus,
                    TransactionCode = @transactionCode,
                    PaymentLinkId = @paymentLinkId,
                    CheckoutUrl = @checkoutUrl,
                    QrCode = @qrCode,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE BookingId = @bookingId
                  AND Provider = N'PayOS'
                  AND [Status] = N'Pending';
            END
            ELSE
            BEGIN
                INSERT INTO dbo.Payment (
                    BookingId,
                    Method,
                    Amount,
                    [Status],
                    Provider,
                    TransactionCode,
                    PaymentLinkId,
                    CheckoutUrl,
                    QrCode)
                VALUES (
                    @bookingId,
                    @paymentMethod,
                    @amount,
                    @paymentStatus,
                    @provider,
                    @transactionCode,
                    @paymentLinkId,
                    @checkoutUrl,
                    @qrCode);
            END;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var updateBooking = connection.CreateCommand();
        updateBooking.Transaction = (SqlTransaction)transaction;
        updateBooking.CommandText = updateBookingSql;
        AddCheckoutParameters(updateBooking, command);

        var updatedRows = await updateBooking.ExecuteNonQueryAsync(cancellationToken);
        if (updatedRows == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await using var upsertPayment = connection.CreateCommand();
        upsertPayment.Transaction = (SqlTransaction)transaction;
        upsertPayment.CommandText = upsertPaymentSql;
        AddCheckoutParameters(upsertPayment, command);
        await upsertPayment.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<ActiveBookingPayment?> GetActivePaymentByBookingIdAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                b.Id AS bookingid,
                p.[Status] AS paymentstatus,
                p.Method AS paymentmethod,
                p.Amount AS amount,
                p.TransactionCode AS transactioncode,
                p.PaymentLinkId AS paymentlinkid,
                p.CheckoutUrl AS checkouturl,
                p.QrCode AS qrcode,
                b.ExpiresAt AS expiresat
            FROM dbo.Booking b
            INNER JOIN dbo.Payment p ON p.BookingId = b.Id
            WHERE b.Id = @bookingId
              AND b.UserId = @userId
              AND b.[Status] = N'PendingPayment'
              AND p.Provider = N'PayOS'
              AND p.[Status] = N'Pending'
              AND (b.ExpiresAt IS NULL OR b.ExpiresAt > SYSUTCDATETIME())
            ORDER BY p.CreatedAt DESC;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@bookingId", SqlDbType.UniqueIdentifier).Value = bookingId;
        command.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId;

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
            ReadNullableString(reader, "paymentlinkid"),
            ReadNullableString(reader, "checkouturl"),
            ReadNullableString(reader, "qrcode"),
            ReadNullableDateTime(reader, "expiresat"));
    }

    public async Task<BookingPaymentStatus?> GetBookingPaymentStatusAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                b.Id AS bookingid,
                b.[Status] AS bookingstatus,
                p.Method AS paymentmethod,
                p.[Status] AS paymentstatus,
                p.Amount AS amount,
                p.TransactionCode AS transactioncode,
                p.PaymentLinkId AS paymentlinkid,
                p.CheckoutUrl AS checkouturl,
                b.ExpiresAt AS expiresat,
                p.PaidAt AS paidat,
                p.UpdatedAt AS updatedat
            FROM dbo.Booking b
            OUTER APPLY (
                SELECT TOP (1)
                    Method,
                    [Status],
                    Amount,
                    TransactionCode,
                    PaymentLinkId,
                    CheckoutUrl,
                    PaidAt,
                    UpdatedAt,
                    CreatedAt
                FROM dbo.Payment
                WHERE BookingId = b.Id
                ORDER BY CreatedAt DESC
            ) p
            WHERE b.Id = @bookingId
              AND b.UserId = @userId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@bookingId", SqlDbType.UniqueIdentifier).Value = bookingId;
        command.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BookingPaymentStatus(
            reader.GetGuid(reader.GetOrdinal("bookingid")),
            reader.GetString(reader.GetOrdinal("bookingstatus")),
            ReadNullableString(reader, "paymentmethod"),
            ReadNullableString(reader, "paymentstatus"),
            ReadNullableDecimal(reader, "amount"),
            ReadNullableString(reader, "transactioncode"),
            ReadNullableString(reader, "paymentlinkid"),
            ReadNullableString(reader, "checkouturl"),
            ReadNullableDateTime(reader, "expiresat"),
            ReadNullableDateTime(reader, "paidat"),
            ReadNullableDateTime(reader, "updatedat"));
    }

    public async Task<int> ExpireStaleBookingsAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE p
            SET
                [Status] = N'Expired',
                UpdatedAt = SYSUTCDATETIME()
            FROM dbo.Payment p
            INNER JOIN dbo.Booking b ON b.Id = p.BookingId
            WHERE b.[Status] IN (N'Pending', N'PendingPayment')
              AND b.ExpiresAt IS NOT NULL
              AND b.ExpiresAt <= @now
              AND p.[Status] = N'Pending';

            UPDATE dbo.Booking
            SET [Status] = N'Expired'
            WHERE [Status] IN (N'Pending', N'PendingPayment')
              AND ExpiresAt IS NOT NULL
              AND ExpiresAt <= @now;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddDateTime(command, "@now", now);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<PayOSPaymentUpdateResult?> UpdatePayOSPaymentAsync(
        PayOSPaymentUpdateCommand command,
        CancellationToken cancellationToken)
    {
        const string findBookingSql = """
            SELECT TOP (1)
                p.BookingId,
                p.[Status] AS paymentstatus,
                b.[Status] AS bookingstatus,
                b.UserId AS userid,
                property.[Name] AS propertyname
            FROM dbo.Payment p WITH (UPDLOCK, ROWLOCK)
            INNER JOIN dbo.Booking b ON b.Id = p.BookingId
            INNER JOIN dbo.Property property ON property.Id = b.PropertyId
            WHERE p.TransactionCode = @transactionCode
              AND p.Provider = N'PayOS';
            """;

        const string updatePaymentSql = """
            UPDATE dbo.Payment
            SET
                [Status] = @paymentStatus,
                PaidAt = CASE
                    WHEN @paymentStatus = N'Success' THEN COALESCE(PaidAt, SYSUTCDATETIME())
                    ELSE PaidAt
                END,
                UpdatedAt = SYSUTCDATETIME()
            WHERE TransactionCode = @transactionCode
              AND Provider = N'PayOS';
            """;

        const string updateBookingSql = """
            UPDATE dbo.Booking
            SET [Status] = @bookingStatus
            WHERE Id = @bookingId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var findBooking = connection.CreateCommand();
        findBooking.Transaction = (SqlTransaction)transaction;
        findBooking.CommandText = findBookingSql;
        AddText(findBooking, "@transactionCode", command.TransactionCode, 100);
        await using var reader = await findBooking.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var bookingId = reader.GetGuid(reader.GetOrdinal("BookingId"));
        var currentPaymentStatus = reader.GetString(reader.GetOrdinal("paymentstatus"));
        var currentBookingStatus = reader.GetString(reader.GetOrdinal("bookingstatus"));
        var userId = reader.GetGuid(reader.GetOrdinal("userid"));
        var propertyName = reader.GetString(reader.GetOrdinal("propertyname"));
        await reader.CloseAsync();

        var statusUnchanged = currentPaymentStatus == command.PaymentStatus
            && currentBookingStatus == command.BookingStatus;
        if (statusUnchanged
            || !PaymentStatuses.CanTransition(currentPaymentStatus, command.PaymentStatus)
            || !BookingStatuses.CanTransition(currentBookingStatus, command.BookingStatus))
        {
            await transaction.CommitAsync(cancellationToken);
            return new PayOSPaymentUpdateResult(
                userId,
                propertyName,
                currentPaymentStatus,
                currentBookingStatus,
                false);
        }

        await using var updatePayment = connection.CreateCommand();
        updatePayment.Transaction = (SqlTransaction)transaction;
        updatePayment.CommandText = updatePaymentSql;
        AddText(updatePayment, "@transactionCode", command.TransactionCode, 100);
        AddText(updatePayment, "@paymentStatus", command.PaymentStatus, 20);
        await updatePayment.ExecuteNonQueryAsync(cancellationToken);

        await using var updateBooking = connection.CreateCommand();
        updateBooking.Transaction = (SqlTransaction)transaction;
        updateBooking.CommandText = updateBookingSql;
        updateBooking.Parameters.Add("@bookingId", SqlDbType.UniqueIdentifier).Value = bookingId;
        AddText(updateBooking, "@bookingStatus", command.BookingStatus, 20);
        var updatedRows = await updateBooking.ExecuteNonQueryAsync(cancellationToken);

        if (updatedRows == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await transaction.CommitAsync(cancellationToken);
        return new PayOSPaymentUpdateResult(
            userId,
            propertyName,
            command.PaymentStatus,
            command.BookingStatus,
            true);
    }

    public async Task<BookingCancellationCandidate?> GetCancellationCandidateAsync(
        Guid bookingId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                b.Id AS bookingid,
                b.UserId AS userid,
                property.[Name] AS propertyname,
                b.CheckIn AS checkin,
                b.[Status] AS bookingstatus,
                latestPayment.[Status] AS paymentstatus
            FROM dbo.Booking b
            INNER JOIN dbo.Property property ON property.Id = b.PropertyId
            OUTER APPLY (
                SELECT TOP (1) payment.[Status]
                FROM dbo.Payment payment
                WHERE payment.BookingId = b.Id
                ORDER BY payment.CreatedAt DESC
            ) latestPayment
            WHERE b.Id = @bookingId
              AND b.UserId = @userId;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@bookingId", SqlDbType.UniqueIdentifier).Value = bookingId;
        command.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = userId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new BookingCancellationCandidate(
            reader.GetGuid(reader.GetOrdinal("bookingid")),
            reader.GetGuid(reader.GetOrdinal("userid")),
            reader.GetString(reader.GetOrdinal("propertyname")),
            DateOnly.FromDateTime(reader.GetDateTime(reader.GetOrdinal("checkin"))),
            reader.GetString(reader.GetOrdinal("bookingstatus")),
            ReadNullableString(reader, "paymentstatus"));
    }

    public async Task<BookingCancellationResult?> CompleteCancellationAsync(
        CompleteBookingCancellationCommand command,
        CancellationToken cancellationToken)
    {
        const string updateBookingSql = """
            UPDATE dbo.Booking
            SET [Status] = N'Cancelled'
            WHERE Id = @bookingId
              AND UserId = @userId
              AND [Status] = @expectedBookingStatus;
            """;

        const string updatePaymentSql = """
            UPDATE dbo.Payment
            SET
                [Status] = N'Cancelled',
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = (
                SELECT TOP (1) Id
                FROM dbo.Payment
                WHERE BookingId = @bookingId
                  AND [Status] = N'Pending'
                ORDER BY CreatedAt DESC
            );
            """;

        const string findPaymentSql = """
            SELECT TOP (1) [Status] AS paymentstatus
            FROM dbo.Payment
            WHERE BookingId = @bookingId
            ORDER BY CreatedAt DESC;
            """;

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var updateBooking = connection.CreateCommand();
        updateBooking.Transaction = (SqlTransaction)transaction;
        updateBooking.CommandText = updateBookingSql;
        updateBooking.Parameters.Add("@bookingId", SqlDbType.UniqueIdentifier).Value = command.BookingId;
        updateBooking.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = command.UserId;
        AddText(updateBooking, "@expectedBookingStatus", command.ExpectedBookingStatus, 20);
        if (await updateBooking.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        await using var updatePayment = connection.CreateCommand();
        updatePayment.Transaction = (SqlTransaction)transaction;
        updatePayment.CommandText = updatePaymentSql;
        updatePayment.Parameters.Add("@bookingId", SqlDbType.UniqueIdentifier).Value = command.BookingId;
        await updatePayment.ExecuteNonQueryAsync(cancellationToken);

        await using var findPayment = connection.CreateCommand();
        findPayment.Transaction = (SqlTransaction)transaction;
        findPayment.CommandText = findPaymentSql;
        findPayment.Parameters.Add("@bookingId", SqlDbType.UniqueIdentifier).Value = command.BookingId;
        var paymentStatus = (string?)await findPayment.ExecuteScalarAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        var message = paymentStatus == PaymentStatuses.Success
            ? "Booking cancelled. Your successful payment is unchanged; contact support about refunds."
            : "Booking cancelled.";
        return new BookingCancellationResult(
            command.BookingId,
            BookingStatuses.Cancelled,
            paymentStatus,
            message);
    }

    private static void AddCheckoutParameters(
        SqlCommand dbCommand,
        CompleteBookingCheckoutCommand command)
    {
        dbCommand.Parameters.Add("@bookingId", SqlDbType.UniqueIdentifier).Value = command.BookingId;
        dbCommand.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = command.UserId;
        AddText(dbCommand, "@bookingStatus", command.BookingStatus, 20);
        AddText(dbCommand, "@paymentMethod", command.PaymentMethod, 50);
        AddText(dbCommand, "@paymentStatus", command.PaymentStatus, 20);
        AddNullableText(dbCommand, "@provider", command.Provider, 50);
        AddMoney(dbCommand, "@amount", command.Amount);
        AddNullableText(dbCommand, "@transactionCode", command.TransactionCode, 100);
        AddNullableText(dbCommand, "@paymentLinkId", command.PaymentLinkId, 100);
        AddNullableText(dbCommand, "@checkoutUrl", command.CheckoutUrl);
        AddNullableText(dbCommand, "@qrCode", command.QrCode);
        AddNullableText(dbCommand, "@customerName", command.CustomerName, 100);
        AddNullableText(dbCommand, "@customerEmail", command.CustomerEmail, 100);
        AddNullableText(dbCommand, "@customerPhone", command.CustomerPhone, 20);
        AddNullableText(dbCommand, "@identityNumber", command.IdentityNumber, 50);
    }

    private static void AddMoney(SqlCommand command, string name, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 12;
        parameter.Scale = 2;
        parameter.Value = value;
    }

    private static void AddDate(SqlCommand command, string name, DateOnly value)
    {
        command.Parameters.Add(name, SqlDbType.Date).Value =
            value.ToDateTime(TimeOnly.MinValue);
    }

    private static void AddDateTime(SqlCommand command, string name, DateTime value)
    {
        command.Parameters.Add(name, SqlDbType.DateTime2).Value = value;
    }

    private static string? ReadNullableString(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTime? ReadNullableDateTime(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private static decimal? ReadNullableDecimal(SqlDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);
    }

    private static void AddNullableText(
        SqlCommand command,
        string name,
        string? value,
        int size = -1)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.NVarChar, size);
        parameter.Value = value is null ? DBNull.Value : value;
    }

    private static void AddText(
        SqlCommand command,
        string name,
        string value,
        int size = -1)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.NVarChar, size);
        parameter.Value = value;
    }
}

