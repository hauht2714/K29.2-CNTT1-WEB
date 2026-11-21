using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MiniMart.Web.Data;
using MiniMart.Web.Models.Entities;

namespace MiniMart.Web.Controllers
{
    [Authorize(Roles = "ADMIN,STORE_MANAGER")]
    public class ProductsController : Controller
    {
        private readonly MiniMartDbContext _context;

        public ProductsController(MiniMartDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? search, long? categoryId, string? status)
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Supplier)
                .Include(p => p.StoreProducts)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(s) || 
                                         p.ProductCode.ToLower().Contains(s) || 
                                         (p.Barcode != null && p.Barcode.Contains(s)));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(p => p.CategoryId == categoryId);
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
            {
                query = query.Where(p => p.Status == status);
            }

            var products = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
            var categories = await _context.Categories.Where(c => c.Status == "ACTIVE").ToListAsync();

            ViewBag.Search = search;
            ViewBag.CategoryId = categoryId;
            ViewBag.Status = status ?? "ALL";
            ViewBag.Categories = categories;

            return View(products);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = new SelectList(await _context.Categories.Where(c => c.Status == "ACTIVE").ToListAsync(), "Id", "Name");
            ViewBag.Suppliers = new SelectList(await _context.Suppliers.Where(s => s.Status == "ACTIVE").ToListAsync(), "Id", "Name");

            var model = new Product
            {
                ProductCode = "SP" + (DateTime.Now.Ticks % 100000).ToString("D5"),
                Unit = "Cái",
                Status = "ACTIVE"
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, int initialStock = 0)
        {
            if (await _context.Products.AnyAsync(p => p.ProductCode == product.ProductCode))
            {
                ModelState.AddModelError("ProductCode", "Mã sản phẩm đã tồn tại.");
            }

            if (!string.IsNullOrWhiteSpace(product.Barcode) && await _context.Products.AnyAsync(p => p.Barcode == product.Barcode))
            {
                ModelState.AddModelError("Barcode", "Mã vạch đã được gán cho sản phẩm khác.");
            }

            if (product.DefaultPrice.HasValue && product.DefaultPrice < 0)
            {
                ModelState.AddModelError("DefaultPrice", "Giá bán không được âm.");
            }

            if (initialStock < 0 || initialStock > 100000)
            {
                ModelState.AddModelError(string.Empty, "Tồn kho ban đầu phải từ 0 đến 100.000.");
            }

            if (ModelState.IsValid)
            {
                product.CreatedAt = DateTime.Now;
                product.UpdatedAt = DateTime.Now;
                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                // Lấy chi nhánh hiện tại của user đăng nhập
                var storeIdClaim = User.FindFirstValue("StoreId");
                long storeId = 0;
                if (!long.TryParse(storeIdClaim, out storeId) || storeId == 0)
                {
                    var firstStore = await _context.Stores.FirstOrDefaultAsync();
                    storeId = firstStore?.Id ?? 1;
                }

                // Tự động tạo bản ghi StoreProduct
                var storeProduct = new StoreProduct
                {
                    StoreId = storeId,
                    ProductId = product.Id,
                    Price = product.DefaultPrice ?? 0,
                    StockQuantity = initialStock,
                    MinStock = 10,
                    MaxStock = 1000,
                    Status = "ACTIVE",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _context.StoreProducts.Add(storeProduct);
                await _context.SaveChangesAsync();

                // Nếu có tồn kho ban đầu, tạo 1 lô hàng mặc định
                if (initialStock > 0)
                {
                    var batch = new ProductBatch
                    {
                        StoreProductId = storeProduct.Id,
                        ImportDate = DateTime.Today,
                        ExpiryDate = DateTime.Today.AddMonths(12),
                        Quantity = initialStock,
                        RemainingQuantity = initialStock,
                        ImportPrice = (product.DefaultPrice ?? 0) * 0.75m,
                        BatchBarcode = $"BATCH-{product.ProductCode}-{DateTime.Today:yyyyMMdd}"
                    };
                    _context.ProductBatches.Add(batch);
                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = $"Thêm sản phẩm '{product.Name}' thành công!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Categories = new SelectList(await _context.Categories.Where(c => c.Status == "ACTIVE").ToListAsync(), "Id", "Name", product.CategoryId);
            ViewBag.Suppliers = new SelectList(await _context.Suppliers.Where(s => s.Status == "ACTIVE").ToListAsync(), "Id", "Name", product.SupplierId);
            return View(product);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(long id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            ViewBag.Categories = new SelectList(await _context.Categories.Where(c => c.Status == "ACTIVE").ToListAsync(), "Id", "Name", product.CategoryId);
            ViewBag.Suppliers = new SelectList(await _context.Suppliers.Where(s => s.Status == "ACTIVE").ToListAsync(), "Id", "Name", product.SupplierId);
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, Product product)
        {
            if (id != product.Id)
            {
                return BadRequest();
            }

            var existing = await _context.Products.FindAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrWhiteSpace(product.Barcode) && 
                await _context.Products.AnyAsync(p => p.Barcode == product.Barcode && p.Id != id))
            {
                ModelState.AddModelError("Barcode", "Mã vạch đã được gán cho sản phẩm khác.");
            }

            if (product.DefaultPrice.HasValue && product.DefaultPrice < 0)
            {
                ModelState.AddModelError("DefaultPrice", "Giá bán không được âm.");
            }

            if (ModelState.IsValid)
            {
                existing.Name = product.Name;
                existing.Description = product.Description;
                existing.CategoryId = product.CategoryId;
                existing.SupplierId = product.SupplierId;
                existing.Unit = product.Unit;
                existing.Barcode = product.Barcode;
                existing.DefaultPrice = product.DefaultPrice;
                existing.Status = product.Status;
                existing.UpdatedAt = DateTime.Now;

                // Cập nhật giá bán ở StoreProduct nếu có
                var storeProducts = await _context.StoreProducts.Where(sp => sp.ProductId == id).ToListAsync();
                foreach (var sp in storeProducts)
                {
                    if (product.DefaultPrice.HasValue)
                    {
                        sp.Price = product.DefaultPrice.Value;
                    }
                    sp.UpdatedAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Cập nhật sản phẩm '{existing.Name}' thành công!";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Categories = new SelectList(await _context.Categories.Where(c => c.Status == "ACTIVE").ToListAsync(), "Id", "Name", product.CategoryId);
            ViewBag.Suppliers = new SelectList(await _context.Suppliers.Where(s => s.Status == "ACTIVE").ToListAsync(), "Id", "Name", product.SupplierId);
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(long id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            product.Status = product.Status == "ACTIVE" ? "DISCONTINUED" : "ACTIVE";
            product.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái sản phẩm '{product.Name}'!";
            return RedirectToAction(nameof(Index));
        }
    }
}

