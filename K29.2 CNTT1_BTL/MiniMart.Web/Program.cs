using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
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

        // Mỗi request kiểm tra lại tài khoản: bị vô hiệu hóa/khóa, đổi vai trò hoặc đổi chi nhánh
        // thì phiên đăng nhập cũ bị hủy và người dùng phải đăng nhập lại.
        options.Events.OnValidatePrincipal = async ctx =>
        {
            var principal = ctx.Principal;
            if (principal == null || !long.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                ctx.RejectPrincipal();
                return;
            }

            var db = ctx.HttpContext.RequestServices.GetRequiredService<MiniMartDbContext>();
            var user = await db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.Status, u.Role, u.StoreId, u.LockedUntil })
                .FirstOrDefaultAsync();

            bool stillValid = user != null
                && user.Status == "ACTIVE"
                && !(user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.Now)
                && user.Role == principal.FindFirstValue(ClaimTypes.Role)
                && (user.StoreId?.ToString() ?? "0") == principal.FindFirstValue("StoreId");

            if (!stillValid)
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
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
