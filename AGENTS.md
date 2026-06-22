# BingCook Backend Agent Guide

Muc tieu file nay: giup dev/agent moi doc nhanh kien truc backend, biet luong request chinh, noi nao can sua khi them tinh nang.

## Tong quan

- Project: ASP.NET Core Web API, .NET 8.
- Solution: `BingCook.sln`.
- App project: `BingCook.Api.csproj`.
- Test project: `BingCook.Api.Tests/BingCook.Api.Tests.csproj`.
- Database: Microsoft SQL Server, truy cap truc tiep bang `Microsoft.Data.SqlClient` qua `SqlConnectionFactory`.
- Auth: JWT Bearer + BCrypt password hash.
- API docs: Swagger bat trong Development.

## Cap nhat gan day

- Loi dang ky `NpgsqlConnector.SetupEncryption` da duoc trace ve root cause: app dung PostgreSQL provider `Npgsql`, nhung connection string tro SQL Server port `1433`.
- Da doi runtime DB sang SQL Server:
  - Them package `Microsoft.Data.SqlClient`.
  - Them `Data/SqlConnectionFactory.cs`.
  - Them `Data/SqlServerUserRepository.cs` cho auth/register/login.
  - Them `Data/SqlServerProductRepository.cs` cho product list/detail.
  - Them `Data/SqlServerBookingRepository.cs` cho booking draft/checkout/PayOS update.
  - `Program.cs` DI sang `SqlServer*Repository`.
  - `appsettings.json` doi connection string sang format `Server=host,1433;Database=...;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=True`.
- Cac `Postgres*Repository` cu van con trong repo de tham khao, nhung runtime hien tai khong dung.
- Sau khi sua provider/config, phai stop process API cu va chay lai `dotnet run --project BingCook.Api.csproj`; neu khong, binary cu van co the nem loi `Npgsql`.
- Verify da chay:
  - `dotnet build BingCook.Api.csproj -o C:\tmp\bingcook-build /p:UseAppHost=false` pass.
  - `dotnet test BingCook.Api.Tests\BingCook.Api.Tests.csproj --no-restore -o C:\tmp\bingcook-test-build /p:UseAppHost=false` pass.

## Kien truc lop

```text
HTTP client
  -> Controllers
     -> Services
        -> Repositories
           -> SQL Server
```

- `Program.cs`: DI container, auth middleware, Swagger, SQL Server connection factory.
- `Controllers/`: route HTTP, validate DTO qua ASP.NET Core model validation, map service/repository result sang HTTP response.
- `Services/`: business logic auth, password hashing, JWT, welcome email.
- `Data/`: repository interfaces + SQL Server implementation. Cac `Postgres*Repository` cu con trong repo nhung khong duoc DI dung.
- `Models/`: internal records dung trong service/repository.
- `Dtos/`: request/response contract cho API client.
- `BingCook.Api.Tests/`: xUnit tests cho service logic.

## Dependency Injection

Dang ky trong `Program.cs`:

- `SqlConnectionFactory`: singleton, dung connection string `ConnectionStrings:DefaultConnection`.
- `IUserRepository -> SqlServerUserRepository`: scoped.
- `IProductRepository -> SqlServerProductRepository`: scoped.
- `IBookingRepository -> SqlServerBookingRepository`: scoped.
- `IPasswordHasher -> BCryptPasswordHasher`: scoped.
- `IJwtTokenService -> JwtTokenService`: scoped.
- `IWelcomeEmailSender -> SmtpWelcomeEmailSender`: scoped.
- `IAuthService -> AuthService`: scoped.
- `IBookingService -> BookingService`: scoped.
- `IPayOSPaymentGateway -> PayOSPaymentGateway`: typed HTTP client.

Middleware order hien tai:

```text
UseHttpsRedirection
UseAuthentication
UseAuthorization
MapControllers
```

## API hien co

### Auth

