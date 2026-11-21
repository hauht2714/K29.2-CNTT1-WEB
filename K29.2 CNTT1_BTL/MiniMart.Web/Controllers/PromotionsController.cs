using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniMart.Web.Data;
using MiniMart.Web.Models.Entities;
using MiniMart.Web.Models.ViewModels;

namespace MiniMart.Web.Controllers
{
    [Authorize(Roles = "ADMIN,STORE_MANAGER")]
    public class PromotionsController : Controller
    {
        private readonly MiniMartDbContext _context;

        public PromotionsController(MiniMartDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? keyword, string? status, string? type, int page = 1)
        {
            var query = _context.Promotions
                .Include(p => p.PromotionItems)
                    .ThenInclude(pi => pi.Product)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(kw) ||
                                         (p.Description != null && p.Description.ToLower().Contains(kw)));
            }

            if (!string.IsNullOrWhiteSpace(type) && type != "ALL")
            {
                query = query.Where(p => p.PromoType == type);
            }

            var now = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(status))
            {
                switch (status)
                {
                    case "RUNNING":
                        query = query.Where(p => p.IsActive && p.StartDate <= now && p.EndDate >= now);
                        break;
                    case "UPCOMING":
                        query = query.Where(p => p.IsActive && p.StartDate > now);
                        break;
                    case "EXPIRED":
                        query = query.Where(p => p.EndDate < now);
                        break;
                    case "INACTIVE":
                        query = query.Where(p => !p.IsActive);
                        break;
                }
            }

            var all = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();

            ViewBag.Keyword = keyword;
            ViewBag.Status = status;
            ViewBag.Type = type;

            ViewBag.TotalCount = all.Count;
            ViewBag.RunningCount = all.Count(p => p.IsActive && p.StartDate <= now && p.EndDate >= now);
            ViewBag.UpcomingCount = all.Count(p => p.IsActive && p.StartDate > now);

            int pageSize = 15;
            int totalPages = (int)Math.Ceiling((double)all.Count / pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var paged = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(paged);
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.Products = await _context.Products.Where(p => p.Status == "ACTIVE").OrderBy(p => p.Name).ToListAsync();
            return View(new PromotionDto
            {
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(7),
                PromoType = "PERCENTAGE",
                IsActive = true
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] PromotionDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Name))
            {
                return Json(new { success = false, message = "Vui lòng nhập tên chương trình khuyến mãi." });
            }

            var validationError = await ValidatePromotionAsync(model);
            if (validationError != null)
            {
                return Json(new { success = false, message = validationError });
            }

            long nextPromoId = (await _context.Promotions.MaxAsync(p => (long?)p.Id) ?? 0) + 1;
            var promotion = new Promotion
            {
                Id = nextPromoId,
                Name = model.Name.Trim(),
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                PromoType = model.PromoType,
                IsActive = model.IsActive,
                Description = model.Description?.Trim(),
                CreatedAt = DateTime.Now
            };

            _context.Promotions.Add(promotion);
            await _context.SaveChangesAsync();

            long nextItemId = (await _context.PromotionItems.MaxAsync(pi => (long?)pi.Id) ?? 0);
            foreach (var it in model.Items)
            {
                nextItemId++;
                _context.PromotionItems.Add(new PromotionItem
                {
                    Id = nextItemId,
                    PromotionId = promotion.Id,
                    ProductId = it.ProductId,
                    DiscountPercent = model.PromoType == "PERCENTAGE" ? it.DiscountPercent : null,
                    DiscountAmount = model.PromoType == "FIXED_AMOUNT" ? it.DiscountAmount : null,
                    BuyQty = model.PromoType == "BUY_X_GET_Y" ? it.BuyQty : null,
                    GetQty = model.PromoType == "BUY_X_GET_Y" ? it.GetQty : null
                });
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Tạo chương trình khuyến mãi thành công!"
            });
        }

        public async Task<IActionResult> Edit(long id)
        {
            var promo = await _context.Promotions
                .Include(p => p.PromotionItems)
                    .ThenInclude(pi => pi.Product)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (promo == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy chương trình khuyến mãi.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Products = await _context.Products.Where(p => p.Status == "ACTIVE").OrderBy(p => p.Name).ToListAsync();

            var dto = new PromotionDto
            {
                Id = promo.Id,
                Name = promo.Name,
                StartDate = promo.StartDate,
                EndDate = promo.EndDate,
                PromoType = promo.PromoType,
                IsActive = promo.IsActive,
                Description = promo.Description,
                Items = promo.PromotionItems.Select(pi => new PromotionItemDto
                {
                    Id = pi.Id,
                    ProductId = pi.ProductId ?? 0,
                    DiscountPercent = pi.DiscountPercent,
                    DiscountAmount = pi.DiscountAmount,
                    BuyQty = pi.BuyQty,
                    GetQty = pi.GetQty
                }).ToList()
            };

            return View(dto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [FromBody] PromotionDto model)
        {
            var promo = await _context.Promotions
                .Include(p => p.PromotionItems)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (promo == null)
            {
                return Json(new { success = false, message = "Không tìm thấy chương trình khuyến mãi." });
            }

            var validationError = await ValidatePromotionAsync(model);
            if (validationError != null)
            {
                return Json(new { success = false, message = validationError });
            }

            promo.Name = model.Name.Trim();
            promo.StartDate = model.StartDate;
            promo.EndDate = model.EndDate;
            promo.PromoType = model.PromoType;
            promo.IsActive = model.IsActive;
            promo.Description = model.Description?.Trim();

            // Xóa các mục cũ và thêm lại
            _context.PromotionItems.RemoveRange(promo.PromotionItems);

            if (model.Items != null)
            {
                long nextItemId = (await _context.PromotionItems.MaxAsync(pi => (long?)pi.Id) ?? 0);
                foreach (var it in model.Items)
                {
                    nextItemId++;
                    _context.PromotionItems.Add(new PromotionItem
                    {
                        Id = nextItemId,
                        PromotionId = promo.Id,
                        ProductId = it.ProductId,
                        DiscountPercent = model.PromoType == "PERCENTAGE" ? it.DiscountPercent : null,
                        DiscountAmount = model.PromoType == "FIXED_AMOUNT" ? it.DiscountAmount : null,
                        BuyQty = model.PromoType == "BUY_X_GET_Y" ? it.BuyQty : null,
                        GetQty = model.PromoType == "BUY_X_GET_Y" ? it.GetQty : null
                    });
                }
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Cập nhật chương trình khuyến mãi thành công!"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(long id)
        {
            var promo = await _context.Promotions.FindAsync(id);
            if (promo == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy chương trình khuyến mãi.";
                return RedirectToAction(nameof(Index));
            }

            promo.IsActive = !promo.IsActive;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã {(promo.IsActive ? "bật" : "tắt")} chương trình '{promo.Name}'.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(long id)
        {
            var promo = await _context.Promotions
                .Include(p => p.PromotionItems)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (promo == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy chương trình khuyến mãi.";
                return RedirectToAction(nameof(Index));
            }

            _context.PromotionItems.RemoveRange(promo.PromotionItems);
            _context.Promotions.Remove(promo);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã xóa chương trình '{promo.Name}'.";
            return RedirectToAction(nameof(Index));
        }

        private static readonly string[] AllowedPromoTypes = { "PERCENTAGE", "FIXED_AMOUNT", "BUY_X_GET_Y" };

        /// <summary>Trả về thông báo lỗi, hoặc null nếu dữ liệu khuyến mãi hợp lệ.</summary>
        private async Task<string?> ValidatePromotionAsync(PromotionDto? model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Name))
                return "Vui lòng nhập tên chương trình khuyến mãi.";

            if (model.EndDate.Date < model.StartDate.Date)
                return "Ngày kết thúc không được nhỏ hơn ngày bắt đầu.";

            if (!AllowedPromoTypes.Contains(model.PromoType))
                return "Loại khuyến mãi không hợp lệ.";

            if (model.Items == null || !model.Items.Any())
                return "Vui lòng thêm ít nhất một sản phẩm áp dụng khuyến mãi.";

            if (model.Items.GroupBy(i => i.ProductId).Any(g => g.Count() > 1))
                return "Mỗi sản phẩm chỉ được thêm một lần trong chương trình.";

            var productIds = model.Items.Select(i => i.ProductId).Distinct().ToList();
            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.DefaultPrice })
                .ToDictionaryAsync(p => p.Id);

            foreach (var it in model.Items)
            {
                if (!products.TryGetValue(it.ProductId, out var product))
                    return $"Sản phẩm mã #{it.ProductId} không tồn tại.";

                switch (model.PromoType)
                {
                    case "PERCENTAGE":
                        if (!it.DiscountPercent.HasValue || it.DiscountPercent <= 0 || it.DiscountPercent > 100)
                            return $"'{product.Name}': phần trăm giảm phải lớn hơn 0 và tối đa 100%.";
                        break;
                    case "FIXED_AMOUNT":
                        if (!it.DiscountAmount.HasValue || it.DiscountAmount <= 0)
                            return $"'{product.Name}': số tiền giảm phải lớn hơn 0.";
                        if (product.DefaultPrice.HasValue && it.DiscountAmount > product.DefaultPrice)
                            return $"'{product.Name}': số tiền giảm không được lớn hơn giá bán ({product.DefaultPrice:N0}đ).";
                        break;
                    case "BUY_X_GET_Y":
                        if (!it.BuyQty.HasValue || it.BuyQty < 1 || !it.GetQty.HasValue || it.GetQty < 1)
                            return $"'{product.Name}': số lượng mua và số lượng tặng phải từ 1 trở lên.";
                        break;
                }
            }

            return null;
        }
    }
}

