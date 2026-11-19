using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniMart.Web.Data;
using MiniMart.Web.Models.Entities;

namespace MiniMart.Web.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class StoresController : Controller
    {
        private readonly MiniMartDbContext _context;

        public StoresController(MiniMartDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? search, string? status)
        {
            var query = _context.Stores.Include(s => s.Users).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(st => st.Name.ToLower().Contains(s) || 
                                          st.StoreCode.ToLower().Contains(s) || 
                                          (st.Phone != null && st.Phone.Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
            {
                query = query.Where(st => st.Status == status);
            }

            var stores = await query.OrderByDescending(st => st.CreatedAt).ToListAsync();

            ViewBag.Search = search;
            ViewBag.Status = status ?? "ALL";

            return View(stores);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var model = new Store
            {
                StoreCode = "ST" + (DateTime.Now.Ticks % 100000).ToString("D5"),
                Status = "ACTIVE",
                StoreType = "MINI_MART"
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Store store)
        {
            if (await _context.Stores.AnyAsync(s => s.StoreCode == store.StoreCode))
            {
                ModelState.AddModelError("StoreCode", "Mã chi nhánh đã tồn tại.");
            }

            if (ModelState.IsValid)
            {
                store.CreatedAt = DateTime.Now;
                store.UpdatedAt = DateTime.Now;
                _context.Stores.Add(store);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Thêm chi nhánh '{store.Name}' thành công!";
                return RedirectToAction(nameof(Index));
            }

            return View(store);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(long id)
        {
            var store = await _context.Stores.FindAsync(id);
            if (store == null)
            {
                return NotFound();
            }
            return View(store);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, Store store)
        {
            if (id != store.Id)
            {
                return BadRequest();
            }

            var existing = await _context.Stores.FindAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                existing.Name = store.Name;
                existing.Address = store.Address;
                existing.Phone = store.Phone;
                existing.Email = store.Email;
                existing.StoreType = store.StoreType;
                existing.Status = store.Status;
                existing.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Cập nhật chi nhánh '{existing.Name}' thành công!";
                return RedirectToAction(nameof(Index));
            }

            return View(store);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(long id)
        {
            var store = await _context.Stores.FindAsync(id);
            if (store == null)
            {
                return NotFound();
            }

            store.Status = store.Status == "ACTIVE" ? "INACTIVE" : "ACTIVE";
            store.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái chi nhánh '{store.Name}' thành {store.Status}!";
            return RedirectToAction(nameof(Index));
        }
    }
}