Controller: `Controllers/AuthController.cs`.

- `POST /api/auth/register`
  - Body: `RegisterRequest`.
  - Tao customer moi.
  - Normalize full name/email/phone.
  - Check trung email hoac phone.
  - Hash password bang BCrypt.
  - Insert user role `Customer`.
  - Gui welcome email neu cau hinh bat.
  - Tra `AuthResponse` gom user + JWT.

- `POST /api/auth/login`
  - Body: `LoginRequest`.
  - `identity` co the la email hoac phone.
  - Verify password bang BCrypt.
  - Tra `AuthResponse` gom user + JWT.

- `POST /api/auth/logout`
  - Yeu cau `[Authorize]`.
  - Hien tai no-op, tra `204 NoContent`.
  - Neu can real logout, them refresh token/session blacklist hoac token revocation.

### Products

Controller: `Controllers/ProductsController.cs`.

- `GET /api/products`
- `GET /api/productlist`

Ca hai route cung goi `IProductRepository.GetAllAsync`.

Response: `ProductListItemResponse`, gom:

- id/type/name/description.
- location/city/address.
- imageUrl.
- rating/reviewCount.
- amenities list build tu cac cot boolean.
- pricePerNight.
- status.

### Bookings

Controller: `Controllers/BookingsController.cs`.

- `POST /api/bookings/draft`
  - Yeu cau `[Authorize]`.
  - Body: `CreateBookingDraftRequest`.
  - Tao booking tam thoi status `Pending`.
  - Validate ngay tra phong phai sau ngay nhan phong.
  - Validate tong khach nguoi lon/tre em > 0.
  - Validate tong khach khong vuot `room.capacity * roomQuantity`.
  - Validate so phong con trong khoang ngay da chon.
  - Tinh `nights`, `roomSubtotal`, `addOnSubtotal`, `totalPrice`.
  - Tra `BookingDraftResponse` gom `bookingId`, thong tin phong, so dem, tong tien tam tinh, add-ons, `NextAction = ProceedToConfirmationPayment`.

Add-on code hien co trong `BookingService`:

- `breakfast`: 120000 / guest / night.
- `airport_pickup`: 250000 / booking.
- `pet_surcharge`: 150000 / room / night.

- `POST /api/bookings/checkout`
  - Yeu cau `[Authorize]`.
  - Body: `CheckoutBookingRequest`.
  - `paymentMethod = PayAtProperty`: booking status `Confirmed`, payment status `Pending`, khach tra tai noi luu tru.
  - `paymentMethod = PayOS` hoac `PayNow`: tao PayOS checkout URL, booking status `PendingPayment`, payment status `Pending`.
  - Tra `BookingCheckoutResponse` gom booking/payment status, amount, transactionCode, paymentLinkId, checkoutUrl, qrCode.

Controller: `Controllers/PaymentsController.cs`.

- `POST /api/payments/payos/webhook`
  - PayOS callback server-to-server.
  - Verify signature bang `PayOS__ChecksumKey`.
  - Neu status `PAID`, update payment `Success` va booking `Paid`.
- `GET /api/payments/payos/return`
  - Redirect URL sau khi PayOS thanh toan thanh cong.
  - Demo fallback: neu query `status=PAID`, update booking `Paid`.
- `GET /api/payments/payos/cancel`
  - Redirect URL khi user huy PayOS.
  - Update payment `Cancelled` va booking `Cancelled` neu co `orderCode`.

## Auth flow

```text
Register
  -> AuthController.Register
  -> AuthService.RegisterAsync
  -> IUserRepository.EmailOrPhoneExistsAsync
  -> IPasswordHasher.Hash
  -> IUserRepository.CreateAsync
  -> IWelcomeEmailSender.SendWelcomeEmailAsync
  -> IJwtTokenService.CreateToken
  -> AuthResponse

Login
  -> AuthController.Login
  -> AuthService.LoginAsync
  -> IUserRepository.FindByIdentityAsync
  -> IPasswordHasher.Verify
  -> IJwtTokenService.CreateToken
  -> AuthResponse
```

