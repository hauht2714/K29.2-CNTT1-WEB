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
    [Authorize(Roles = "ADMIN,STORE_MANAGER,CASHIER")]
    public class InvoicesController : Controller
    {
        private readonly MiniMartDbContext _context;

        public InvoicesController(MiniMartDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(
            string? keyword,
            string? status,
            string? paymentMethod,
            string? timeRange,
            DateTime? fromDate,
            DateTime? toDate,
            int page = 1)
        {
            var query = _context.Invoices
                .Include(i => i.Store)
                .Include(i => i.Customer)
                .Include(i => i.Cashier)
                .AsQueryable();

            // Lọc theo từ khóa (Mã HĐ, Khách hàng, SĐT, Thu ngân)
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(i => i.InvoiceCode.ToLower().Contains(kw) ||
                                         (i.Customer != null && (i.Customer.FullName.ToLower().Contains(kw) || i.Customer.Phone.Contains(kw))) ||
                                         i.Cashier.FullName.ToLower().Contains(kw));
            }

            // Lọc theo trạng thái
            if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
            {
                query = query.Where(i => i.Status == status);
            }

            // Lọc theo phương thức thanh toán
            if (!string.IsNullOrWhiteSpace(paymentMethod) && paymentMethod != "ALL")
            {
                query = query.Where(i => i.PaymentMethod == paymentMethod);
            }

            // Lọc theo khoảng thời gian
            DateTime now = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(timeRange))
            {
                switch (timeRange)
                {
                    case "today":
                        var startOfToday = now.Date;
                        var endOfToday = startOfToday.AddDays(1).AddTicks(-1);
                        query = query.Where(i => i.CreatedAt >= startOfToday && i.CreatedAt <= endOfToday);
                        break;
                    case "yesterday":
                        var startOfYesterday = now.Date.AddDays(-1);
                        var endOfYesterday = now.Date.AddTicks(-1);
                        query = query.Where(i => i.CreatedAt >= startOfYesterday && i.CreatedAt <= endOfYesterday);
                        break;
                    case "7days":
                        var start7 = now.Date.AddDays(-7);
                        query = query.Where(i => i.CreatedAt >= start7);
                        break;
                    case "30days":
                        var start30 = now.Date.AddDays(-30);
                        query = query.Where(i => i.CreatedAt >= start30);
                        break;
                    case "thisMonth":
                        var startMonth = new DateTime(now.Year, now.Month, 1);
                        query = query.Where(i => i.CreatedAt >= startMonth);
                        break;
                }
            }
            else if (fromDate.HasValue || toDate.HasValue)
            {
                if (fromDate.HasValue)
                {
                    query = query.Where(i => i.CreatedAt >= fromDate.Value.Date);
                }
                if (toDate.HasValue)
                {
                    var endD = toDate.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(i => i.CreatedAt <= endD);
                }
            }

            // Thống kê nhanh
            var allFiltered = await query.ToListAsync();
            ViewBag.TotalCount = allFiltered.Count;
            ViewBag.CompletedCount = allFiltered.Count(i => i.Status == "COMPLETED");
            ViewBag.CancelledCount = allFiltered.Count(i => i.Status == "CANCELLED");
            ViewBag.TotalRevenue = allFiltered.Where(i => i.Status == "COMPLETED").Sum(i => i.TotalAmount);
            ViewBag.TotalDiscounts = allFiltered.Where(i => i.Status == "COMPLETED").Sum(i => i.DiscountAmount + i.PointsDiscount);

            // Phân trang
            int pageSize = 20;
            int totalItems = allFiltered.Count;
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var pagedInvoices = allFiltered
                .OrderByDescending(i => i.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.Keyword = keyword;
            ViewBag.Status = status;
            ViewBag.PaymentMethod = paymentMethod;
            ViewBag.TimeRange = timeRange;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            return View(pagedInvoices);
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv(
            string? keyword,
            string? status,
            string? paymentMethod,
            string? timeRange,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var query = _context.Invoices
                .Include(i => i.Store)
                .Include(i => i.Customer)
                .Include(i => i.Cashier)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(i => i.InvoiceCode.ToLower().Contains(kw) ||
                                         (i.Customer != null && (i.Customer.FullName.ToLower().Contains(kw) || i.Customer.Phone.Contains(kw))) ||
                                         i.Cashier.FullName.ToLower().Contains(kw));
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "ALL")
            {
                query = query.Where(i => i.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(paymentMethod) && paymentMethod != "ALL")
            {
                query = query.Where(i => i.PaymentMethod == paymentMethod);
            }

            DateTime now = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(timeRange))
            {
                switch (timeRange)
                {
                    case "today":
                        var startOfToday = now.Date;
                        var endOfToday = startOfToday.AddDays(1).AddTicks(-1);
                        query = query.Where(i => i.CreatedAt >= startOfToday && i.CreatedAt <= endOfToday);
                        break;
                    case "yesterday":
                        var startOfYesterday = now.Date.AddDays(-1);
                        var endOfYesterday = now.Date.AddTicks(-1);
                        query = query.Where(i => i.CreatedAt >= startOfYesterday && i.CreatedAt <= endOfYesterday);
                        break;
                    case "7days":
                        query = query.Where(i => i.CreatedAt >= now.Date.AddDays(-7));
                        break;
                    case "30days":
                        query = query.Where(i => i.CreatedAt >= now.Date.AddDays(-30));
                        break;
                    case "thisMonth":
                        query = query.Where(i => i.CreatedAt >= new DateTime(now.Year, now.Month, 1));
                        break;
                }
            }
            else if (fromDate.HasValue || toDate.HasValue)
            {
                if (fromDate.HasValue)
                {
                    query = query.Where(i => i.CreatedAt >= fromDate.Value.Date);
                }
                if (toDate.HasValue)
                {
                    var endD = toDate.Value.Date.AddDays(1).AddTicks(-1);
                    query = query.Where(i => i.CreatedAt <= endD);
                }
            }

            var invoices = await query.OrderByDescending(i => i.CreatedAt).ToListAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Mã hóa đơn,Cửa hàng,Ngày tạo,Thu ngân,Khách hàng,Số điện thoại,Phương thức thanh toán,Tạm tính,Giảm giá,Tiêu điểm,Tổng tiền,Trạng thái,Ghi chú/Lý do hủy");

            foreach (var inv in invoices)
            {
                var payStr = inv.PaymentMethod switch
                {
                    "CASH" => "Tiền mặt",
                    "CARD" => "Thẻ ngân hàng",
                    "TRANSFER" => "Chuyển khoản QR",
                    "BANK_TRANSFER" => "Chuyển khoản QR",
                    "E_WALLET" => "Ví điện tử",
                    _ => inv.PaymentMethod
                };
                var statusStr = inv.Status == "COMPLETED" ? "Hoàn thành" : "Đã hủy";
                var custName = inv.Customer?.FullName ?? "Khách lẻ";
                var custPhone = inv.Customer?.Phone ?? "";
                var storeName = inv.Store?.Name ?? "";
                var cashierName = inv.Cashier?.FullName ?? "";
                var cancelReason = (inv.CancelReason ?? "").Replace(",", ";").Replace("\r", "").Replace("\n", " ");

                sb.AppendLine($"\"{inv.InvoiceCode}\",\"{storeName}\",\"{inv.CreatedAt:dd/MM/yyyy HH:mm:ss}\",\"{cashierName}\",\"{custName}\",\"{custPhone}\",\"{payStr}\",{inv.Subtotal},{inv.DiscountAmount},{inv.PointsDiscount},{inv.TotalAmount},\"{statusStr}\",\"{cancelReason}\"");
            }

            var encoding = new System.Text.UTF8Encoding(true);
            byte[] fileBytes = encoding.GetBytes(sb.ToString());
            string fileName = $"Danh_sach_hoa_don_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            return File(fileBytes, "text/csv; charset=utf-8", fileName);
        }

        public async Task<IActionResult> Details(long id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Store)
                .Include(i => i.Customer)
                .Include(i => i.Cashier)
                .Include(i => i.InvoiceItems)
                    .ThenInclude(it => it.Product)
                .Include(i => i.InvoiceItems)
                    .ThenInclude(it => it.InvoiceItemBatches)
                        .ThenInclude(ib => ib.Batch)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy hóa đơn yêu cầu.";
                return RedirectToAction(nameof(Index));
            }

            return View(invoice);
        }

        [HttpPost]
        [Authorize(Roles = "ADMIN,STORE_MANAGER")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(long id, string cancelReason)
        {
            if (string.IsNullOrWhiteSpace(cancelReason))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập lý do hủy hóa đơn.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var invoice = await _context.Invoices
                .Include(i => i.Customer)
                .Include(i => i.InvoiceItems)
                    .ThenInclude(it => it.InvoiceItemBatches)
                        .ThenInclude(ib => ib.Batch)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy hóa đơn.";
                return RedirectToAction(nameof(Index));
            }

            if (invoice.Status == "CANCELLED")
            {
                TempData["ErrorMessage"] = "Hóa đơn này đã được hủy trước đó.";
                return RedirectToAction(nameof(Details), new { id });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Hoàn trả tồn kho và lô hàng
                foreach (var item in invoice.InvoiceItems)
                {
                    var sp = await _context.StoreProducts
                        .FirstOrDefaultAsync(s => s.StoreId == invoice.StoreId && s.ProductId == item.ProductId);
                    if (sp != null)
                    {
                        sp.StockQuantity += item.Quantity;
                        sp.UpdatedAt = DateTime.Now;
                    }

                    // Hoàn lại số lượng cho từng lô đã xuất
                    foreach (var ib in item.InvoiceItemBatches)
                    {
                        if (ib.Batch != null)
                        {
                            ib.Batch.RemainingQuantity = (ib.Batch.RemainingQuantity ?? 0) + ib.Quantity;
                        }
                    }

                    // Ghi nhận giao dịch hoàn trả kho
                    _context.InventoryTransactions.Add(new InventoryTransaction
                    {
                        StoreId = invoice.StoreId,
                        ProductId = item.ProductId,
                        Type = "RETURN",
                        Quantity = item.Quantity,
                        ReferenceId = invoice.Id,
                        Note = $"Hủy hóa đơn #{invoice.InvoiceCode}. Lý do: {cancelReason}",
                        CreatedAt = DateTime.Now
                    });
                }

                // 2. Xử lý điểm tích lũy của khách hàng
                if (invoice.Customer != null)
                {
                    // Trừ điểm đã tích lũy từ hóa đơn này (10.000đ = 1 điểm)
                    int pointsEarned = (int)(invoice.TotalAmount / 10000m);
                    invoice.Customer.TotalPoints = Math.Max(0, invoice.Customer.TotalPoints - pointsEarned);

                    // Trả lại điểm khách đã dùng giảm trừ (nếu có)
                    int pointsUsed = (int)(invoice.PointsDiscount / 1000m);
                    if (pointsUsed > 0)
                    {
                        invoice.Customer.TotalPoints += pointsUsed;
                    }

                    invoice.Customer.UpdatedAt = DateTime.Now;
                }

                // 3. Cập nhật trạng thái hóa đơn
                invoice.Status = "CANCELLED";
                invoice.CancelReason = cancelReason.Trim();
                invoice.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Đã hủy thành công hóa đơn #{invoice.InvoiceCode} và hoàn kho đầy đủ.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = $"Lỗi khi hủy hóa đơn: {ex.Message}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetReceiptData(long id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Store)
                .Include(i => i.Customer)
                .Include(i => i.Cashier)
                .Include(i => i.InvoiceItems)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null)
            {
                return Json(new { success = false, message = "Không tìm thấy hóa đơn." });
            }

            return Json(new
            {
                success = true,
                receipt = new
                {
                    invoiceCode = invoice.InvoiceCode,
                    storeName = invoice.Store?.Name ?? "MiniMart",
                    storeAddress = invoice.Store?.Address ?? "",
                    storePhone = invoice.Store?.Phone ?? "",
                    createdAt = invoice.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss"),
                    cashierName = invoice.Cashier?.FullName ?? "",
                    customerName = invoice.Customer?.FullName ?? "Khách lẻ",
                    customerPhone = invoice.Customer?.Phone ?? "",
                    paymentMethod = invoice.PaymentMethod switch
                    {
                        "CASH" => "Tiền mặt",
                        "CARD" => "Thẻ ngân hàng",
                        "TRANSFER" => "Chuyển khoản QR",
                        _ => invoice.PaymentMethod
                    },
                    subtotal = invoice.Subtotal,
                    discountAmount = invoice.DiscountAmount,
                    pointsDiscount = invoice.PointsDiscount,
                    totalAmount = invoice.TotalAmount,
                    cashReceived = invoice.CashReceived ?? invoice.TotalAmount,
                    cashChange = invoice.CashChange ?? 0,
                    status = invoice.Status,
                    items = invoice.InvoiceItems.Select(it => new
                    {
                        productName = it.ProductName,
                        quantity = it.Quantity,
                        unitPrice = it.UnitPrice,
                        subtotal = it.Subtotal
                    })
                }
            });
        }

        private long GetCurrentUserId()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return long.TryParse(idClaim, out var id) ? id : 1;
        }
    }
}
