using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniMart.Web.Data;
using MiniMart.Web.Models.ViewModels;
using AppUser = MiniMart.Web.Models.Entities.User;

namespace MiniMart.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly MiniMartDbContext _context;

        public AccountController(MiniMartDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToLocal(returnUrl);
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var username = model.Username.Trim();
            AppUser? user = null;

            try
            {
                user = await _context.Users
                    .Include(u => u.Store)
                    .FirstOrDefaultAsync(u => u.Username == username);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Không thể kết nối đến cơ sở dữ liệu Oracle. Vui lòng kiểm tra dịch vụ Oracle và thông tin kết nối trong appsettings.json. Chi tiết: {ex.Message}");
                return View(model);
            }

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không chính xác.");
                return View(model);
            }

            if (user.Status != "ACTIVE")
            {
                ModelState.AddModelError(string.Empty, "Tài khoản của bạn đang bị vô hiệu hóa hoặc bị khóa.");
                return View(model);
            }

            if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.Now)
            {
                var remainingMinutes = Math.Ceiling((user.LockedUntil.Value - DateTime.Now).TotalMinutes);
                ModelState.AddModelError(string.Empty, $"Tài khoản tạm thời bị khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau {remainingMinutes} phút.");
                return View(model);
            }

            // Kiểm tra mật khẩu bằng BCrypt
            bool isPasswordValid = false;
            try
            {
                isPasswordValid = BCrypt.Net.BCrypt.Verify(model.Password, user.Password);
            }
            catch
            {
                isPasswordValid = false;
            }

            if (!isPasswordValid)
            {
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= 5)
                {
                    user.LockedUntil = DateTime.Now.AddMinutes(15);
                }
                await _context.SaveChangesAsync();

                ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không chính xác.");
                return View(model);
            }

            // Đăng nhập thành công -> Reset đếm sai
            user.FailedLoginAttempts = 0;
            user.LockedUntil = null;
            await _context.SaveChangesAsync();

            // Thiết lập Claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("FullName", user.FullName),
                new Claim("Email", user.Email ?? string.Empty),
                new Claim("StoreId", user.StoreId?.ToString() ?? "0"),
                new Claim("StoreName", user.Store?.Name ?? "MiniMart Hệ Thống")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            return RedirectToLocal(returnUrl, user.Role);
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogoutPost()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        [Authorize]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(userIdString, out long userId))
            {
                return RedirectToAction("Login");
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            if (!BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.Password))
            {
                ModelState.AddModelError(string.Empty, "Mật khẩu hiện tại không chính xác.");
                return View(model);
            }

            user.Password = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            user.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private IActionResult RedirectToLocal(string? returnUrl, string? role = null)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }
    }
}
