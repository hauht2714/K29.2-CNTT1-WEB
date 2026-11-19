using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MiniMart.Web.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Cấu hình Database Context kết nối Oracle
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=localhost:1521/XEPDB1;User Id=UTC_1;Password=123456;";

builder.Services.AddDbContext<MiniMartDbContext>(options =>
    options.UseOracle(connectionString));

// Cấu hình Cookie Authentication & Authorization
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "MiniMart.AuthCookie";
        options.Cookie.HttpOnly = true;
    });

builder.Services.AddAuthorization();

// Hỗ trợ validate AntiForgeryToken qua HTTP Header cho các request AJAX JSON
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

var app = builder.Build();

// Tự động khởi tạo dữ liệu mẫu nếu database đang chạy
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<MiniMartDbContext>();
        DbInitializer.Initialize(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("Không thể kết nối đến Oracle để khởi tạo: {Message}. Vui lòng đảm bảo Oracle đang chạy.", ex.Message);
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
