using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MiniMart.Web.Data;
using AppUser = MiniMart.Web.Models.Entities.User;

namespace MiniMart.Web.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class UsersController : Controller
    {
        private readonly MiniMartDbContext _context;

        public UsersController(MiniMartDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? search, string? role, long? storeId)
        {
            var query = _context.Users.Include(u => u.Store).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(u => u.Username.ToLower().Contains(s) || 
                                         u.FullName.ToLower().Contains(s) || 
                                         (u.Email != null && u.Email.ToLower().Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(role) && role != "ALL")
            {
                query = query.Where(u => u.Role == role);
            }

            if (storeId.HasValue && storeId.Value > 0)
            {
                query = query.Where(u => u.StoreId == storeId);
            }

            var users = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();
            var stores = await _context.Stores.Where(s => s.Status == "ACTIVE").ToListAsync();

            ViewBag.Search = search;
            ViewBag.Role = role ?? "ALL";
            ViewBag.StoreId = storeId;
            ViewBag.Stores = stores;

            return View(users);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Stores = new SelectList(await _context.Stores.Where(s => s.Status == "ACTIVE").ToListAsync(), "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AppUser model, string rawPassword)
        {
            // Form không gửi trường Password (mật khẩu đã băm) mà gửi rawPassword
            ModelState.Remove(nameof(AppUser.Password));
            ModelState.Remove(nameof(AppUser.Status));

            if (await _context.Users.AnyAsync(u => u.Username == (model.Username ?? "").Trim()))
            {
                ModelState.AddModelError("Username", "Tên đăng nhập đã được sử dụng.");
            }

            if (string.IsNullOrWhiteSpace(rawPassword) || rawPassword.Length < 6)
            {
                ModelState.AddModelError(string.Empty, "Mật khẩu phải có ít nhất 6 ký tự.");
            }

            await ValidateUserFieldsAsync(model, null);

            if (ModelState.IsValid)
            {
                model.Username = model.Username.Trim();
                model.Password = BCrypt.Net.BCrypt.HashPassword(rawPassword);
                model.Status = "ACTIVE";
                model.CreatedAt = DateTime.Now;
                model.UpdatedAt = DateTime.Now;

                _context.Users.Add(model);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Tạo tài khoản nhân viên '{model.Username}' thành công!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Stores = new SelectList(await _context.Stores.Where(s => s.Status == "ACTIVE").ToListAsync(), "Id", "Name", model.StoreId);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(long id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            ViewBag.Stores = new SelectList(await _context.Stores.Where(s => s.Status == "ACTIVE").ToListAsync(), "Id", "Name", user.StoreId);
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, AppUser model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Form sửa không gửi mật khẩu; mật khẩu được đổi qua chức năng "Đặt lại mật khẩu"
            ModelState.Remove(nameof(AppUser.Password));
            await ValidateUserFieldsAsync(model, id);

            if (ModelState.IsValid)
            {
                user.FullName = model.FullName;
                user.Email = model.Email;
                user.Role = model.Role;
                user.StoreId = model.StoreId;
                user.Status = model.Status;
                user.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Cập nhật nhân viên '{user.FullName}' thành công!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Stores = new SelectList(await _context.Stores.Where(s => s.Status == "ACTIVE").ToListAsync(), "Id", "Name", model.StoreId);
            return View(model);
        }

        private static readonly string[] AllowedRoles = { "ADMIN", "STORE_MANAGER", "CASHIER", "WAREHOUSE_STAFF" };
        private static readonly string[] AllowedStatuses = { "ACTIVE", "DISABLED", "LOCKED" };

        private async Task ValidateUserFieldsAsync(AppUser model, long? currentUserId)
        {
            if (!AllowedRoles.Contains(model.Role))
            {
                ModelState.AddModelError("Role", "Vai trò không hợp lệ.");
            }

            if (currentUserId.HasValue && !AllowedStatuses.Contains(model.Status))
            {
                ModelState.AddModelError("Status", "Trạng thái không hợp lệ.");
            }

            if (model.StoreId.HasValue && !await _context.Stores.AnyAsync(s => s.Id == model.StoreId))
            {
                ModelState.AddModelError("StoreId", "Chi nhánh không tồn tại.");
            }

            if (!string.IsNullOrWhiteSpace(model.Email))
            {
                var email = model.Email.Trim();
                if (await _context.Users.AnyAsync(u => u.Email == email && u.Id != (currentUserId ?? 0)))
                {
                    ModelState.AddModelError("Email", "Email đã được dùng cho nhân viên khác.");
                }
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(long id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            user.Status = user.Status == "ACTIVE" ? "DISABLED" : "ACTIVE";
            user.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái nhân viên '{user.Username}' thành {user.Status}!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(long id, string newPassword)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var pwd = string.IsNullOrWhiteSpace(newPassword) ? "123456" : newPassword;
            user.Password = BCrypt.Net.BCrypt.HashPassword(pwd);
            user.FailedLoginAttempts = 0;
            user.LockedUntil = null;
            user.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã đặt lại mật khẩu cho '{user.Username}' thành: {pwd}";
            return RedirectToAction(nameof(Index));
        }
    }
}

