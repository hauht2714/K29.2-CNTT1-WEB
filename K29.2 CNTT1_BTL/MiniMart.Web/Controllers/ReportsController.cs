using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniMart.Web.Data;
using MiniMart.Web.Models.ViewModels;

namespace MiniMart.Web.Controllers
{
    [Authorize(Roles = "ADMIN,STORE_MANAGER")]
    public class ReportsController : Controller
    {
        private readonly MiniMartDbContext _context;

        public ReportsController(MiniMartDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(
            string? timeRange,
            DateTime? fromDate,
            DateTime? toDate,
            long? storeId)
        {
            var userStoreId = GetCurrentStoreId();
            var isAdmin = User.IsInRole("ADMIN");
            var selectedStoreId = (isAdmin && storeId.HasValue && storeId.Value > 0) ? storeId.Value : userStoreId;

            var stores = await _context.Stores.Where(s => s.Status == "ACTIVE").OrderBy(s => s.Name).ToListAsync();
            ViewBag.Stores = stores;
            ViewBag.SelectedStoreId = selectedStoreId;

            // Xác định khoảng thời gian
            DateTime now = DateTime.Now;
            DateTime start = new DateTime(now.Year, now.Month, 1);
            DateTime end = now.Date.AddDays(1).AddTicks(-1);

            timeRange = string.IsNullOrEmpty(timeRange) ? "thisMonth" : timeRange;

            switch (timeRange)
            {
                case "today":
                    start = now.Date;
                    end = now.Date.AddDays(1).AddTicks(-1);
                    break;
                case "yesterday":
                    start = now.Date.AddDays(-1);
                    end = now.Date.AddTicks(-1);
                    break;
                case "7days":
                    start = now.Date.AddDays(-6);
                    end = now.Date.AddDays(1).AddTicks(-1);
                    break;
                case "30days":
                    start = now.Date.AddDays(-29);
                    end = now.Date.AddDays(1).AddTicks(-1);
                    break;
                case "thisMonth":
                    start = new DateTime(now.Year, now.Month, 1);
                    end = now.Date.AddDays(1).AddTicks(-1);
                    break;
                case "lastMonth":
                    var firstOfThisMonth = new DateTime(now.Year, now.Month, 1);
                    start = firstOfThisMonth.AddMonths(-1);
                    end = firstOfThisMonth.AddTicks(-1);
                    break;
                case "custom":
                    if (fromDate.HasValue) start = fromDate.Value.Date;
                    if (toDate.HasValue) end = toDate.Value.Date.AddDays(1).AddTicks(-1);
                    break;
            }

            var query = _context.Invoices
                .Include(i => i.Store)
                .Include(i => i.Cashier)
                .Include(i => i.InvoiceItems)
                    .ThenInclude(it => it.Product)
                        .ThenInclude(p => p.Category)
                .Include(i => i.InvoiceItems)
                    .ThenInclude(it => it.InvoiceItemBatches)
                        .ThenInclude(ib => ib.Batch)
                .Where(i => i.Status == "COMPLETED" && i.CreatedAt >= start && i.CreatedAt <= end)
                .AsQueryable();

            if (selectedStoreId > 0 && (isAdmin || selectedStoreId == userStoreId))
            {
                query = query.Where(i => i.StoreId == selectedStoreId);
            }

            var invoices = await query.OrderBy(i => i.CreatedAt).ToListAsync();

            var model = new ReportsViewModel
            {
                TimeRange = timeRange,
                FromDate = start,
                ToDate = end,
                SelectedStoreId = selectedStoreId,
                TotalInvoices = invoices.Count,
                TotalRevenue = invoices.Sum(i => i.TotalAmount),
                TotalDiscounts = invoices.Sum(i => i.DiscountAmount + i.PointsDiscount)
            };

            model.AverageOrderValue = model.TotalInvoices > 0 ? model.TotalRevenue / model.TotalInvoices : 0;

            // Tính ước tính giá vốn (COGS) & Lợi nhuận gộp
            decimal totalCost = 0;
            foreach (var inv in invoices)
            {
                foreach (var item in inv.InvoiceItems)
                {
                    decimal itemCost = 0;
                    if (item.InvoiceItemBatches != null && item.InvoiceItemBatches.Any())
                    {
                        itemCost = item.InvoiceItemBatches.Sum(ib => ib.Quantity * (ib.Batch?.ImportPrice ?? item.UnitPrice * 0.7m));
                    }
                    else if (item.Product != null && item.Product.LastImportPrice.HasValue)
                    {
                        itemCost = item.Quantity * item.Product.LastImportPrice.Value;
                    }
                    else
                    {
                        itemCost = item.Quantity * (item.UnitPrice * 0.7m); // Ước tính 70% giá bán
                    }
                    totalCost += itemCost;
                }
            }

            model.EstimatedCost = totalCost;
            model.GrossProfit = Math.Max(0, model.TotalRevenue - model.EstimatedCost);
            model.ProfitMargin = model.TotalRevenue > 0 ? (double)(model.GrossProfit / model.TotalRevenue * 100) : 0;

            // 1. Biểu đồ doanh thu & lợi nhuận theo ngày
            var daysCount = (end.Date - start.Date).Days + 1;
            if (daysCount <= 31)
            {
                for (int d = 0; d < daysCount; d++)
                {
                    var curDate = start.Date.AddDays(d);
                    var dayInvs = invoices.Where(i => i.CreatedAt.Date == curDate).ToList();

                    decimal dayRev = dayInvs.Sum(i => i.TotalAmount);
                    decimal dayCost = 0;
                    foreach (var inv in dayInvs)
                    {
                        foreach (var it in inv.InvoiceItems)
                        {
                            dayCost += it.Quantity * (it.Product?.LastImportPrice ?? it.UnitPrice * 0.7m);
                        }
                    }
                    decimal dayProfit = Math.Max(0, dayRev - dayCost);

                    model.ChartDates.Add(curDate.ToString("dd/MM"));
                    model.ChartRevenues.Add(dayRev);
                    model.ChartProfits.Add(dayProfit);
                }
            }
            else
            {
                // Nhóm theo tuần hoặc tháng nếu khoảng thời gian dài
                var months = invoices.GroupBy(i => new { i.CreatedAt.Year, i.CreatedAt.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month);

                foreach (var g in months)
                {
                    decimal mRev = g.Sum(i => i.TotalAmount);
                    decimal mCost = g.SelectMany(i => i.InvoiceItems).Sum(it => it.Quantity * (it.Product?.LastImportPrice ?? it.UnitPrice * 0.7m));
                    model.ChartDates.Add($"T{g.Key.Month}/{g.Key.Year}");
                    model.ChartRevenues.Add(mRev);
                    model.ChartProfits.Add(Math.Max(0, mRev - mCost));
                }
            }

            // 2. Cơ cấu theo danh mục sản phẩm
            var allItems = invoices.SelectMany(i => i.InvoiceItems).ToList();
            var catGroups = allItems
                .GroupBy(it => (it.Product != null && it.Product.Category != null) ? it.Product.Category.Name : "Khác")
                .OrderByDescending(g => g.Sum(it => it.Subtotal))
                .Take(6)
                .ToList();

            foreach (var cg in catGroups)
            {
                model.CategoryLabels.Add(cg.Key);
                model.CategoryRevenues.Add(cg.Sum(it => it.Subtotal));
            }

            // 3. Cơ cấu phương thức thanh toán
            var cashCount = invoices.Count(i => i.PaymentMethod == "CASH");
            var transferCount = invoices.Count(i => i.PaymentMethod == "TRANSFER" || i.PaymentMethod == "BANK_TRANSFER" || i.PaymentMethod == "E_WALLET");
            var cardCount = invoices.Count(i => i.PaymentMethod == "CARD");

            model.PaymentLabels = new List<string> { "Tiền mặt", "Chuyển khoản QR", "Thẻ ngân hàng" };
            model.PaymentCounts = new List<int> { cashCount, transferCount, cardCount };

            // 4. Top 10 sản phẩm bán chạy nhất
            model.TopProducts = allItems
                .GroupBy(it => new 
                { 
                    it.ProductId, 
                    it.ProductName, 
                    ProductCode = it.Product != null ? it.Product.ProductCode : "",
                    CategoryName = (it.Product != null && it.Product.Category != null) ? it.Product.Category.Name : "Khác"
                })
                .Select(g => new TopProductDto
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.ProductName,
                    ProductCode = g.Key.ProductCode,
                    CategoryName = g.Key.CategoryName,
                    QuantitySold = g.Sum(it => it.Quantity),
                    TotalRevenue = g.Sum(it => it.Subtotal)
                })
                .OrderByDescending(p => p.QuantitySold)
                .Take(10)
                .ToList();

            if (model.TopProducts.Any())
            {
                int maxQ = model.TopProducts.Max(p => p.QuantitySold);
                foreach (var tp in model.TopProducts)
                {
                    tp.PercentageOfMax = maxQ > 0 ? (int)((double)tp.QuantitySold / maxQ * 100) : 0;
                }
            }

            // 5. Doanh số theo thu ngân
            model.CashierSales = invoices
                .GroupBy(i => new { i.CashierId, CashierName = i.Cashier?.FullName ?? "Thu ngân", Username = i.Cashier?.Username ?? "" })
                .Select(g => new CashierSalesDto
                {
                    CashierId = g.Key.CashierId,
                    CashierName = g.Key.CashierName,
                    Username = g.Key.Username,
                    InvoicesCount = g.Count(),
                    TotalAmount = g.Sum(i => i.TotalAmount)
                })
                .OrderByDescending(c => c.TotalAmount)
                .ToList();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv(
            string? timeRange,
            DateTime? fromDate,
            DateTime? toDate,
            long? storeId)
        {
            var userStoreId = GetCurrentStoreId();
            var isAdmin = User.IsInRole("ADMIN");
            var selectedStoreId = (isAdmin && storeId.HasValue && storeId.Value > 0) ? storeId.Value : userStoreId;

            DateTime now = DateTime.Now;
            DateTime start = new DateTime(now.Year, now.Month, 1);
            DateTime end = now.Date.AddDays(1).AddTicks(-1);

            timeRange = string.IsNullOrEmpty(timeRange) ? "thisMonth" : timeRange;

            switch (timeRange)
            {
                case "today":
                    start = now.Date;
                    end = now.Date.AddDays(1).AddTicks(-1);
                    break;
                case "yesterday":
                    start = now.Date.AddDays(-1);
                    end = now.Date.AddTicks(-1);
                    break;
                case "7days":
                    start = now.Date.AddDays(-6);
                    end = now.Date.AddDays(1).AddTicks(-1);
                    break;
                case "30days":
                    start = now.Date.AddDays(-29);
                    end = now.Date.AddDays(1).AddTicks(-1);
                    break;
                case "thisMonth":
                    start = new DateTime(now.Year, now.Month, 1);
                    end = now.Date.AddDays(1).AddTicks(-1);
                    break;
                case "lastMonth":
                    var firstOfThisMonth = new DateTime(now.Year, now.Month, 1);
                    start = firstOfThisMonth.AddMonths(-1);
                    end = firstOfThisMonth.AddTicks(-1);
                    break;
                case "custom":
                    if (fromDate.HasValue) start = fromDate.Value.Date;
                    if (toDate.HasValue) end = toDate.Value.Date.AddDays(1).AddTicks(-1);
                    break;
            }

            var query = _context.Invoices
                .Include(i => i.Store)
                .Include(i => i.Customer)
                .Include(i => i.Cashier)
                .Include(i => i.InvoiceItems)
                    .ThenInclude(it => it.Product)
                .Where(i => i.Status == "COMPLETED" && i.CreatedAt >= start && i.CreatedAt <= end)
                .AsQueryable();

            if (selectedStoreId > 0 && (isAdmin || selectedStoreId == userStoreId))
            {
                query = query.Where(i => i.StoreId == selectedStoreId);
            }

            var invoices = await query.OrderBy(i => i.CreatedAt).ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine($"BÁO CÁO DOANH THU & HOẠT ĐỘNG BÁN HÀNG ({start:dd/MM/yyyy} - {end:dd/MM/yyyy})");
            sb.AppendLine($"Tổng số hóa đơn: {invoices.Count},Tổng doanh thu: {invoices.Sum(i => i.TotalAmount):N0} đ,Tổng giảm giá/điểm: {invoices.Sum(i => i.DiscountAmount + i.PointsDiscount):N0} đ");
            sb.AppendLine();
            sb.AppendLine("Mã hóa đơn,Chi nhánh,Thời gian,Thu ngân,Khách hàng,Hình thức thanh toán,Tạm tính,Giảm giá,Tổng tiền");

            foreach (var inv in invoices)
            {
                var payMethod = inv.PaymentMethod switch
                {
                    "CASH" => "Tiền mặt",
                    "CARD" => "Thẻ",
                    "TRANSFER" => "Chuyển khoản QR",
                    "BANK_TRANSFER" => "Chuyển khoản QR",
                    "E_WALLET" => "Ví điện tử",
                    _ => inv.PaymentMethod
                };

                sb.AppendLine($"\"{inv.InvoiceCode}\",\"{inv.Store?.Name}\",\"{inv.CreatedAt:dd/MM/yyyy HH:mm}\",\"{inv.Cashier?.FullName}\",\"{inv.Customer?.FullName ?? "Khách lẻ"}\",\"{payMethod}\",{inv.Subtotal},{inv.DiscountAmount + inv.PointsDiscount},{inv.TotalAmount}");
            }

            var encoding = new UTF8Encoding(true);
            byte[] fileBytes = encoding.GetBytes(sb.ToString());
            string fileName = $"Bao_cao_doanh_thu_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

            return File(fileBytes, "text/csv; charset=utf-8", fileName);
        }

        private long GetCurrentStoreId()
        {
            var storeIdClaim = User.FindFirst("StoreId")?.Value;
            return long.TryParse(storeIdClaim, out var id) ? id : 1;
        }
    }
}

