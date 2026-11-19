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
    public class CategoriesController : Controller
    {
        private readonly MiniMartDbContext _context;

        public CategoriesController(MiniMartDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var categories = await _context.Categories
                .Include(c => c.Products)
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(categories);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category)
        {
            if (ModelState.IsValid)
            {
                category.Status = "ACTIVE";
                category.CategoryLevel = 1;
                category.CreatedAt = DateTime.Now;
                category.UpdatedAt = DateTime.Now;

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Thêm danh mục '{category.Name}' thành công!";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, Category category)
        {
            var existing = await _context.Categories.FindAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            existing.Name = category.Name;
            existing.Description = category.Description;
            existing.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Cập nhật danh mục '{existing.Name}' thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(long id)
        {
            var category = await _context.Categories.Include(c => c.Products).FirstOrDefaultAsync(c => c.Id == id);
            if (category == null)
            {
                return NotFound();
            }

            if (category.Products.Any())
            {
                TempData["ErrorMessage"] = $"Không thể xóa danh mục '{category.Name}' vì đang có {category.Products.Count} sản phẩm liên kết!";
                return RedirectToAction(nameof(Index));
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã xóa danh mục '{category.Name}'!";
            return RedirectToAction(nameof(Index));
        }
    }
}