## Booking flow

```text
Create booking draft
  -> BookingsController.CreateDraft
  -> IBookingService.CreateDraftAsync
  -> IBookingRepository.GetRoomQuoteAsync
  -> validate dates / guests / room quantity / availability
  -> calculate nights + subtotal
  -> IBookingRepository.CreateDraftAsync
  -> BookingDraftResponse

Checkout PayAtProperty
  -> BookingsController.Checkout
  -> IBookingService.CheckoutAsync
  -> IBookingRepository.GetCheckoutQuoteAsync
  -> IBookingRepository.CompleteCheckoutAsync
  -> booking Confirmed + payment Pending
  -> BookingCheckoutResponse

Checkout PayOS
  -> BookingsController.Checkout
  -> IBookingService.CheckoutAsync
  -> IPayOSPaymentGateway.CreatePaymentLinkAsync
  -> IBookingRepository.CompleteCheckoutAsync
  -> booking PendingPayment + payment Pending
  -> frontend open checkoutUrl / qrCode
  -> PayOS webhook/return
  -> IBookingRepository.UpdatePayOSPaymentAsync
  -> booking Paid / Cancelled
```

## Database expectations

Code hien tai dang chay voi Microsoft SQL Server schema trong `C:/FPTU/SU26/PRM393/BookingDB.sql`.

- Provider runtime: `Microsoft.Data.SqlClient`.
- Connection string dung format SQL Server: `Server=host,1433;Database=BookingDB;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=True`.
- `Program.cs` dang ky `SqlConnectionFactory` va DI sang cac repo `SqlServer*Repository`.
- Cac file `Postgres*Repository` van con de tham khao/rollback, nhung runtime hien tai khong dung chung.

Cac table/column chinh repository can:

- `dbo.[User]`: `Id`, `FullName`, `Email`, `Phone`, `Password`, `Role`, `CreatedAt`.
- `dbo.Property`: `Id`, `TypeId`, `Name`, `Description`, `City`, `Address`, `Status`, `CreatedAt`, `HasWifi`, `HasPool`, `HasParking`, `HasAC`, `HasBreakfast`, `IsPetAllowed`, `IsSelfCheckIn`.
- `dbo.PropertyType`: `Id`, `Name`.
- `dbo.PropertyImage`: `Id`, `PropertyId`, `ImageUrl`.
- `dbo.Room`: `Id`, `PropertyId`, `Name`, `Price`, `Capacity`, `TotalRoom`, `AvailableRoom`.
- `dbo.RoomImage`: `Id`, `RoomId`, `ImageUrl`.
- `dbo.Booking`: `Id`, `UserId`, `PropertyId`, `RoomId`, `CheckIn`, `CheckOut`, `Guest`, `TotalPrice`, `Status`, `Note`, `RoomQuantity`, `AdultGuest`, `ChildGuest`, `SelectedAddOns`, `ContactFullName`, `ContactEmail`, `ContactPhone`, `IdentityNumber`.
- `dbo.Payment`: `Id`, `BookingId`, `Method`, `Amount`, `Status`, `CreatedAt`, `Provider`, `TransactionCode`, `CheckoutUrl`, `QrCode`, `PaidAt`, `UpdatedAt`.
- `dbo.Review`: `PropertyId`, `Rating`, `UserId`, `Comment`, `CreatedAt`.

Can luu y:

- `SelectedAddOns` trong SQL Server luu JSON text, vi SQL Server khong co `TEXT[]` nhu PostgreSQL.
- Product/booking availability tinh bang `TotalRoom - SUM(Booking.RoomQuantity)` trong khoang ngay overlap.
- Neu gap loi dang ky dang kieu `NpgsqlConnector.SetupEncryption`, nghia la app dang chay binary/config cu PostgreSQL hoac chua restart sau migrate SQL Server.
- `scripts/seed_booking_rooms.sql` la seed/patch PostgreSQL cu; voi SQL Server hay dung `C:/FPTU/SU26/PRM393/BookingDB.sql`.
## Config

