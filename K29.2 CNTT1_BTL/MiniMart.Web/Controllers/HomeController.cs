using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniMart.Web.Data;
using MiniMart.Web.Models;
using MiniMart.Web.Models.ViewModels;

namespace MiniMart.Web.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly MiniMartDbContext _context;

        public HomeController(MiniMartDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var next30Days = today.AddDays(30);

            var model = new DashboardViewModel();

            try
            {
                // 1. Thống kê hôm nay
                var todayInvoices = await _context.Invoices
                    .Where(i => i.CreatedAt >= today && i.CreatedAt < tomorrow && i.Status == "COMPLETED")
                    .ToListAsync();

                model.TodayRevenue = todayInvoices.Sum(i => i.TotalAmount);
                model.TodayInvoicesCount = todayInvoices.Count;

                // 2. Thống kê tháng này
                var monthInvoices = await _context.Invoices
                    .Where(i => i.CreatedAt >= startOfMonth && i.Status == "COMPLETED")
                    .ToListAsync();

                model.MonthRevenue = monthInvoices.Sum(i => i.TotalAmount);
                model.MonthInvoicesCount = monthInvoices.Count;

                // 3. Tổng số lượng thực thể
                model.TotalProductsCount = await _context.Products.CountAsync(p => p.Status == "ACTIVE");
                model.TotalCustomersCount = await _context.Customers.CountAsync(c => c.Status == "ACTIVE");

                // 4. Cảnh báo tồn kho & hạn sử dụng
                model.LowStockProducts = await _context.StoreProducts
                    .Include(sp => sp.Product)
                    .Where(sp => sp.StockQuantity <= sp.MinStock)
                    .Take(5)
                    .ToListAsync();
                model.LowStockCount = await _context.StoreProducts.CountAsync(sp => sp.StockQuantity <= sp.MinStock);

                model.NearExpiryBatchCount = await _context.ProductBatches
                    .CountAsync(pb => pb.ExpiryDate.HasValue && pb.ExpiryDate.Value <= next30Days && pb.RemainingQuantity > 0);

                // 5. 5 hóa đơn gần nhất
                model.RecentInvoices = await _context.Invoices
                    .Include(i => i.Customer)
                    .Include(i => i.Cashier)
                    .OrderByDescending(i => i.CreatedAt)
                    .Take(5)
                    .ToListAsync();

                // 6. Biểu đồ doanh thu 7 ngày gần nhất
                var sevenDaysAgo = today.AddDays(-6);
                var recent7DaysInvoices = await _context.Invoices
                    .Where(i => i.CreatedAt >= sevenDaysAgo && i.Status == "COMPLETED")
                    .ToListAsync();

                for (int d = 6; d >= 0; d--)
                {
                    var targetDate = today.AddDays(-d);
                    var dateStr = targetDate.ToString("dd/MM");
                    var dayInvoices = recent7DaysInvoices.Where(i => i.CreatedAt.Date == targetDate).ToList();

                    model.ChartDates.Add(dateStr);
                    model.ChartRevenues.Add(dayInvoices.Sum(i => i.TotalAmount));
                    model.ChartOrders.Add(dayInvoices.Count);
                }

                // 7. Cơ cấu phương thức thanh toán tháng này
                model.CashOrdersCount = monthInvoices.Count(i => i.PaymentMethod == "CASH");
                model.CardOrdersCount = monthInvoices.Count(i => i.PaymentMethod == "CARD");
                model.TransferOrdersCount = monthInvoices.Count(i => i.PaymentMethod == "TRANSFER" || i.PaymentMethod == "BANK_TRANSFER" || i.PaymentMethod == "E_WALLET");

                // 8. Top 5 sản phẩm bán chạy nhất tháng này
                var topItems = await _context.InvoiceItems
                    .Include(it => it.Invoice)
                    .Include(it => it.Product)
                        .ThenInclude(p => p.Category)
                    .Where(it => it.Invoice.CreatedAt >= startOfMonth && it.Invoice.Status == "COMPLETED")
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
                        QuantitySold = g.Sum(x => x.Quantity),
                        TotalRevenue = g.Sum(x => x.Subtotal)
                    })
                    .OrderByDescending(x => x.QuantitySold)
                    .Take(5)
                    .ToListAsync();

                if (topItems.Any())
                {
                    int maxQty = topItems.Max(t => t.QuantitySold);
                    foreach (var item in topItems)
                    {
                        item.PercentageOfMax = maxQty > 0 ? (int)((double)item.QuantitySold / maxQty * 100) : 0;
                    }
                }
                model.TopSellingProducts = topItems;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Dashboard query error] {ex.Message}");
            }

            return View(model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
