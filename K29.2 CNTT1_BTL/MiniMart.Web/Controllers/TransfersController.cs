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
    public class TransfersController : Controller
    {
        private readonly MiniMartDbContext _context;

        public TransfersController(MiniMartDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? keyword, string? status, long? fromStoreId, long? toStoreId, int page = 1)
        {
            var userStoreId = GetCurrentStoreId();
            var isAdmin = User.IsInRole("ADMIN");

            var query = _context.StockTransfers
                .Include(t => t.FromStore)
                .Include(t => t.ToStore)
                .Include(t => t.StockTransferItems)
                    .ThenInclude(ti => ti.StoreProduct)
                        .ThenInclude(sp => sp!.Product)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(t => t.FromStoreId == userStoreId || t.ToStoreId == userStoreId);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(t => (t.TransferCode != null && t.TransferCode.ToLower().Contains(kw)) ||
                                         (t.Note != null && t.Note.ToLower().Contains(kw)));
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
            {
                query = query.Where(t => t.Status == status);
            }

            if (fromStoreId.HasValue && fromStoreId.Value > 0)
            {
                query = query.Where(t => t.FromStoreId == fromStoreId.Value);
            }

            if (toStoreId.HasValue && toStoreId.Value > 0)
            {
                query = query.Where(t => t.ToStoreId == toStoreId.Value);
            }

            var all = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

            var stores = await _context.Stores.Where(s => s.Status == "ACTIVE").ToListAsync();
            ViewBag.Stores = stores;
            ViewBag.Keyword = keyword;
            ViewBag.Status = status;
            ViewBag.FromStoreId = fromStoreId;
            ViewBag.ToStoreId = toStoreId;

            ViewBag.TotalCount = all.Count;
            ViewBag.CompletedCount = all.Count(t => t.Status == "COMPLETED");
            ViewBag.InTransitCount = all.Count(t => t.Status == "IN_TRANSIT" || t.Status == "PENDING");

            int pageSize = 15;
            int totalPages = (int)Math.Ceiling((double)all.Count / pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var paged = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(paged);
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv(string? keyword, string? status, long? fromStoreId, long? toStoreId)
        {
            var userStoreId = GetCurrentStoreId();
            var isAdmin = User.IsInRole("ADMIN");

            var query = _context.StockTransfers
                .Include(t => t.FromStore)
                .Include(t => t.ToStore)
                .Include(t => t.StockTransferItems)
                    .ThenInclude(ti => ti.StoreProduct)
                        .ThenInclude(sp => sp!.Product)
                .AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(t => t.FromStoreId == userStoreId || t.ToStoreId == userStoreId);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(t => (t.TransferCode != null && t.TransferCode.ToLower().Contains(kw)) ||
                                         (t.Note != null && t.Note.ToLower().Contains(kw)));
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
            {
                query = query.Where(t => t.Status == status);
            }

            if (fromStoreId.HasValue && fromStoreId.Value > 0)
            {
                query = query.Where(t => t.FromStoreId == fromStoreId.Value);
            }

            if (toStoreId.HasValue && toStoreId.Value > 0)
            {
                query = query.Where(t => t.ToStoreId == toStoreId.Value);
            }

            var all = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Mã phiếu chuyển,Chi nhánh xuất,Chi nhánh nhận,Ngày tạo,Số mặt hàng,Tổng số lượng,Trạng thái,Ghi chú");

            foreach (var t in all)
            {
                var statusStr = t.Status switch
                {
                    "COMPLETED" => "Hoàn thành",
                    "IN_TRANSIT" => "Đang vận chuyển",
                    "PENDING" => "Chờ duyệt",
                    "CANCELLED" => "Đã hủy",
                    _ => t.Status
                };
                var fromStoreName = t.FromStore?.Name ?? "";
                var toStoreName = t.ToStore?.Name ?? "";
                var totalQty = t.StockTransferItems.Sum(it => it.Quantity ?? 0);
                var note = (t.Note ?? "").Replace(",", ";").Replace("\r", "").Replace("\n", " ");

                sb.AppendLine($"\"{t.TransferCode}\",\"{fromStoreName}\",\"{toStoreName}\",\"{t.CreatedAt:dd/MM/yyyy HH:mm}\",{t.StockTransferItems.Count},{totalQty},\"{statusStr}\",\"{note}\"");
            }

            var encoding = new System.Text.UTF8Encoding(true);
            byte[] fileBytes = encoding.GetBytes(sb.ToString());
            string fileName = $"Danh_sach_chuyen_kho_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            return File(fileBytes, "text/csv; charset=utf-8", fileName);
        }

        public async Task<IActionResult> Create(long? fromStoreId)
        {
            var userStoreId = GetCurrentStoreId();
            var sourceStoreId = fromStoreId.HasValue && fromStoreId.Value > 0 ? fromStoreId.Value : userStoreId;

            var stores = await _context.Stores.Where(s => s.Status == "ACTIVE").ToListAsync();
            var availableProducts = await _context.StoreProducts
                .Include(sp => sp.Product)
                .Where(sp => sp.StoreId == sourceStoreId && sp.StockQuantity > 0)
                .OrderBy(sp => sp.Product.Name)
                .ToListAsync();

            ViewBag.Stores = stores;
            ViewBag.SourceStoreId = sourceStoreId;
            ViewBag.AvailableProducts = availableProducts;

            return View(new CreateTransferDto
            {
                FromStoreId = sourceStoreId,
                ToStoreId = stores.FirstOrDefault(s => s.Id != sourceStoreId)?.Id ?? 0
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetStoreProducts(long storeId)
        {
            var products = await _context.StoreProducts
                .Include(sp => sp.Product)
                .Where(sp => sp.StoreId == storeId && sp.StockQuantity > 0)
                .OrderBy(sp => sp.Product.Name)
                .Select(sp => new
                {
                    storeProductId = sp.Id,
                    productId = sp.ProductId,
                    productName = sp.Product.Name,
                    productCode = sp.Product.ProductCode,
                    unit = sp.Product.Unit,
                    stockQuantity = sp.StockQuantity,
                    price = sp.Price
                })
                .ToListAsync();

            return Json(products);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] CreateTransferDto model)
        {
            if (model == null || model.Items == null || !model.Items.Any())
            {
                return Json(new { success = false, message = "Vui lòng chọn ít nhất một sản phẩm cần chuyển kho." });
            }

            if (model.FromStoreId == model.ToStoreId)
            {
                return Json(new { success = false, message = "Chi nhánh xuất và chi nhánh nhận không được trùng nhau." });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var transferCode = "CK" + DateTime.Now.ToString("yyyyMMdd") + (DateTime.Now.Ticks % 10000).ToString("D4");
                var status = model.CompleteImmediately ? "COMPLETED" : "IN_TRANSIT";

                var transfer = new StockTransfer
                {
                    TransferCode = transferCode,
                    FromStoreId = model.FromStoreId,
                    ToStoreId = model.ToStoreId,
                    Status = status,
                    Note = model.Note?.Trim(),
                    CreatedAt = DateTime.Now
                };

                _context.StockTransfers.Add(transfer);
                await _context.SaveChangesAsync();

                var fromStore = await _context.Stores.FindAsync(model.FromStoreId);
                var toStore = await _context.Stores.FindAsync(model.ToStoreId);

                foreach (var item in model.Items)
                {
                    var sourceSp = await _context.StoreProducts
                        .Include(s => s.Product)
                        .FirstOrDefaultAsync(s => s.Id == item.StoreProductId);

                    if (sourceSp == null || sourceSp.StockQuantity < item.Quantity)
                    {
                        await transaction.RollbackAsync();
                        return Json(new { success = false, message = $"Sản phẩm '{sourceSp?.Product?.Name ?? "N/A"}' không đủ số lượng tồn để chuyển (Tồn: {sourceSp?.StockQuantity ?? 0})." });
                    }

                    // 1. Giảm tồn kho ở kho xuất
                    sourceSp.StockQuantity -= item.Quantity;
                    sourceSp.UpdatedAt = DateTime.Now;

                    // 2. Ghi nhận giao dịch xuất chuyển kho
                    _context.InventoryTransactions.Add(new InventoryTransaction
                    {
                        StoreId = model.FromStoreId,
                        ProductId = sourceSp.ProductId,
                        Type = "TRANSFER_OUT",
                        Quantity = -item.Quantity,
                        ReferenceId = transfer.Id,
                        Note = $"Chuyển kho #{transfer.TransferCode} tới chi nhánh {toStore?.Name}",
                        CreatedAt = DateTime.Now
                    });

                    // 3. Nếu hoàn tất ngay, tăng tồn kho ở kho nhận
                    if (model.CompleteImmediately)
                    {
                        var destSp = await _context.StoreProducts
                            .FirstOrDefaultAsync(s => s.StoreId == model.ToStoreId && s.ProductId == sourceSp.ProductId);

                        if (destSp == null)
                        {
                            destSp = new StoreProduct
                            {
                                StoreId = model.ToStoreId,
                                ProductId = sourceSp.ProductId,
                                Price = sourceSp.Price,
                                StockQuantity = item.Quantity,
                                MinStock = sourceSp.MinStock,
                                MaxStock = sourceSp.MaxStock,
                                Status = "ACTIVE",
                                CreatedAt = DateTime.Now,
                                UpdatedAt = DateTime.Now
                            };
                            _context.StoreProducts.Add(destSp);
                            await _context.SaveChangesAsync();
                        }
                        else
                        {
                            destSp.StockQuantity += item.Quantity;
                            destSp.UpdatedAt = DateTime.Now;
                        }

                        _context.InventoryTransactions.Add(new InventoryTransaction
                        {
                            StoreId = model.ToStoreId,
                            ProductId = sourceSp.ProductId,
                            Type = "TRANSFER_IN",
                            Quantity = item.Quantity,
                            ReferenceId = transfer.Id,
                            Note = $"Nhận chuyển kho #{transfer.TransferCode} từ chi nhánh {fromStore?.Name}",
                            CreatedAt = DateTime.Now
                        });
                    }

                    // Lưu mục chuyển kho
                    _context.StockTransferItems.Add(new StockTransferItem
                    {
                        TransferId = transfer.Id,
                        StoreProductId = sourceSp.Id,
                        Quantity = item.Quantity
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    message = $"Tạo phiếu chuyển kho #{transfer.TransferCode} thành công!",
                    transferId = transfer.Id
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Lỗi khi lưu phiếu chuyển kho: " + ex.Message });
            }
        }

        public async Task<IActionResult> Details(long id)
        {
            var transfer = await _context.StockTransfers
                .Include(t => t.FromStore)
                .Include(t => t.ToStore)
                .Include(t => t.StockTransferItems)
                    .ThenInclude(ti => ti.StoreProduct)
                        .ThenInclude(sp => sp!.Product)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transfer == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy phiếu chuyển kho.";
                return RedirectToAction(nameof(Index));
            }

            return View(transfer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Receive(long id)
        {
            var transfer = await _context.StockTransfers
                .Include(t => t.FromStore)
                .Include(t => t.ToStore)
                .Include(t => t.StockTransferItems)
                    .ThenInclude(ti => ti.StoreProduct)
                        .ThenInclude(sp => sp!.Product)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transfer == null || transfer.Status != "IN_TRANSIT")
            {
                TempData["ErrorMessage"] = "Phiếu chuyển kho không ở trạng thái đang vận chuyển.";
                return RedirectToAction(nameof(Details), new { id });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                transfer.Status = "COMPLETED";

                foreach (var item in transfer.StockTransferItems)
                {
                    if (item.StoreProduct == null) continue;

                    var prodId = item.StoreProduct.ProductId;
                    var qty = item.Quantity ?? 0;

                    var destSp = await _context.StoreProducts
                        .FirstOrDefaultAsync(s => s.StoreId == transfer.ToStoreId && s.ProductId == prodId);

                    if (destSp == null)
                    {
                        destSp = new StoreProduct
                        {
                            StoreId = transfer.ToStoreId!.Value,
                            ProductId = prodId,
                            Price = item.StoreProduct.Price,
                            StockQuantity = qty,
                            MinStock = item.StoreProduct.MinStock,
                            MaxStock = item.StoreProduct.MaxStock,
                            Status = "ACTIVE",
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };
                        _context.StoreProducts.Add(destSp);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        destSp.StockQuantity += qty;
                        destSp.UpdatedAt = DateTime.Now;
                    }

                    _context.InventoryTransactions.Add(new InventoryTransaction
                    {
                        StoreId = transfer.ToStoreId,
                        ProductId = prodId,
                        Type = "TRANSFER_IN",
                        Quantity = qty,
                        ReferenceId = transfer.Id,
                        Note = $"Nhận chuyển kho #{transfer.TransferCode} từ chi nhánh {transfer.FromStore?.Name}",
                        CreatedAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Đã xác nhận nhận đủ hàng phiếu chuyển #{transfer.TransferCode}.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "Lỗi khi nhận hàng: " + ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(long id, string? reason)
        {
            var transfer = await _context.StockTransfers
                .Include(t => t.StockTransferItems)
                    .ThenInclude(ti => ti.StoreProduct)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transfer == null || transfer.Status == "COMPLETED" || transfer.Status == "CANCELLED")
            {
                TempData["ErrorMessage"] = "Không thể hủy phiếu chuyển kho này.";
                return RedirectToAction(nameof(Index));
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Hoàn lại kho xuất
                foreach (var item in transfer.StockTransferItems)
                {
                    if (item.StoreProduct != null)
                    {
                        item.StoreProduct.StockQuantity += (item.Quantity ?? 0);
                        item.StoreProduct.UpdatedAt = DateTime.Now;

                        _context.InventoryTransactions.Add(new InventoryTransaction
                        {
                            StoreId = transfer.FromStoreId,
                            ProductId = item.StoreProduct.ProductId,
                            Type = "RETURN",
                            Quantity = item.Quantity ?? 0,
                            ReferenceId = transfer.Id,
                            Note = $"Hủy phiếu chuyển kho #{transfer.TransferCode}. Lý do: {reason}",
                            CreatedAt = DateTime.Now
                        });
                    }
                }

                transfer.Status = "CANCELLED";
                transfer.Note = string.IsNullOrEmpty(transfer.Note) ? reason : $"{transfer.Note} (Lý do hủy: {reason})";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Đã hủy phiếu chuyển #{transfer.TransferCode} và hoàn tồn về kho xuất.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "Lỗi khi hủy phiếu: " + ex.Message;
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        private long GetCurrentStoreId()
        {
            var storeIdClaim = User.FindFirst("StoreId")?.Value;
            return long.TryParse(storeIdClaim, out var id) ? id : 1;
        }
    }
}

