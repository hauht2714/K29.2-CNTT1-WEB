using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniMart.Web.Data;
using MiniMart.Web.Models.Entities;

namespace MiniMart.Web.Controllers
{
    [Authorize(Roles = "ADMIN,STORE_MANAGER,WAREHOUSE_STAFF")]
    public class InventoryController : Controller
    {
        private readonly MiniMartDbContext _context;

        public InventoryController(MiniMartDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(
            long? storeId,
            long? categoryId,
            string? keyword,
            string? stockFilter,
            int page = 1)
        {
            var userStoreId = GetCurrentStoreId();
            var isAdmin = User.IsInRole("ADMIN");

            // Nếu không phải admin thì chỉ được xem kho của chi nhánh mình
            var selectedStoreId = (isAdmin && storeId.HasValue && storeId.Value > 0) ? storeId.Value : userStoreId;

            var stores = await _context.Stores.Where(s => s.Status == "ACTIVE").OrderBy(s => s.Name).ToListAsync();
            var categories = await _context.Categories.Where(c => c.Status == "ACTIVE").OrderBy(c => c.Name).ToListAsync();

            ViewBag.Stores = stores;
            ViewBag.Categories = categories;
            ViewBag.SelectedStoreId = selectedStoreId;
            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.Keyword = keyword;
            ViewBag.StockFilter = stockFilter ?? "ALL";

            var now = DateTime.Now;
            var thirtyDaysFromNow = now.AddDays(30);

            var query = _context.StoreProducts
                .Include(sp => sp.Store)
                .Include(sp => sp.Product)
                    .ThenInclude(p => p.Category)
                .Include(sp => sp.ProductBatches)
                .Where(sp => sp.StoreId == selectedStoreId)
                .AsQueryable();

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(sp => sp.Product.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(sp => sp.Product.Name.ToLower().Contains(kw) ||
                                          sp.Product.ProductCode.ToLower().Contains(kw) ||
                                          (sp.Product.Barcode != null && sp.Product.Barcode.Contains(kw)));
            }

            var allItems = await query.ToListAsync();

            // Tính thống kê
            ViewBag.TotalItems = allItems.Count;
            ViewBag.TotalStock = allItems.Sum(sp => sp.StockQuantity);
            ViewBag.LowStockCount = allItems.Count(sp => sp.StockQuantity <= sp.MinStock && sp.StockQuantity > 0);
            ViewBag.OutOfStockCount = allItems.Count(sp => sp.StockQuantity == 0);
            ViewBag.NearExpiryCount = allItems.Count(sp => sp.ProductBatches.Any(b => (b.RemainingQuantity ?? 0) > 0 && b.ExpiryDate <= thirtyDaysFromNow && b.ExpiryDate >= now));
            ViewBag.ExpiredCount = allItems.Count(sp => sp.ProductBatches.Any(b => (b.RemainingQuantity ?? 0) > 0 && b.ExpiryDate < now));

            // Lọc theo bộ lọc tồn kho
            switch (stockFilter)
            {
                case "LOW":
                    allItems = allItems.Where(sp => sp.StockQuantity <= sp.MinStock && sp.StockQuantity > 0).ToList();
                    break;
                case "OUT":
                    allItems = allItems.Where(sp => sp.StockQuantity == 0).ToList();
                    break;
                case "EXPIRING":
                    allItems = allItems.Where(sp => sp.ProductBatches.Any(b => (b.RemainingQuantity ?? 0) > 0 && b.ExpiryDate <= thirtyDaysFromNow && b.ExpiryDate >= now)).ToList();
                    break;
                case "EXPIRED":
                    allItems = allItems.Where(sp => sp.ProductBatches.Any(b => (b.RemainingQuantity ?? 0) > 0 && b.ExpiryDate < now)).ToList();
                    break;
            }

            // Phân trang
            int pageSize = 20;
            int totalFiltered = allItems.Count;
            int totalPages = (int)Math.Ceiling((double)totalFiltered / pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var paged = allItems
                .OrderBy(sp => sp.StockQuantity <= sp.MinStock ? 0 : 1)
                .ThenBy(sp => sp.Product.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(paged);
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv(
            long? storeId,
            long? categoryId,
            string? keyword,
            string? stockFilter)
        {
            var userStoreId = GetCurrentStoreId();
            var isAdmin = User.IsInRole("ADMIN");
            var selectedStoreId = (isAdmin && storeId.HasValue && storeId.Value > 0) ? storeId.Value : userStoreId;

            var now = DateTime.Now;
            var thirtyDaysFromNow = now.AddDays(30);

            var query = _context.StoreProducts
                .Include(sp => sp.Store)
                .Include(sp => sp.Product)
                    .ThenInclude(p => p.Category)
                .Include(sp => sp.ProductBatches)
                .Where(sp => sp.StoreId == selectedStoreId)
                .AsQueryable();

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(sp => sp.Product.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(sp => sp.Product.Name.ToLower().Contains(kw) ||
                                          sp.Product.ProductCode.ToLower().Contains(kw) ||
                                          (sp.Product.Barcode != null && sp.Product.Barcode.Contains(kw)));
            }

            var allItems = await query.ToListAsync();

            switch (stockFilter)
            {
                case "LOW":
                    allItems = allItems.Where(sp => sp.StockQuantity <= sp.MinStock && sp.StockQuantity > 0).ToList();
                    break;
                case "OUT":
                    allItems = allItems.Where(sp => sp.StockQuantity == 0).ToList();
                    break;
                case "EXPIRING":
                    allItems = allItems.Where(sp => sp.ProductBatches.Any(b => (b.RemainingQuantity ?? 0) > 0 && b.ExpiryDate <= thirtyDaysFromNow && b.ExpiryDate >= now)).ToList();
                    break;
                case "EXPIRED":
                    allItems = allItems.Where(sp => sp.ProductBatches.Any(b => (b.RemainingQuantity ?? 0) > 0 && b.ExpiryDate < now)).ToList();
                    break;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Mã sản phẩm,Tên sản phẩm,Mã vạch (Barcode),Danh mục,Đơn vị tính,Đơn giá bán,Tồn kho hiện tại,Tồn tối thiểu,Tồn tối đa,Số lô hàng,Tình trạng cảnh báo");

            foreach (var sp in allItems)
            {
                var prod = sp.Product;
                var catName = prod.Category?.Name ?? "Khác";
                var hasExpired = sp.ProductBatches.Any(b => (b.RemainingQuantity ?? 0) > 0 && b.ExpiryDate < now);
                var isExpiring = sp.ProductBatches.Any(b => (b.RemainingQuantity ?? 0) > 0 && b.ExpiryDate <= thirtyDaysFromNow && b.ExpiryDate >= now);
                var alertStr = hasExpired ? "Có lô hết hạn" : (isExpiring ? "Cận date" : (sp.StockQuantity == 0 ? "Hết hàng" : (sp.StockQuantity <= sp.MinStock ? "Sắp hết hàng" : "Bình thường")));

                sb.AppendLine($"\"{prod.ProductCode}\",\"{prod.Name.Replace("\"", "\"\"")}\",\"{prod.Barcode}\",\"{catName}\",\"{prod.Unit}\",{sp.Price},{sp.StockQuantity},{sp.MinStock},{sp.MaxStock},{sp.ProductBatches.Count},\"{alertStr}\"");
            }

            var encoding = new System.Text.UTF8Encoding(true);
            byte[] fileBytes = encoding.GetBytes(sb.ToString());
            string fileName = $"Bao_cao_ton_kho_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            return File(fileBytes, "text/csv; charset=utf-8", fileName);
        }

        public async Task<IActionResult> Batches(long id)
        {
            var sp = await _context.StoreProducts
                .Include(s => s.Store)
                .Include(s => s.Product)
                .Include(s => s.ProductBatches)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sp == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy sản phẩm trong kho.";
                return RedirectToAction(nameof(Index));
            }

            return View(sp);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Adjust(long storeProductId, int newQuantity, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập lý do điều chỉnh tồn kho.";
                return RedirectToAction(nameof(Index));
            }

            if (newQuantity < 0)
            {
                TempData["ErrorMessage"] = "Số lượng tồn kho không thể âm.";
                return RedirectToAction(nameof(Index));
            }

            var sp = await _context.StoreProducts
                .Include(s => s.Product)
                .FirstOrDefaultAsync(s => s.Id == storeProductId);

            if (sp == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy sản phẩm kho.";
                return RedirectToAction(nameof(Index));
            }

            int diff = newQuantity - sp.StockQuantity;
            if (diff == 0)
            {
                TempData["ErrorMessage"] = "Số lượng mới giống số lượng hiện tại, không có gì để điều chỉnh.";
                return RedirectToAction(nameof(Index));
            }

            sp.StockQuantity = newQuantity;
            sp.UpdatedAt = DateTime.Now;

            // Đồng bộ lô hàng: hao hụt thì trừ lô hết hạn sớm nhất trước, thừa thì thêm một lô điều chỉnh
            if (diff < 0)
            {
                await StockBatchHelper.DeductAsync(_context, sp.Id, -diff);
            }
            else
            {
                StockBatchHelper.AddBatch(_context, sp, diff, sp.Product.LastImportPrice, null);
            }

            _context.InventoryTransactions.Add(new InventoryTransaction
            {
                StoreId = sp.StoreId,
                ProductId = sp.ProductId,
                Type = "ADJUSTMENT",
                Quantity = diff,
                ReferenceId = sp.Id,
                Note = $"Kiểm kê điều chỉnh kho ({diff:+#;-#;0}). Lý do: {reason.Trim()}",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã cập nhật số lượng tồn của '{sp.Product.Name}' thành {newQuantity} {sp.Product.Unit}.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> History(long? storeId, long? productId, string? type, int page = 1)
        {
            var userStoreId = GetCurrentStoreId();
            var isAdmin = User.IsInRole("ADMIN");
            var selectedStoreId = (isAdmin && storeId.HasValue && storeId.Value > 0) ? storeId.Value : userStoreId;

            var query = _context.InventoryTransactions
                .Include(t => t.Store)
                .Include(t => t.Product)
                .Where(t => t.StoreId == selectedStoreId)
                .AsQueryable();

            if (productId.HasValue && productId.Value > 0)
            {
                query = query.Where(t => t.ProductId == productId.Value);
            }

            if (!string.IsNullOrWhiteSpace(type) && type != "ALL")
            {
                query = query.Where(t => t.Type == type);
            }

            var stores = await _context.Stores.Where(s => s.Status == "ACTIVE").ToListAsync();
            ViewBag.Stores = stores;
            ViewBag.SelectedStoreId = selectedStoreId;
            ViewBag.SelectedProductId = productId;
            ViewBag.SelectedType = type ?? "ALL";

            var all = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

            int pageSize = 25;
            int totalPages = (int)Math.Ceiling((double)all.Count / pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var paged = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalRecords = all.Count;

            return View(paged);
        }

        private long GetCurrentStoreId()
        {
            var storeIdClaim = User.FindFirst("StoreId")?.Value;
            return long.TryParse(storeIdClaim, out var id) ? id : 1;
        }
    }
}

