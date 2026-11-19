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
    [Authorize(Roles = "ADMIN,STORE_MANAGER,CASHIER")]
    public class CustomersController : Controller
    {
        private readonly MiniMartDbContext _context;

        public CustomersController(MiniMartDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? search, string? tier)
        {
            var query = _context.Customers.Include(c => c.Invoices).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(c => c.FullName.ToLower().Contains(s) ||
                                         c.Phone.Contains(s) ||
                                         c.MemberCode.ToLower().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(tier) && tier != "ALL")
            {
                query = query.Where(c => c.MembershipTier == tier);
            }

            var customers = await query.OrderByDescending(c => c.TotalPoints).ToListAsync();

            ViewBag.Search = search;
            ViewBag.Tier = tier ?? "ALL";

            return View(customers);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var model = new Customer
            {
                MemberCode = "KH" + (DateTime.Now.Ticks % 100000).ToString("D5"),
                MembershipTier = "MEMBER",
                Status = "ACTIVE"
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            if (await _context.Customers.AnyAsync(c => c.MemberCode == customer.MemberCode))
            {
                ModelState.AddModelError("MemberCode", "Mã khách hàng đã tồn tại.");
            }

            if (await _context.Customers.AnyAsync(c => c.Phone == customer.Phone.Trim()))
            {
                ModelState.AddModelError("Phone", "Số điện thoại đã được đăng ký cho khách hàng khác.");
            }

            if (ModelState.IsValid)
            {
                customer.Phone = customer.Phone.Trim();
                customer.TotalPoints = 0;
                customer.AccumulatedPoints = 0;
                customer.MembershipTier = "MEMBER";
                customer.Status = "ACTIVE";
                customer.CreatedAt = DateTime.Now;
                customer.UpdatedAt = DateTime.Now;

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Đăng ký khách hàng '{customer.FullName}' thành công!";
                return RedirectToAction(nameof(Index));
            }

            return View(customer);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(long id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, Customer customer)
        {
            if (id != customer.Id)
            {
                return BadRequest();
            }

            var existing = await _context.Customers.FindAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            if (await _context.Customers.AnyAsync(c => c.Phone == customer.Phone.Trim() && c.Id != id))
            {
                ModelState.AddModelError("Phone", "Số điện thoại đã được đăng ký cho khách hàng khác.");
            }

            if (ModelState.IsValid)
            {
                existing.FullName = customer.FullName;
                existing.Phone = customer.Phone.Trim();
                existing.Email = customer.Email;
                existing.Address = customer.Address;
                existing.DateOfBirth = customer.DateOfBirth;
                existing.MembershipTier = customer.MembershipTier;
                existing.Status = customer.Status;
                existing.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Cập nhật khách hàng '{existing.FullName}' thành công!";
                return RedirectToAction(nameof(Index));
            }

            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(long id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound();
            }

            customer.Status = customer.Status == "ACTIVE" ? "DISABLED" : "ACTIVE";
            customer.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái khách hàng '{customer.FullName}'!";
            return RedirectToAction(nameof(Index));
        }
    }
}

