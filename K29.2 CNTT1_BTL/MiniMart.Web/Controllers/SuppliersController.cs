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
    [Authorize(Roles = "ADMIN,STORE_MANAGER")]
    public class SuppliersController : Controller
    {
        private readonly MiniMartDbContext _context;

        public SuppliersController(MiniMartDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? search)
        {
            var query = _context.Suppliers.Include(s => s.Products).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(sup => sup.Name.ToLower().Contains(s) ||
                                           sup.SupplierCode.ToLower().Contains(s) ||
                                           (sup.Phone != null && sup.Phone.Contains(s)) ||
                                           (sup.ContactPerson != null && sup.ContactPerson.ToLower().Contains(s)));
            }

            var suppliers = await query.OrderByDescending(s => s.CreatedAt).ToListAsync();
            ViewBag.Search = search;

            return View(suppliers);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var model = new Supplier
            {
                SupplierCode = "NCC" + (DateTime.Now.Ticks % 10000).ToString("D4"),
                Status = "ACTIVE"
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Supplier supplier)
        {
            if (await _context.Suppliers.AnyAsync(s => s.SupplierCode == supplier.SupplierCode))
            {
                ModelState.AddModelError("SupplierCode", "Mã nhà cung cấp đã tồn tại.");
            }

            if (ModelState.IsValid)
            {
                supplier.CreatedAt = DateTime.Now;
                supplier.UpdatedAt = DateTime.Now;

                _context.Suppliers.Add(supplier);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Thêm nhà cung cấp '{supplier.Name}' thành công!";
                return RedirectToAction(nameof(Index));
            }

            return View(supplier);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(long id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
            {
                return NotFound();
            }
            return View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, Supplier supplier)
        {
            if (id != supplier.Id)
            {
                return BadRequest();
            }

            var existing = await _context.Suppliers.FindAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                existing.Name = supplier.Name;
                existing.Address = supplier.Address;
                existing.Phone = supplier.Phone;
                existing.Email = supplier.Email;
                existing.TaxCode = supplier.TaxCode;
                existing.ContactPerson = supplier.ContactPerson;
                existing.Status = supplier.Status;
                existing.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Cập nhật nhà cung cấp '{existing.Name}' thành công!";
                return RedirectToAction(nameof(Index));
            }

            return View(supplier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(long id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
            {
                return NotFound();
            }

            supplier.Status = supplier.Status == "ACTIVE" ? "INACTIVE" : "ACTIVE";
            supplier.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái NCC '{supplier.Name}'!";
            return RedirectToAction(nameof(Index));
        }
    }
}

