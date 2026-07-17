-- =========================
-- Booking App Database
-- Microsoft SQL Server Schema
-- Updated for BingCook backend booking + PayOS flow
-- =========================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =========================
-- Tables
-- =========================

IF OBJECT_ID(N'dbo.[User]', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.[User] (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_User PRIMARY KEY DEFAULT NEWID(),
        FullName NVARCHAR(100) NOT NULL,
        Email NVARCHAR(100) NULL,
        Phone NVARCHAR(20) NULL,
        [Password] NVARCHAR(MAX) NULL,
        [Role] NVARCHAR(20) NOT NULL CONSTRAINT DF_User_Role DEFAULT N'Customer',
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_User_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_User_Email UNIQUE (Email),
        CONSTRAINT CK_User_Role CHECK ([Role] IN (N'Customer', N'Host', N'Admin'))
    );
END;
GO

IF OBJECT_ID(N'dbo.PropertyType', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PropertyType (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PropertyType PRIMARY KEY DEFAULT NEWID(),
        [Name] NVARCHAR(50) NOT NULL,
        CONSTRAINT UQ_PropertyType_Name UNIQUE ([Name])
    );
END;
GO

IF OBJECT_ID(N'dbo.Property', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Property (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Property PRIMARY KEY DEFAULT NEWID(),
        HostId UNIQUEIDENTIFIER NULL,
        TypeId UNIQUEIDENTIFIER NULL,
        [Name] NVARCHAR(150) NOT NULL,
        [Description] NVARCHAR(MAX) NULL,
        [Address] NVARCHAR(MAX) NULL,
        City NVARCHAR(100) NULL,
        Latitude DECIMAL(10,7) NULL,
        Longitude DECIMAL(10,7) NULL,
        Amenities NVARCHAR(MAX) NOT NULL CONSTRAINT DF_Property_Amenities DEFAULT N'[]',
        PricePerNight DECIMAL(12,2) NULL,
        Rating DECIMAL(2,1) NOT NULL CONSTRAINT DF_Property_Rating DEFAULT 0,
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT DF_Property_Status DEFAULT N'Active',
        HasWifi BIT NOT NULL CONSTRAINT DF_Property_HasWifi DEFAULT 1,
        HasPool BIT NOT NULL CONSTRAINT DF_Property_HasPool DEFAULT 0,
        HasParking BIT NOT NULL CONSTRAINT DF_Property_HasParking DEFAULT 1,
        HasAC BIT NOT NULL CONSTRAINT DF_Property_HasAC DEFAULT 1,
        HasBreakfast BIT NOT NULL CONSTRAINT DF_Property_HasBreakfast DEFAULT 0,
        IsPetAllowed BIT NOT NULL CONSTRAINT DF_Property_IsPetAllowed DEFAULT 0,
        IsSelfCheckIn BIT NOT NULL CONSTRAINT DF_Property_IsSelfCheckIn DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Property_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Property_User FOREIGN KEY (HostId) REFERENCES dbo.[User](Id),
        CONSTRAINT FK_Property_PropertyType FOREIGN KEY (TypeId) REFERENCES dbo.PropertyType(Id),
        CONSTRAINT CK_Property_Status CHECK ([Status] IN (N'Active', N'Inactive', N'Suspended')),
        CONSTRAINT CK_Property_Amenities_IsJson CHECK (ISJSON(Amenities) = 1)
    );
END;
GO

IF OBJECT_ID(N'dbo.PropertyImage', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PropertyImage (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PropertyImage PRIMARY KEY DEFAULT NEWID(),
        PropertyId UNIQUEIDENTIFIER NULL,
        ImageUrl NVARCHAR(MAX) NULL,
        CONSTRAINT FK_PropertyImage_Property FOREIGN KEY (PropertyId) REFERENCES dbo.Property(Id) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'dbo.SavedProperty', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SavedProperty (
        UserId UNIQUEIDENTIFIER NOT NULL,
        PropertyId UNIQUEIDENTIFIER NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_SavedProperty_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_SavedProperty PRIMARY KEY (UserId, PropertyId),
        CONSTRAINT FK_SavedProperty_User FOREIGN KEY (UserId) REFERENCES dbo.[User](Id) ON DELETE CASCADE,
        CONSTRAINT FK_SavedProperty_Property FOREIGN KEY (PropertyId) REFERENCES dbo.Property(Id) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_SavedProperty_User_CreatedAt'
      AND object_id = OBJECT_ID(N'dbo.SavedProperty')
)
BEGIN
    CREATE INDEX IX_SavedProperty_User_CreatedAt
        ON dbo.SavedProperty (UserId, CreatedAt DESC);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_SavedProperty_Property'
      AND object_id = OBJECT_ID(N'dbo.SavedProperty')
)
BEGIN
    CREATE INDEX IX_SavedProperty_Property
        ON dbo.SavedProperty (PropertyId);
END;
GO

IF OBJECT_ID(N'dbo.Room', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Room (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Room PRIMARY KEY DEFAULT NEWID(),
        PropertyId UNIQUEIDENTIFIER NULL,
        [Name] NVARCHAR(100) NOT NULL,
        Price DECIMAL(12,2) NOT NULL,
        Capacity INT NOT NULL CONSTRAINT DF_Room_Capacity DEFAULT 1,
        TotalRoom INT NOT NULL CONSTRAINT DF_Room_TotalRoom DEFAULT 1,
        AvailableRoom INT NOT NULL CONSTRAINT DF_Room_AvailableRoom DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Room_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Room_Property FOREIGN KEY (PropertyId) REFERENCES dbo.Property(Id) ON DELETE CASCADE,
        CONSTRAINT CK_Room_Capacity CHECK (Capacity > 0),
        CONSTRAINT CK_Room_TotalRoom CHECK (TotalRoom > 0),
        CONSTRAINT CK_Room_AvailableRoom CHECK (AvailableRoom >= 0)
    );
END;
GO

IF OBJECT_ID(N'dbo.RoomImage', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RoomImage (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_RoomImage PRIMARY KEY DEFAULT NEWID(),
        RoomId UNIQUEIDENTIFIER NULL,
        ImageUrl NVARCHAR(MAX) NULL,
        CONSTRAINT FK_RoomImage_Room FOREIGN KEY (RoomId) REFERENCES dbo.Room(Id) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'dbo.Booking', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Booking (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Booking PRIMARY KEY DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NULL,
        PropertyId UNIQUEIDENTIFIER NULL,
        RoomId UNIQUEIDENTIFIER NULL,
        CheckIn DATE NOT NULL,
        CheckOut DATE NOT NULL,
        Guest INT NOT NULL CONSTRAINT DF_Booking_Guest DEFAULT 1,
        TotalPrice DECIMAL(12,2) NULL,
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT DF_Booking_Status DEFAULT N'Pending',
        Note NVARCHAR(MAX) NULL,
        RoomQuantity INT NOT NULL CONSTRAINT DF_Booking_RoomQuantity DEFAULT 1,
        AdultGuest INT NOT NULL CONSTRAINT DF_Booking_AdultGuest DEFAULT 1,
        ChildGuest INT NOT NULL CONSTRAINT DF_Booking_ChildGuest DEFAULT 0,
        SelectedAddOns NVARCHAR(MAX) NOT NULL CONSTRAINT DF_Booking_SelectedAddOns DEFAULT N'[]',
        ContactFullName NVARCHAR(100) NULL,
        ContactEmail NVARCHAR(100) NULL,
        ContactPhone NVARCHAR(20) NULL,
        IdentityNumber NVARCHAR(50) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Booking_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Booking_User FOREIGN KEY (UserId) REFERENCES dbo.[User](Id),
        CONSTRAINT FK_Booking_Property FOREIGN KEY (PropertyId) REFERENCES dbo.Property(Id),
        CONSTRAINT FK_Booking_Room FOREIGN KEY (RoomId) REFERENCES dbo.Room(Id),
        CONSTRAINT CK_Booking_Dates CHECK (CheckOut > CheckIn),
        CONSTRAINT CK_Booking_RoomQuantity CHECK (RoomQuantity > 0),
        CONSTRAINT CK_Booking_GuestCounts CHECK (AdultGuest >= 0 AND ChildGuest >= 0),
        CONSTRAINT CK_Booking_SelectedAddOns_IsJson CHECK (ISJSON(SelectedAddOns) = 1)
    );
END;
GO

IF OBJECT_ID(N'dbo.Payment', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Payment (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Payment PRIMARY KEY DEFAULT NEWID(),
        BookingId UNIQUEIDENTIFIER NULL,
        Method NVARCHAR(50) NULL,
        Amount DECIMAL(12,2) NULL,
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT DF_Payment_Status DEFAULT N'Pending',
        Provider NVARCHAR(50) NULL,
        TransactionCode NVARCHAR(100) NULL,
        CheckoutUrl NVARCHAR(MAX) NULL,
        QrCode NVARCHAR(MAX) NULL,
        PaidAt DATETIME2 NULL,
        UpdatedAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Payment_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Payment_Booking FOREIGN KEY (BookingId) REFERENCES dbo.Booking(Id) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Payment_TransactionCode'
      AND object_id = OBJECT_ID(N'dbo.Payment')
)
BEGIN
    CREATE UNIQUE INDEX IX_Payment_TransactionCode
        ON dbo.Payment(TransactionCode)
        WHERE TransactionCode IS NOT NULL;
END;
GO

IF OBJECT_ID(N'dbo.Review', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Review (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Review PRIMARY KEY DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NULL,
        PropertyId UNIQUEIDENTIFIER NULL,
        Rating INT NULL,
        Comment NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Review_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Review_User FOREIGN KEY (UserId) REFERENCES dbo.[User](Id),
        CONSTRAINT FK_Review_Property FOREIGN KEY (PropertyId) REFERENCES dbo.Property(Id) ON DELETE CASCADE,
        CONSTRAINT CK_Review_Rating CHECK (Rating BETWEEN 1 AND 5)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_Review_UserId_PropertyId'
      AND object_id = OBJECT_ID(N'dbo.Review')
)
BEGIN
    CREATE UNIQUE INDEX UX_Review_UserId_PropertyId
        ON dbo.Review(UserId, PropertyId)
        WHERE UserId IS NOT NULL AND PropertyId IS NOT NULL;
END;
GO

IF OBJECT_ID(N'dbo.Notification', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Notification (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Notification PRIMARY KEY DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NULL,
        Title NVARCHAR(200) NULL,
        [Message] NVARCHAR(MAX) NULL,
        IsRead BIT NOT NULL CONSTRAINT DF_Notification_IsRead DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Notification_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Notification_User FOREIGN KEY (UserId) REFERENCES dbo.[User](Id) ON DELETE CASCADE
    );
END;
GO

IF OBJECT_ID(N'dbo.Chat', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Chat (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Chat PRIMARY KEY DEFAULT NEWID(),
        SenderId UNIQUEIDENTIFIER NULL,
        ReceiverId UNIQUEIDENTIFIER NULL,
        [Message] NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Chat_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_Chat_Sender FOREIGN KEY (SenderId) REFERENCES dbo.[User](Id),
        CONSTRAINT FK_Chat_Receiver FOREIGN KEY (ReceiverId) REFERENCES dbo.[User](Id)
    );
END;
GO

-- =========================
-- Idempotent schema patch for older SQL Server DBs
-- =========================

IF COL_LENGTH('dbo.Property', 'HasWifi') IS NULL ALTER TABLE dbo.Property ADD HasWifi BIT NOT NULL CONSTRAINT DF_Property_HasWifi_Patch DEFAULT 1;
IF COL_LENGTH('dbo.Property', 'HasPool') IS NULL ALTER TABLE dbo.Property ADD HasPool BIT NOT NULL CONSTRAINT DF_Property_HasPool_Patch DEFAULT 0;
IF COL_LENGTH('dbo.Property', 'HasParking') IS NULL ALTER TABLE dbo.Property ADD HasParking BIT NOT NULL CONSTRAINT DF_Property_HasParking_Patch DEFAULT 1;
IF COL_LENGTH('dbo.Property', 'HasAC') IS NULL ALTER TABLE dbo.Property ADD HasAC BIT NOT NULL CONSTRAINT DF_Property_HasAC_Patch DEFAULT 1;
IF COL_LENGTH('dbo.Property', 'HasBreakfast') IS NULL ALTER TABLE dbo.Property ADD HasBreakfast BIT NOT NULL CONSTRAINT DF_Property_HasBreakfast_Patch DEFAULT 0;
IF COL_LENGTH('dbo.Property', 'IsPetAllowed') IS NULL ALTER TABLE dbo.Property ADD IsPetAllowed BIT NOT NULL CONSTRAINT DF_Property_IsPetAllowed_Patch DEFAULT 0;
IF COL_LENGTH('dbo.Property', 'IsSelfCheckIn') IS NULL ALTER TABLE dbo.Property ADD IsSelfCheckIn BIT NOT NULL CONSTRAINT DF_Property_IsSelfCheckIn_Patch DEFAULT 0;
GO

IF COL_LENGTH('dbo.Booking', 'RoomQuantity') IS NULL ALTER TABLE dbo.Booking ADD RoomQuantity INT NOT NULL CONSTRAINT DF_Booking_RoomQuantity_Patch DEFAULT 1;
IF COL_LENGTH('dbo.Booking', 'AdultGuest') IS NULL ALTER TABLE dbo.Booking ADD AdultGuest INT NOT NULL CONSTRAINT DF_Booking_AdultGuest_Patch DEFAULT 1;
IF COL_LENGTH('dbo.Booking', 'ChildGuest') IS NULL ALTER TABLE dbo.Booking ADD ChildGuest INT NOT NULL CONSTRAINT DF_Booking_ChildGuest_Patch DEFAULT 0;
IF COL_LENGTH('dbo.Booking', 'SelectedAddOns') IS NULL ALTER TABLE dbo.Booking ADD SelectedAddOns NVARCHAR(MAX) NOT NULL CONSTRAINT DF_Booking_SelectedAddOns_Patch DEFAULT N'[]';
IF COL_LENGTH('dbo.Booking', 'ContactFullName') IS NULL ALTER TABLE dbo.Booking ADD ContactFullName NVARCHAR(100) NULL;
IF COL_LENGTH('dbo.Booking', 'ContactEmail') IS NULL ALTER TABLE dbo.Booking ADD ContactEmail NVARCHAR(100) NULL;
IF COL_LENGTH('dbo.Booking', 'ContactPhone') IS NULL ALTER TABLE dbo.Booking ADD ContactPhone NVARCHAR(20) NULL;
IF COL_LENGTH('dbo.Booking', 'IdentityNumber') IS NULL ALTER TABLE dbo.Booking ADD IdentityNumber NVARCHAR(50) NULL;
GO

IF COL_LENGTH('dbo.Payment', 'Provider') IS NULL ALTER TABLE dbo.Payment ADD Provider NVARCHAR(50) NULL;
IF COL_LENGTH('dbo.Payment', 'TransactionCode') IS NULL ALTER TABLE dbo.Payment ADD TransactionCode NVARCHAR(100) NULL;
IF COL_LENGTH('dbo.Payment', 'CheckoutUrl') IS NULL ALTER TABLE dbo.Payment ADD CheckoutUrl NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.Payment', 'QrCode') IS NULL ALTER TABLE dbo.Payment ADD QrCode NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.Payment', 'PaidAt') IS NULL ALTER TABLE dbo.Payment ADD PaidAt DATETIME2 NULL;
IF COL_LENGTH('dbo.Payment', 'UpdatedAt') IS NULL ALTER TABLE dbo.Payment ADD UpdatedAt DATETIME2 NULL;
GO

-- =========================
-- Seed data
-- =========================

MERGE dbo.PropertyType AS target
USING (VALUES
    (CONVERT(UNIQUEIDENTIFIER, '01111111-1111-1111-1111-111111111111'), N'Hotel'),
    (CONVERT(UNIQUEIDENTIFIER, '04444444-4444-4444-4444-444444444444'), N'Motel'),
    (CONVERT(UNIQUEIDENTIFIER, '05555555-5555-5555-5555-555555555555'), N'Homestay'),
    (CONVERT(UNIQUEIDENTIFIER, '02222222-2222-2222-2222-222222222222'), N'Resort'),
    (CONVERT(UNIQUEIDENTIFIER, '03333333-3333-3333-3333-333333333333'), N'Apartment')
) AS source (Id, [Name])
ON target.[Name] = source.[Name]
WHEN NOT MATCHED THEN
    INSERT (Id, [Name]) VALUES (source.Id, source.[Name]);
GO

MERGE dbo.Property AS target
USING (VALUES
    (
        CONVERT(UNIQUEIDENTIFIER, '11111111-1111-1111-1111-111111111111'),
        N'Hotel',
        N'BingCook Central Hotel',
        N'Modern hotel near downtown restaurants and transit.',
        N'12 Nguyen Hue Street',
        N'Ho Chi Minh City',
        CONVERT(DECIMAL(10,7), 10.7756000),
        CONVERT(DECIMAL(10,7), 106.7019000),
        N'["Wi-Fi","Parking","AC","Breakfast"]',
        CONVERT(DECIMAL(12,2), 850000),
        CONVERT(DECIMAL(2,1), 4.7),
        N'Active',
        CONVERT(BIT, 1), CONVERT(BIT, 0), CONVERT(BIT, 1), CONVERT(BIT, 1), CONVERT(BIT, 1), CONVERT(BIT, 0), CONVERT(BIT, 1)
    ),
    (
        CONVERT(UNIQUEIDENTIFIER, '12222222-2222-2222-2222-222222222222'),
        N'Resort',
        N'BingCook Garden Resort',
        N'Quiet resort with pool, garden paths, and family rooms.',
        N'88 Tran Phu Beach Road',
        N'Da Nang',
        CONVERT(DECIMAL(10,7), 16.0678000),
        CONVERT(DECIMAL(10,7), 108.2208000),
        N'["Wi-Fi","Pool","Parking","AC","Breakfast","Pet friendly"]',
        CONVERT(DECIMAL(12,2), 1450000),
        CONVERT(DECIMAL(2,1), 4.8),
        N'Active',
        CONVERT(BIT, 1), CONVERT(BIT, 1), CONVERT(BIT, 1), CONVERT(BIT, 1), CONVERT(BIT, 1), CONVERT(BIT, 1), CONVERT(BIT, 0)
    ),
    (
        CONVERT(UNIQUEIDENTIFIER, '13333333-3333-3333-3333-333333333333'),
        N'Apartment',
        N'BingCook Sky Apartment',
        N'Self check-in apartment for long stays and small groups.',
        N'45 West Lake View',
        N'Ha Noi',
        CONVERT(DECIMAL(10,7), 21.0583000),
        CONVERT(DECIMAL(10,7), 105.8317000),
        N'["Wi-Fi","Parking","AC","Self check-in"]',
        CONVERT(DECIMAL(12,2), 980000),
        CONVERT(DECIMAL(2,1), 4.6),
        N'Active',
        CONVERT(BIT, 1), CONVERT(BIT, 0), CONVERT(BIT, 1), CONVERT(BIT, 1), CONVERT(BIT, 0), CONVERT(BIT, 0), CONVERT(BIT, 1)
    )
) AS source (
    Id, TypeName, [Name], [Description], [Address], City, Latitude, Longitude, Amenities,
    PricePerNight, Rating, [Status], HasWifi, HasPool, HasParking, HasAC, HasBreakfast,
    IsPetAllowed, IsSelfCheckIn
)
ON target.Id = source.Id
WHEN MATCHED THEN
    UPDATE SET
        TypeId = (SELECT TOP 1 Id FROM dbo.PropertyType WHERE [Name] = source.TypeName),
        [Name] = source.[Name],
        [Description] = source.[Description],
        [Address] = source.[Address],
        City = source.City,
        Latitude = source.Latitude,
        Longitude = source.Longitude,
        Amenities = source.Amenities,
        PricePerNight = source.PricePerNight,
        Rating = source.Rating,
        [Status] = source.[Status],
        HasWifi = source.HasWifi,
        HasPool = source.HasPool,
        HasParking = source.HasParking,
        HasAC = source.HasAC,
        HasBreakfast = source.HasBreakfast,
        IsPetAllowed = source.IsPetAllowed,
        IsSelfCheckIn = source.IsSelfCheckIn
WHEN NOT MATCHED THEN
    INSERT (
        Id, TypeId, [Name], [Description], [Address], City, Latitude, Longitude, Amenities,
        PricePerNight, Rating, [Status], HasWifi, HasPool, HasParking, HasAC, HasBreakfast,
        IsPetAllowed, IsSelfCheckIn
    )
    VALUES (
        source.Id,
        (SELECT TOP 1 Id FROM dbo.PropertyType WHERE [Name] = source.TypeName),
        source.[Name], source.[Description], source.[Address], source.City, source.Latitude,
        source.Longitude, source.Amenities, source.PricePerNight, source.Rating, source.[Status],
        source.HasWifi, source.HasPool, source.HasParking, source.HasAC, source.HasBreakfast,
        source.IsPetAllowed, source.IsSelfCheckIn
    );
GO

MERGE dbo.PropertyImage AS target
USING (VALUES
    (CONVERT(UNIQUEIDENTIFIER, '31111111-1111-1111-1111-111111111111'), CONVERT(UNIQUEIDENTIFIER, '11111111-1111-1111-1111-111111111111'), N'https://images.unsplash.com/photo-1566073771259-6a8506099945'),
    (CONVERT(UNIQUEIDENTIFIER, '32222222-2222-2222-2222-222222222222'), CONVERT(UNIQUEIDENTIFIER, '12222222-2222-2222-2222-222222222222'), N'https://images.unsplash.com/photo-1582719508461-905c673771fd'),
    (CONVERT(UNIQUEIDENTIFIER, '33333333-3333-3333-3333-333333333333'), CONVERT(UNIQUEIDENTIFIER, '13333333-3333-3333-3333-333333333333'), N'https://images.unsplash.com/photo-1505693416388-ac5ce068fe85')
) AS source (Id, PropertyId, ImageUrl)
ON target.Id = source.Id
WHEN MATCHED THEN
    UPDATE SET PropertyId = source.PropertyId, ImageUrl = source.ImageUrl
WHEN NOT MATCHED THEN
    INSERT (Id, PropertyId, ImageUrl) VALUES (source.Id, source.PropertyId, source.ImageUrl);
GO

MERGE dbo.Room AS target
USING (VALUES
    (CONVERT(UNIQUEIDENTIFIER, '21111111-1111-1111-1111-111111111111'), CONVERT(UNIQUEIDENTIFIER, '11111111-1111-1111-1111-111111111111'), N'Deluxe King Room', CONVERT(DECIMAL(12,2), 850000), 3, 8, 8),
    (CONVERT(UNIQUEIDENTIFIER, '21111111-2222-2222-2222-222222222222'), CONVERT(UNIQUEIDENTIFIER, '11111111-1111-1111-1111-111111111111'), N'Family Twin Room', CONVERT(DECIMAL(12,2), 1250000), 4, 5, 5),
    (CONVERT(UNIQUEIDENTIFIER, '22222222-1111-1111-1111-111111111111'), CONVERT(UNIQUEIDENTIFIER, '12222222-2222-2222-2222-222222222222'), N'Garden Villa', CONVERT(DECIMAL(12,2), 1450000), 4, 4, 4),
    (CONVERT(UNIQUEIDENTIFIER, '22222222-2222-2222-2222-222222222222'), CONVERT(UNIQUEIDENTIFIER, '12222222-2222-2222-2222-222222222222'), N'Poolside Suite', CONVERT(DECIMAL(12,2), 1950000), 5, 3, 3),
    (CONVERT(UNIQUEIDENTIFIER, '23333333-1111-1111-1111-111111111111'), CONVERT(UNIQUEIDENTIFIER, '13333333-3333-3333-3333-333333333333'), N'One Bedroom Apartment', CONVERT(DECIMAL(12,2), 980000), 3, 6, 6),
    (CONVERT(UNIQUEIDENTIFIER, '23333333-2222-2222-2222-222222222222'), CONVERT(UNIQUEIDENTIFIER, '13333333-3333-3333-3333-333333333333'), N'Two Bedroom Apartment', CONVERT(DECIMAL(12,2), 1550000), 5, 4, 4)
) AS source (Id, PropertyId, [Name], Price, Capacity, TotalRoom, AvailableRoom)
ON target.Id = source.Id
WHEN MATCHED THEN
    UPDATE SET
        PropertyId = source.PropertyId,
        [Name] = source.[Name],
        Price = source.Price,
        Capacity = source.Capacity,
        TotalRoom = source.TotalRoom,
        AvailableRoom = source.AvailableRoom
WHEN NOT MATCHED THEN
    INSERT (Id, PropertyId, [Name], Price, Capacity, TotalRoom, AvailableRoom)
    VALUES (source.Id, source.PropertyId, source.[Name], source.Price, source.Capacity, source.TotalRoom, source.AvailableRoom);
GO

MERGE dbo.RoomImage AS target
USING (VALUES
    (CONVERT(UNIQUEIDENTIFIER, '41111111-1111-1111-1111-111111111111'), CONVERT(UNIQUEIDENTIFIER, '21111111-1111-1111-1111-111111111111'), N'https://images.unsplash.com/photo-1590490360182-c33d57733427'),
    (CONVERT(UNIQUEIDENTIFIER, '41111111-2222-2222-2222-222222222222'), CONVERT(UNIQUEIDENTIFIER, '21111111-2222-2222-2222-222222222222'), N'https://images.unsplash.com/photo-1566665797739-1674de7a421a'),
    (CONVERT(UNIQUEIDENTIFIER, '42222222-1111-1111-1111-111111111111'), CONVERT(UNIQUEIDENTIFIER, '22222222-1111-1111-1111-111111111111'), N'https://images.unsplash.com/photo-1571896349842-33c89424de2d'),
    (CONVERT(UNIQUEIDENTIFIER, '42222222-2222-2222-2222-222222222222'), CONVERT(UNIQUEIDENTIFIER, '22222222-2222-2222-2222-222222222222'), N'https://images.unsplash.com/photo-1578683010236-d716f9a3f461'),
    (CONVERT(UNIQUEIDENTIFIER, '43333333-1111-1111-1111-111111111111'), CONVERT(UNIQUEIDENTIFIER, '23333333-1111-1111-1111-111111111111'), N'https://images.unsplash.com/photo-1522708323590-d24dbb6b0267'),
    (CONVERT(UNIQUEIDENTIFIER, '43333333-2222-2222-2222-222222222222'), CONVERT(UNIQUEIDENTIFIER, '23333333-2222-2222-2222-222222222222'), N'https://images.unsplash.com/photo-1493809842364-78817add7ffb')
) AS source (Id, RoomId, ImageUrl)
ON target.Id = source.Id
WHEN MATCHED THEN
    UPDATE SET RoomId = source.RoomId, ImageUrl = source.ImageUrl
WHEN NOT MATCHED THEN
    INSERT (Id, RoomId, ImageUrl) VALUES (source.Id, source.RoomId, source.ImageUrl);
GO

-- =========================
-- PayOS compatibility patch
-- Add this block at the end of deployed databases before enabling PayOS checkout.
-- Current backend repositories read/write dbo.Payment.PaymentLinkId.
-- =========================

IF COL_LENGTH('dbo.Payment', 'PaymentLinkId') IS NULL
BEGIN
    ALTER TABLE dbo.Payment ADD PaymentLinkId NVARCHAR(100) NULL;
END;
GO

-- =========================
-- Realtime chat add-on
-- Add this block at the end of deployed databases.
--
-- Why not use dbo.Chat directly?
-- The existing dbo.Chat table stores only sender/receiver/message, so it cannot
-- safely group messages by property, booking, read state, or conversation status.
-- Keep dbo.Chat for backward compatibility and use these two simple tables for
-- the customer <-> hotel realtime chat feature.
-- =========================

IF OBJECT_ID(N'dbo.ChatConversation', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ChatConversation (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ChatConversation PRIMARY KEY DEFAULT NEWID(),
        PropertyId UNIQUEIDENTIFIER NOT NULL,
        BookingId UNIQUEIDENTIFIER NULL,
        CustomerUserId UNIQUEIDENTIFIER NOT NULL,
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT DF_ChatConversation_Status DEFAULT N'Open',
        LastMessageAt DATETIME2 NULL,
        CustomerLastReadAt DATETIME2 NULL,
        HostLastReadAt DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_ChatConversation_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_ChatConversation_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_ChatConversation_Property FOREIGN KEY (PropertyId) REFERENCES dbo.Property(Id),
        CONSTRAINT FK_ChatConversation_Booking FOREIGN KEY (BookingId) REFERENCES dbo.Booking(Id),
        CONSTRAINT FK_ChatConversation_Customer FOREIGN KEY (CustomerUserId) REFERENCES dbo.[User](Id),
        CONSTRAINT CK_ChatConversation_Status CHECK ([Status] IN (N'Open', N'Closed', N'Archived'))
    );
END;
GO

IF OBJECT_ID(N'dbo.ChatMessage', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ChatMessage (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ChatMessage PRIMARY KEY DEFAULT NEWID(),
        ConversationId UNIQUEIDENTIFIER NOT NULL,
        SenderUserId UNIQUEIDENTIFIER NOT NULL,
        Body NVARCHAR(2000) NOT NULL,
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_ChatMessage_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_ChatMessage_Conversation FOREIGN KEY (ConversationId) REFERENCES dbo.ChatConversation(Id) ON DELETE CASCADE,
        CONSTRAINT FK_ChatMessage_Sender FOREIGN KEY (SenderUserId) REFERENCES dbo.[User](Id),
        CONSTRAINT CK_ChatMessage_Body_NotBlank CHECK (LEN(LTRIM(RTRIM(Body))) > 0)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ChatConversation_Customer'
      AND object_id = OBJECT_ID(N'dbo.ChatConversation')
)
BEGIN
    CREATE INDEX IX_ChatConversation_Customer
        ON dbo.ChatConversation(CustomerUserId, UpdatedAt DESC);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ChatConversation_Property'
      AND object_id = OBJECT_ID(N'dbo.ChatConversation')
)
BEGIN
    CREATE INDEX IX_ChatConversation_Property
        ON dbo.ChatConversation(PropertyId, UpdatedAt DESC);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_ChatMessage_Conversation_CreatedAt'
      AND object_id = OBJECT_ID(N'dbo.ChatMessage')
)
BEGIN
    CREATE INDEX IX_ChatMessage_Conversation_CreatedAt
        ON dbo.ChatMessage(ConversationId, CreatedAt);
END;
GO
