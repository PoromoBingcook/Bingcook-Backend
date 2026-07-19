using System.Text;
using BingCook.Api.Data;
using BingCook.Api.Hubs;
using BingCook.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<WelcomeEmailOptions>(
    builder.Configuration.GetSection(WelcomeEmailOptions.SectionName));
builder.Services.Configure<PayOSOptions>(
    builder.Configuration.GetSection(PayOSOptions.SectionName));
builder.Services.Configure<BookingOptions>(
    builder.Configuration.GetSection(BookingOptions.SectionName));

var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddSingleton(_ =>
    new SqlConnectionFactory(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("DefaultConnection is missing.")));

builder.Services.AddScoped<IUserRepository, SqlServerUserRepository>();
builder.Services.AddScoped<IEmailVerificationRepository, SqlServerEmailVerificationRepository>();
builder.Services.AddScoped<IProductRepository, SqlServerProductRepository>();
builder.Services.AddScoped<IReviewRepository, SqlServerReviewRepository>();
builder.Services.AddScoped<ISavedPropertyRepository, SqlServerSavedPropertyRepository>();
builder.Services.AddScoped<IBookingRepository, SqlServerBookingRepository>();
builder.Services.AddScoped<IChatRepository, SqlServerChatRepository>();
builder.Services.AddScoped<INotificationRepository, SqlServerNotificationRepository>();
builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IWelcomeEmailSender, SmtpWelcomeEmailSender>();
builder.Services.AddScoped<IEmailOtpSender, SmtpEmailOtpSender>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddHttpClient<IPayOSPaymentGateway, PayOSPaymentGateway>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SigningKey))
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken)
                    && path.StartsWithSegments("/hubs/chat"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.Run();

static void LoadDotEnv()
{
    var path = Path.Combine(Directory.GetCurrentDirectory(), ".env");
    if (!File.Exists(path))
    {
        return;
    }

    foreach (var rawLine in File.ReadAllLines(path))
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
        {
            continue;
        }

        var separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0)
        {
            continue;
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(key)
            || Environment.GetEnvironmentVariable(key) is not null)
        {
            continue;
        }

        Environment.SetEnvironmentVariable(key, value);
    }
}

