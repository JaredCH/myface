using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MyFace.Data;
using MyFace.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<MyFace.Web.Services.SuspensionFilter>();
});

// Database configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Database=myface;Username=postgres;Password=postgres";

if (builder.Environment.IsDevelopment())
{
    // Use in-memory database for Development so the app runs without external services
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("myface"));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));
}

// Register services
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ForumService>();
builder.Services.AddScoped<OnionStatusService>();
builder.Services.AddScoped<RssService>();
builder.Services.AddScoped<ReputationService>();
builder.Services.AddScoped<VisitTrackingService>();
builder.Services.AddScoped<RateLimitService>();
builder.Services.AddScoped<MailService>();
builder.Services.AddScoped<MyFace.Web.Services.BBCodeFormatter>();
builder.Services.AddSingleton<MyFace.Web.Services.CaptchaService>();
builder.Services.AddHostedService<MyFace.Web.Services.OnionMonitorWorker>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<MyFace.Services.ChatService>();
builder.Services.AddSingleton<MyFace.Services.ChatSnapshotService>();

// HttpClient for Tor/Onion monitoring
builder.Services.AddHttpClient("TorClient")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        UseProxy = true,
        // NOTE: .NET HttpClientHandler does not support SOCKS5 directly.
        // Use an HTTP proxy like Privoxy that forwards to Tor's SOCKS.
        Proxy = new System.Net.WebProxy("http://127.0.0.1:8118"),
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5,
        // Ignore SSL errors for self-signed .onion certificates
        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
    });

// Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        if (builder.Environment.IsDevelopment())
        {
            options.Cookie.SecurePolicy = CookieSecurePolicy.None;
            options.Cookie.SameSite = SameSiteMode.Lax;
        }
        else
        {
            // Tor hidden services run over HTTP (but are encrypted), so we cannot enforce Secure cookies
            options.Cookie.SecurePolicy = CookieSecurePolicy.None;
            options.Cookie.SameSite = SameSiteMode.Lax;
        }
    });

builder.Services.AddAuthorization();

// Session for anonymous posting tracking
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.None; // Required for Tor (HTTP)
});

// Security headers
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAntiforgery(options =>
    {
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;
    });
}
else
{
    builder.Services.AddAntiforgery(options =>
    {
        options.Cookie.SecurePolicy = CookieSecurePolicy.None;
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // app.UseHsts(); // Disable HSTS for Tor hidden service
}

// Security: Remove X-Powered-By header
app.Use(async (context, next) =>
{
    context.Response.Headers.Remove("Server");
    context.Response.Headers.Remove("X-Powered-By");
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    await next();
});

// Do NOT use HTTPS redirection for Tor hidden service
// app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();

app.UseSession();
app.UseMiddleware<MyFace.Web.Middleware.VisitTrackingMiddleware>();
app.UseMiddleware<MyFace.Web.Middleware.CaptchaMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<MyFace.Web.Middleware.UsernameRequiredMiddleware>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Ensure database exists for Development to avoid migration requirements
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var mailService = scope.ServiceProvider.GetRequiredService<MailService>();
    try
    {
        db.Database.EnsureCreated();
        mailService.EnsureSchemaAsync().GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database initialization failed: {ex.Message}");
    }
}

app.Run();