File chinh: `appsettings.json`.

- `ConnectionStrings:DefaultConnection`: SQL Server connection.
- `Jwt:Issuer`: issuer token.
- `Jwt:Audience`: audience token.
- `Jwt:SigningKey`: key ky HMAC SHA256, can toi thieu 32 chars cho dev.
- `Jwt:ExpiresMinutes`: thoi gian song token.
- `WelcomeEmail:Enabled`: bat/tat gui email welcome.
- `WelcomeEmail:Host`, `Port`, `EnableSsl`, `Username`, `Password`, `FromEmail`, `FromName`: SMTP config.
- `PayOS__ClientId`, `PayOS__ApiKey`, `PayOS__ChecksumKey`: PayOS secret env vars.
- `PayOS__ReturnUrl`, `PayOS__CancelUrl`: PayOS redirect URLs.

Khuyen nghi: khong commit password/secret that vao `appsettings.json`; dung user secrets, env vars, hoac config rieng theo moi truong.

Repo co loader `.env` trong `Program.cs`. File `.env` duoc `.gitignore` bo qua.

## Ports va chay local

`Properties/launchSettings.json`:

- HTTP: `http://localhost:5115`.
- HTTPS: `https://localhost:7008`.
- Swagger: `/swagger`.

Lenh hay dung:

```powershell
dotnet restore
dotnet build
dotnet run --project BingCook.Api.csproj
dotnet test
```

Co file request mau: `BingCook.Api.http`.

Sample booking draft request nam trong `BingCook.Api.http`, dung `Authorization: Bearer {{authToken}}`.

Sample checkout PayAtProperty va PayOS nam trong `BingCook.Api.http`.

## Test

Test hien co nam trong `BingCook.Api.Tests/AuthServiceTests.cs`.

Dang cover:

- Register thanh cong thi goi welcome email sau khi tao user.
- Register van success neu welcome email fail.

Khi them logic service, uu tien test service bang fake repository/sender nhu pattern hien co. Neu them endpoint phuc tap, can them integration test rieng.

## Quy uoc khi sua code

- Giu controller mong: route + status code + DTO mapping.
- Business rule dat trong service.
- SQL dat trong repository.
- Dung parameterized SQL, khong noi chuoi user input.
- Dung `CancellationToken` di xuyen controller -> service -> repository.
- DTO la contract public; model la internal shape.
- Khi them dependency moi, dang ky DI trong `Program.cs`.
- Khi them config moi, tao options class neu config co nhieu field.

## Diem can canh giac

- `AuthService.LogoutAsync` hien chua revoke token.
- Welcome email fail chi log warning, khong fail register. Day la behavior co chu y.
- `ProductsController` co 2 route cho cung list: `/api/products` va `/api/productlist`.
- Product query chi lay property `Active`.
- Product availability tinh theo `room.totalroom - SUM(booking.roomquantity)` trong khoang ngay.
- Product image lay anh dau tien theo `propertyimage.id`.
- Product price lay `MIN(room.price)`.
- Rating lay trung binh tu `review.rating`.
- Booking draft tao row `booking` status `Pending`, chua xu ly payment.
- Booking add-on hien luu code JSON trong `Booking.SelectedAddOns`; gia add-on tinh trong `BookingService`.
- Checkout PayAtProperty tao payment status `Pending`, booking status `Confirmed`.
- Checkout PayOS tao PayOS link, payment status `Pending`, booking status `PendingPayment`.
- PayOS success update payment `Success`, booking `Paid`; cancel update ca hai ve `Cancelled`.
- Schema SQL ngoai project can sync voi repository truoc khi deploy.


