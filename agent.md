# BingCook Backend Agent Guide

Muc tieu file nay: giup dev/agent moi doc nhanh kien truc backend, biet luong request chinh, noi nao can sua khi them tinh nang.

## Tong quan

- Project: ASP.NET Core Web API, .NET 8.
- Solution: `BingCook.sln`.
- App project: `BingCook.Api.csproj`.
- Test project: `BingCook.Api.Tests/BingCook.Api.Tests.csproj`.
- Database: PostgreSQL, truy cap truc tiep bang `NpgsqlDataSource`.
- Auth: JWT Bearer + BCrypt password hash.
- API docs: Swagger bat trong Development.

## Kien truc lop

```text
HTTP client
  -> Controllers
     -> Services
        -> Repositories
           -> PostgreSQL
```

- `Program.cs`: DI container, auth middleware, Swagger, PostgreSQL datasource.
- `Controllers/`: route HTTP, validate DTO qua ASP.NET Core model validation, map service/repository result sang HTTP response.
- `Services/`: business logic auth, password hashing, JWT, welcome email.
- `Data/`: repository interfaces + PostgreSQL implementation.
- `Models/`: internal records dung trong service/repository.
- `Dtos/`: request/response contract cho API client.
- `BingCook.Api.Tests/`: xUnit tests cho service logic.

## Dependency Injection

Dang ky trong `Program.cs`:

- `NpgsqlDataSource`: singleton, dung connection string `ConnectionStrings:DefaultConnection`.
- `IUserRepository -> PostgresUserRepository`: scoped.
- `IProductRepository -> PostgresProductRepository`: scoped.
- `IPasswordHasher -> BCryptPasswordHasher`: scoped.
- `IJwtTokenService -> JwtTokenService`: scoped.
- `IWelcomeEmailSender -> SmtpWelcomeEmailSender`: scoped.
- `IAuthService -> AuthService`: scoped.

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

## Database expectations

Code hien tai expect PostgreSQL schema co cac object sau:

- Table `"User"` voi columns: `id`, `fullname`, `email`, `phone`, `password`, `role`, `createdat`.
- Enum/type `user_role`, vi insert dang cast `@role::user_role`.
- Table `property` voi columns: `id`, `typeid`, `name`, `description`, `city`, `address`, `status`, `createdat`, `haswifi`, `haspool`, `hasparking`, `hasac`, `hasbreakfast`, `ispetallowed`, `isselfcheckin`.
- Enum/type `property_status`, vi filter dung `'Active'::property_status`.
- Table `propertytype`: `id`, `name`.
- Table `propertyimage`: `id`, `propertyid`, `imageurl`.
- Table `room`: `propertyid`, `price`.
- Table `review`: `propertyid`, `rating`.

Can luu y: file `C:/FPTU/SU26/PRM393/BookingDB.sql` hien co schema co nhieu bang dung domain booking, nhung chua khop hoan toan voi code hien tai:

- `BookingDB.sql` dung `VARCHAR` cho `Role`/`Status`, trong khi code cast sang enum `user_role` va `property_status`.
- `BookingDB.sql` co `Amenities TEXT[]`, `PricePerNight`, `Rating` trong `Property`, trong khi code doc amenity tu cac cot boolean va gia tu `Room`.
- `BookingDB.sql` chua co cac cot `haswifi`, `haspool`, `hasparking`, `hasac`, `hasbreakfast`, `ispetallowed`, `isselfcheckin`.

Neu DB bi loi runtime, kiem tra schema drift truoc.

## Config

File chinh: `appsettings.json`.

- `ConnectionStrings:DefaultConnection`: PostgreSQL connection.
- `Jwt:Issuer`: issuer token.
- `Jwt:Audience`: audience token.
- `Jwt:SigningKey`: key ky HMAC SHA256, can toi thieu 32 chars cho dev.
- `Jwt:ExpiresMinutes`: thoi gian song token.
- `WelcomeEmail:Enabled`: bat/tat gui email welcome.
- `WelcomeEmail:Host`, `Port`, `EnableSsl`, `Username`, `Password`, `FromEmail`, `FromName`: SMTP config.

Khuyen nghi: khong commit password/secret that vao `appsettings.json`; dung user secrets, env vars, hoac config rieng theo moi truong.

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
- Product image lay anh dau tien theo `propertyimage.id`.
- Product price lay `MIN(room.price)`.
- Rating lay trung binh tu `review.rating`.
- Schema SQL ngoai project can sync voi repository truoc khi deploy.
