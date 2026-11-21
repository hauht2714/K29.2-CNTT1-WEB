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
using MiniMart.Web.Models.ViewModels;

namespace MiniMart.Web.Controllers
{
    [Authorize(Roles = "ADMIN,STORE_MANAGER,WAREHOUSE_STAFF")]
    public class ImportController : Controller
    {
        private readonly MiniMartDbContext _context;

        public ImportController(MiniMartDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? keyword, string? status, long? supplierId, int page = 1)
        {
            var userStoreId = GetCurrentStoreId();
            var isAdmin = User.IsInRole("ADMIN");

            var query = _context.ImportReceipts
                .Include(r => r.Supplier)
                .Include(r => r.Store)
                .Include(r => r.CreatedByUser)
                .Include(r => r.ImportReceiptItems)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(r => r.StoreId == userStoreId);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(r => r.ReceiptCode.ToLower().Contains(kw) ||
                                         r.Supplier.Name.ToLower().Contains(kw) ||
                                         r.CreatedByUser.FullName.ToLower().Contains(kw));
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
            {
                query = query.Where(r => r.Status == status);
            }

            if (supplierId.HasValue && supplierId.Value > 0)
            {
                query = query.Where(r => r.SupplierId == supplierId.Value);
            }

            var all = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();

            ViewBag.Suppliers = await _context.Suppliers.Where(s => s.Status == "ACTIVE").OrderBy(s => s.Name).ToListAsync();
            ViewBag.Keyword = keyword;
            ViewBag.Status = status;
            ViewBag.SupplierId = supplierId;

            ViewBag.TotalCount = all.Count;
            ViewBag.CompletedCount = all.Count(r => r.Status == "COMPLETED");
            ViewBag.TotalImportAmount = all.Where(r => r.Status == "COMPLETED").Sum(r => r.TotalAmount);

            int pageSize = 15;
            int totalPages = (int)Math.Ceiling((double)all.Count / pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var paged = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(paged);
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv(string? keyword, string? status, long? supplierId)
        {
            var userStoreId = GetCurrentStoreId();
            var isAdmin = User.IsInRole("ADMIN");

            var query = _context.ImportReceipts
                .Include(r => r.Supplier)
                .Include(r => r.Store)
                .Include(r => r.CreatedByUser)
                .Include(r => r.ImportReceiptItems)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(r => r.StoreId == userStoreId);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(r => r.ReceiptCode.ToLower().Contains(kw) ||
                                         r.Supplier.Name.ToLower().Contains(kw) ||
                                         r.CreatedByUser.FullName.ToLower().Contains(kw));
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
            {
                query = query.Where(r => r.Status == status);
            }

            if (supplierId.HasValue && supplierId.Value > 0)
            {
                query = query.Where(r => r.SupplierId == supplierId.Value);
            }

            var all = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Mã phiếu nhập,Chi nhánh nhập,Nhà cung cấp,Ngày nhập,Người tạo,Tổng tiền,Số mặt hàng,Trạng thái,Ghi chú");

            foreach (var r in all)
            {
                var statusStr = r.Status switch
                {
                    "COMPLETED" => "Hoàn thành",
                    "DRAFT" => "Bản nháp",
                    "CANCELLED" => "Đã hủy",
                    _ => r.Status
                };
                var storeName = r.Store?.Name ?? "";
                var suppName = r.Supplier?.Name ?? "";
                var creator = r.CreatedByUser?.FullName ?? "";
                var note = (r.Note ?? "").Replace(",", ";").Replace("\r", "").Replace("\n", " ");

                sb.AppendLine($"\"{r.ReceiptCode}\",\"{storeName}\",\"{suppName}\",\"{r.ImportDate:dd/MM/yyyy}\",\"{creator}\",{r.TotalAmount},{r.ImportReceiptItems.Count},\"{statusStr}\",\"{note}\"");
            }

            var encoding = new System.Text.UTF8Encoding(true);
            byte[] fileBytes = encoding.GetBytes(sb.ToString());
            string fileName = $"Danh_sach_phieu_nhap_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            return File(fileBytes, "text/csv; charset=utf-8", fileName);
        }

        public async Task<IActionResult> Create()
        {
            var userStoreId = GetCurrentStoreId();
            ViewBag.Suppliers = await _context.Suppliers.Where(s => s.Status == "ACTIVE").OrderBy(s => s.Name).ToListAsync();
            ViewBag.Stores = await _context.Stores.Where(s => s.Status == "ACTIVE").OrderBy(s => s.Name).ToListAsync();
            ViewBag.Products = await _context.Products.Where(p => p.Status == "ACTIVE").OrderBy(p => p.Name).ToListAsync();
            ViewBag.UserStoreId = userStoreId;

            return View(new CreateImportReceiptDto
            {
                StoreId = userStoreId,
                ImportDate = DateTime.Today
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] CreateImportReceiptDto model)
        {
            if (model == null || model.Items == null || !model.Items.Any())
            {
                return Json(new { success = false, message = "Vui lòng thêm ít nhất một sản phẩm cần nhập kho." });
            }

            var userId = GetCurrentUserId();
            var targetStoreId = model.StoreId > 0 ? model.StoreId : GetCurrentStoreId();

            var supplier = await _context.Suppliers.FindAsync(model.SupplierId);
            if (supplier == null)
            {
                return Json(new { success = false, message = "Không tìm thấy nhà cung cấp đã chọn." });
            }

            if (!await _context.Stores.AnyAsync(s => s.Id == targetStoreId))
            {
                return Json(new { success = false, message = "Không tìm thấy chi nhánh nhận hàng." });
            }

            if (model.Items.Any(i => i.Quantity < 1 || i.Quantity > 100000))
            {
                return Json(new { success = false, message = "Số lượng nhập của mỗi sản phẩm phải từ 1 đến 100.000." });
            }

            if (model.Items.Any(i => i.ImportPrice < 0 || i.ImportPrice > 1000000000))
            {
                return Json(new { success = false, message = "Giá nhập không hợp lệ (phải từ 0 trở lên)." });
            }

            var requestedProductIds = model.Items.Select(i => i.ProductId).Distinct().ToList();
            var existingProductCount = await _context.Products.CountAsync(p => requestedProductIds.Contains(p.Id));
            if (existingProductCount != requestedProductIds.Count)
            {
                return Json(new { success = false, message = "Có sản phẩm trong phiếu không tồn tại. Vui lòng tải lại trang và chọn lại." });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var receiptCode = "PN" + DateTime.Now.ToString("yyyyMMdd") + (DateTime.Now.Ticks % 10000).ToString("D4");
                decimal totalAmount = model.Items.Sum(i => i.Quantity * i.ImportPrice);

                var receipt = new ImportReceipt
                {
                    ReceiptCode = receiptCode,
                    SupplierId = model.SupplierId,
                    StoreId = targetStoreId,
                    ImportDate = model.ImportDate,
                    TotalAmount = totalAmount,
                    Status = model.CompleteImmediately ? "COMPLETED" : "DRAFT",
                    Note = model.Note?.Trim(),
                    CreatedBy = userId,
                    ConfirmedBy = model.CompleteImmediately ? userId : null,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.ImportReceipts.Add(receipt);
                await _context.SaveChangesAsync();

                foreach (var item in model.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null) continue;

                    var receiptItem = new ImportReceiptItem
                    {
                        ReceiptId = receipt.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        ImportPrice = item.ImportPrice,
                        Subtotal = item.Quantity * item.ImportPrice,
                        ExpiryDate = item.ExpiryDate
                    };
                    _context.ImportReceiptItems.Add(receiptItem);

                    // Cập nhật giá nhập gần nhất của sản phẩm
                    product.LastImportPrice = item.ImportPrice;

                    // Nếu hoàn thành nhập kho ngay, cập nhật tồn kho và tạo lô
                    if (model.CompleteImmediately)
                    {
                        var sp = await _context.StoreProducts
                            .FirstOrDefaultAsync(s => s.StoreId == targetStoreId && s.ProductId == item.ProductId);

                        if (sp == null)
                        {
                            sp = new StoreProduct
                            {
                                StoreId = targetStoreId,
                                ProductId = item.ProductId,
                                Price = product.DefaultPrice ?? (item.ImportPrice * 1.2m),
                                StockQuantity = item.Quantity,
                                MinStock = 10,
                                MaxStock = 1000,
                                Status = "ACTIVE",
                                CreatedAt = DateTime.Now,
                                UpdatedAt = DateTime.Now
                            };
                            _context.StoreProducts.Add(sp);
                            await _context.SaveChangesAsync();
                        }
                        else
                        {
                            sp.StockQuantity += item.Quantity;
                            sp.UpdatedAt = DateTime.Now;
                        }

                        // Tạo lô hàng mới (ProductBatch)
                        var batchBarcode = StockBatchHelper.NewBatchBarcode();
                        var batch = new ProductBatch
                        {
                            StoreProductId = sp.Id,
                            ImportDate = model.ImportDate,
                            ExpiryDate = item.ExpiryDate,
                            Quantity = item.Quantity,
                            RemainingQuantity = item.Quantity,
                            ImportPrice = item.ImportPrice,
                            BatchBarcode = batchBarcode
                        };
                        _context.ProductBatches.Add(batch);

                        // Ghi nhận nhật ký kho
                        _context.InventoryTransactions.Add(new InventoryTransaction
                        {
                            StoreId = targetStoreId,
                            ProductId = item.ProductId,
                            Type = "IMPORT",
                            Quantity = item.Quantity,
                            ReferenceId = receipt.Id,
                            Note = $"Nhập kho PN #{receipt.ReceiptCode} từ {supplier.Name}",
                            CreatedAt = DateTime.Now
                        });
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    message = $"Tạo phiếu nhập #{receipt.ReceiptCode} thành công!",
                    receiptId = receipt.Id
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Lỗi khi lưu phiếu nhập kho: " + ex.Message });
            }
        }

        public async Task<IActionResult> Details(long id)
        {
            var receipt = await _context.ImportReceipts
                .Include(r => r.Supplier)
                .Include(r => r.Store)
                .Include(r => r.CreatedByUser)
                .Include(r => r.ConfirmedByUser)
                .Include(r => r.CancelledByUser)
                .Include(r => r.ImportReceiptItems)
                    .ThenInclude(it => it.Product)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (receipt == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy phiếu nhập kho.";
                return RedirectToAction(nameof(Index));
            }

            return View(receipt);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(long id)
        {
            var receipt = await _context.ImportReceipts
                .Include(r => r.Supplier)
                .Include(r => r.ImportReceiptItems)
                    .ThenInclude(it => it.Product)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (receipt == null || receipt.Status != "DRAFT")
            {
                TempData["ErrorMessage"] = "Phiếu nhập không hợp lệ hoặc đã được xử lý trước đó.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var userId = GetCurrentUserId();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                receipt.Status = "COMPLETED";
                receipt.ConfirmedBy = userId;
                receipt.UpdatedAt = DateTime.Now;

                foreach (var item in receipt.ImportReceiptItems)
                {
                    var sp = await _context.StoreProducts
                        .FirstOrDefaultAsync(s => s.StoreId == receipt.StoreId && s.ProductId == item.ProductId);

                    if (sp == null)
                    {
                        sp = new StoreProduct
                        {
                            StoreId = receipt.StoreId,
                            ProductId = item.ProductId,
                            Price = item.Product.DefaultPrice ?? (item.ImportPrice * 1.2m),
                            StockQuantity = item.Quantity,
                            MinStock = 10,
                            MaxStock = 1000,
                            Status = "ACTIVE",
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };
                        _context.StoreProducts.Add(sp);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        sp.StockQuantity += item.Quantity;
                        sp.UpdatedAt = DateTime.Now;
                    }

                    var batchBarcode = StockBatchHelper.NewBatchBarcode();
                    _context.ProductBatches.Add(new ProductBatch
                    {
                        StoreProductId = sp.Id,
                        ImportDate = receipt.ImportDate,
                        ExpiryDate = item.ExpiryDate,
                        Quantity = item.Quantity,
                        RemainingQuantity = item.Quantity,
                        ImportPrice = item.ImportPrice,
                        BatchBarcode = batchBarcode
                    });

                    _context.InventoryTransactions.Add(new InventoryTransaction
                    {
                        StoreId = receipt.StoreId,
                        ProductId = item.ProductId,
                        Type = "IMPORT",
                        Quantity = item.Quantity,
                        ReferenceId = receipt.Id,
                        Note = $"Xác nhận nhập kho PN #{receipt.ReceiptCode} từ {receipt.Supplier.Name}",
                        CreatedAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Đã duyệt nhập kho phiếu #{receipt.ReceiptCode} thành công.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "Lỗi khi xác nhận phiếu nhập: " + ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(long id, string? reason)
        {
            var receipt = await _context.ImportReceipts.FindAsync(id);
            if (receipt == null || receipt.Status == "CANCELLED")
            {
                TempData["ErrorMessage"] = "Phiếu nhập không hợp lệ hoặc đã hủy.";
                return RedirectToAction(nameof(Index));
            }

            if (receipt.Status == "COMPLETED")
            {
                TempData["ErrorMessage"] = "Không thể hủy phiếu nhập đã hoàn thành (hàng đã vào kho). Hãy kiểm kê điều chỉnh tồn kho thay thế.";
                return RedirectToAction(nameof(Details), new { id });
            }

            receipt.Status = "CANCELLED";
            receipt.CancelledBy = GetCurrentUserId();
            receipt.Note = string.IsNullOrEmpty(receipt.Note) ? reason : $"{receipt.Note} (Lý do hủy: {reason})";
            receipt.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã hủy phiếu nhập nháp #{receipt.ReceiptCode}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        private long GetCurrentUserId()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return long.TryParse(idClaim, out var id) ? id : 1;
        }

        private long GetCurrentStoreId()
        {
            var storeIdClaim = User.FindFirst("StoreId")?.Value;
            return long.TryParse(storeIdClaim, out var id) ? id : 1;
        }
    }
}

