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
    [Authorize(Roles = "ADMIN,STORE_MANAGER,CASHIER")]
    public class PosController : Controller
    {
        private const decimal PointValue = 1000m;          // 1 điểm = 1.000đ khi tiêu
        private const decimal MoneyPerEarnedPoint = 10000m; // 10.000đ thực trả = 1 điểm tích lũy
        private const int MaxQuantityPerLine = 100000;
        private static readonly string[] AllowedPaymentMethods = { "CASH", "BANK_TRANSFER", "E_WALLET", "CARD", "TRANSFER" };

        private readonly MiniMartDbContext _context;

        public PosController(MiniMartDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var storeId = GetCurrentStoreId();

            var categories = await _context.Categories
                .Where(c => c.Status == "ACTIVE")
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.StoreId = storeId;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts(string? keyword, long? categoryId)
        {
            var storeId = GetCurrentStoreId();

            var query = _context.Products
                .Include(p => p.StoreProducts)
                .Where(p => p.Status == "ACTIVE")
                .AsQueryable();

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(p => p.CategoryId == categoryId);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(kw) ||
                                         p.ProductCode.ToLower().Contains(kw) ||
                                         (p.Barcode != null && p.Barcode.Contains(kw)));
            }

            var list = await query.Take(40).ToListAsync();
            var activePromos = await GetActivePromotionsAsync();

            var result = list.Select(p =>
            {
                // Chỉ hiển thị tồn kho của chi nhánh hiện tại
                var sp = p.StoreProducts.FirstOrDefault(s => s.StoreId == storeId);
                var price = CalculatePrice(p, sp, activePromos);

                return new
                {
                    id = p.Id,
                    storeProductId = sp?.Id ?? 0,
                    productCode = p.ProductCode,
                    name = p.Name,
                    barcode = p.Barcode ?? "",
                    unit = p.Unit,
                    originalPrice = price.BasePrice,
                    price = price.UnitPrice,
                    promoTag = price.PromoTag,
                    buyQty = price.BuyQty,
                    getQty = price.GetQty,
                    stockQuantity = sp?.StockQuantity ?? 0,
                    imageUrl = p.ImageUrl
                };
            });

            return Json(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomerByPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return Json(new { success = false, message = "Số điện thoại không hợp lệ." });
            }

            var cleanPhone = phone.Trim();
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Phone == cleanPhone && c.Status == "ACTIVE");

            if (customer == null)
            {
                return Json(new { success = false, message = "Không tìm thấy khách hàng với số điện thoại này." });
            }

            return Json(new
            {
                success = true,
                customer = new
                {
                    id = customer.Id,
                    memberCode = customer.MemberCode,
                    fullName = customer.FullName,
                    phone = customer.Phone,
                    totalPoints = customer.TotalPoints,
                    membershipTier = customer.MembershipTier
                }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout([FromBody] CheckoutRequestDto req)
        {
            if (req == null || req.Items == null || !req.Items.Any())
            {
                return Json(new { success = false, message = "Giỏ hàng đang trống." });
            }

            var paymentMethod = (req.PaymentMethod ?? "").Trim().ToUpper();
            if (!AllowedPaymentMethods.Contains(paymentMethod))
            {
                return Json(new { success = false, message = "Phương thức thanh toán không hợp lệ." });
            }

            if (req.Items.Any(i => i.Quantity <= 0 || i.Quantity > MaxQuantityPerLine))
            {
                return Json(new { success = false, message = "Số lượng sản phẩm phải lớn hơn 0." });
            }

            // Gộp các dòng trùng sản phẩm
            var requestedLines = req.Items
                .GroupBy(i => i.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
                .ToList();

            var storeId = GetCurrentStoreId();
            var cashierId = GetCurrentUserId();

            var store = await _context.Stores.FindAsync(storeId);
            if (store == null)
            {
                return Json(new { success = false, message = "Tài khoản chưa được gán chi nhánh hợp lệ để bán hàng." });
            }
            var cashier = await _context.Users.FindAsync(cashierId);

            Customer? customer = null;
            if (req.CustomerId.HasValue)
            {
                customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == req.CustomerId.Value && c.Status == "ACTIVE");
                if (customer == null)
                {
                    return Json(new { success = false, message = "Khách hàng không tồn tại hoặc đã bị khóa." });
                }
            }

            // Giá bán luôn lấy từ CSDL, không tin giá do trình duyệt gửi lên
            var productIds = requestedLines.Select(l => l.ProductId).ToList();
            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);
            var storeProducts = await _context.StoreProducts
                .Where(s => s.StoreId == store.Id && productIds.Contains(s.ProductId))
                .ToDictionaryAsync(s => s.ProductId);
            var activePromos = await GetActivePromotionsAsync();

            var lines = new List<(Product Product, StoreProduct Sp, int Quantity, int FreeQty, decimal UnitPrice, decimal LineDiscount, decimal LineTotal)>();
            foreach (var line in requestedLines)
            {
                if (!products.TryGetValue(line.ProductId, out var product) || product.Status != "ACTIVE")
                {
                    return Json(new { success = false, message = $"Sản phẩm mã #{line.ProductId} không tồn tại hoặc đã ngừng kinh doanh." });
                }

                if (!storeProducts.TryGetValue(line.ProductId, out var sp) || sp.StockQuantity < line.Quantity)
                {
                    return Json(new { success = false, message = $"Sản phẩm '{product.Name}' không đủ tồn kho để bán (còn {sp?.StockQuantity ?? 0})." });
                }

                var price = CalculatePrice(product, sp, activePromos);
                int freeQty = 0;
                if (price.BuyQty > 0 && price.GetQty > 0)
                {
                    freeQty = line.Quantity / (price.BuyQty + price.GetQty) * price.GetQty;
                }

                decimal lineDiscount = freeQty * price.UnitPrice;
                decimal lineTotal = price.UnitPrice * line.Quantity - lineDiscount;
                lines.Add((product, sp, line.Quantity, freeQty, price.UnitPrice, lineDiscount, lineTotal));
            }

            decimal subtotal = lines.Sum(l => l.LineTotal);

            // Điểm dùng không vượt quá điểm khách có và không vượt quá giá trị đơn hàng
            int pointsToUse = 0;
            if (customer != null && req.PointsToRedeem > 0)
            {
                int maxPointsByAmount = (int)Math.Floor(subtotal / PointValue);
                pointsToUse = Math.Min(req.PointsToRedeem, Math.Min(customer.TotalPoints, maxPointsByAmount));
                pointsToUse = Math.Max(0, pointsToUse);
            }
            decimal pointsDiscount = pointsToUse * PointValue;
            decimal totalAmount = Math.Max(0, subtotal - pointsDiscount);

            if (paymentMethod == "CASH" && req.CashReceived < totalAmount)
            {
                return Json(new { success = false, message = $"Tiền khách đưa chưa đủ để thanh toán (cần {totalAmount:N0}đ)." });
            }

            decimal cashChange = paymentMethod == "CASH" ? Math.Max(0, req.CashReceived - totalAmount) : 0;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Tạo hóa đơn
                var invoiceCode = "HD" + DateTime.Now.ToString("yyyyMMdd") + (DateTime.Now.Ticks % 100000).ToString("D5");
                var invoice = new Invoice
                {
                    InvoiceCode = invoiceCode,
                    StoreId = store.Id,
                    CustomerId = customer?.Id,
                    CashierId = cashier?.Id ?? cashierId,
                    Subtotal = subtotal,
                    DiscountAmount = 0,
                    PointsDiscount = pointsDiscount,
                    TotalAmount = totalAmount,
                    PaymentMethod = paymentMethod,
                    CashReceived = paymentMethod == "CASH" ? req.CashReceived : totalAmount,
                    CashChange = cashChange,
                    Status = "COMPLETED",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();

                var receiptItems = new List<ReceiptItemDto>();

                // 2. Xử lý từng món hàng và trừ kho theo lô (hết hạn trước xuất trước)
                foreach (var line in lines)
                {
                    var sp = line.Sp;
                    sp.StockQuantity -= line.Quantity;
                    sp.UpdatedAt = DateTime.Now;

                    var invItem = new InvoiceItem
                    {
                        InvoiceId = invoice.Id,
                        ProductId = line.Product.Id,
                        ProductName = line.Product.Name,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        Discount = line.LineDiscount,
                        Subtotal = line.LineTotal,
                        StoreProductId = sp.Id
                    };
                    _context.InvoiceItems.Add(invItem);
                    await _context.SaveChangesAsync();

                    var deducted = await StockBatchHelper.DeductAsync(_context, sp.Id, line.Quantity);
                    foreach (var (batch, qty) in deducted)
                    {
                        _context.InvoiceItemBatches.Add(new InvoiceItemBatch
                        {
                            InvoiceItemId = invItem.Id,
                            BatchId = batch.Id,
                            Quantity = qty
                        });
                    }

                    _context.InventoryTransactions.Add(new InventoryTransaction
                    {
                        StoreId = invoice.StoreId,
                        ProductId = line.Product.Id,
                        Type = "SALE",
                        Quantity = -line.Quantity,
                        ReferenceId = invoice.Id,
                        Note = $"Bán hàng HĐ {invoice.InvoiceCode}",
                        CreatedAt = DateTime.Now
                    });

                    receiptItems.Add(new ReceiptItemDto
                    {
                        ProductName = line.FreeQty > 0 ? $"{line.Product.Name} (tặng {line.FreeQty})" : line.Product.Name,
                        Unit = line.Product.Unit,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        Subtotal = line.LineTotal
                    });
                }

                // 3. Tích & tiêu điểm khách hàng
                int pointsEarned = 0;
                int newTotalPoints = 0;

                if (customer != null)
                {
                    if (pointsToUse > 0)
                    {
                        customer.TotalPoints -= pointsToUse;
                    }

                    pointsEarned = (int)(totalAmount / MoneyPerEarnedPoint);
                    customer.TotalPoints += pointsEarned;
                    customer.AccumulatedPoints += pointsEarned;
                    customer.LastTransactionDate = DateTime.Now;

                    if (customer.AccumulatedPoints >= 5000) customer.MembershipTier = "DIAMOND";
                    else if (customer.AccumulatedPoints >= 3000) customer.MembershipTier = "GOLD";
                    else if (customer.AccumulatedPoints >= 1000) customer.MembershipTier = "SILVER";

                    customer.UpdatedAt = DateTime.Now;
                    newTotalPoints = customer.TotalPoints;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var receipt = new ReceiptDto
                {
                    InvoiceCode = invoice.InvoiceCode,
                    StoreName = store.Name,
                    StoreAddress = store.Address ?? "",
                    StorePhone = store.Phone ?? "",
                    CashierName = cashier?.FullName ?? "Thu ngân",
                    CustomerName = customer?.FullName ?? "Khách lẻ",
                    CustomerPhone = customer?.Phone ?? "",
                    CreatedAt = invoice.CreatedAt,
                    Subtotal = subtotal,
                    DiscountAmount = 0,
                    PointsDiscount = pointsDiscount,
                    TotalAmount = totalAmount,
                    PaymentMethod = paymentMethod,
                    CashReceived = invoice.CashReceived ?? totalAmount,
                    CashChange = cashChange,
                    PointsEarned = pointsEarned,
                    NewTotalPoints = newTotalPoints,
                    Items = receiptItems
                };

                return Json(new { success = true, receipt });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Lỗi thanh toán: " + ex.Message });
            }
        }

        /// <summary>
        /// Khuyến mãi đang chạy: đã bắt đầu và chưa qua hết ngày kết thúc
        /// (ngày kết thúc được tính trọn ngày, kể cả khi lưu là 00:00).
        /// </summary>
        private async Task<List<Promotion>> GetActivePromotionsAsync()
        {
            var now = DateTime.Now;
            var today = DateTime.Today;
            return await _context.Promotions
                .Include(pr => pr.PromotionItems)
                .Where(pr => pr.IsActive && pr.StartDate <= now && pr.EndDate >= today)
                .ToListAsync();
        }

        private record PriceInfo(decimal BasePrice, decimal UnitPrice, string PromoTag, int BuyQty, int GetQty);

        private static PriceInfo CalculatePrice(Product p, StoreProduct? sp, List<Promotion> activePromos)
        {
            decimal basePrice = Math.Max(0, sp?.Price ?? p.DefaultPrice ?? 0);

            var promoItem = activePromos
                .SelectMany(pr => pr.PromotionItems.Select(pi => new { Promo = pr, Item = pi }))
                .FirstOrDefault(x => x.Item.ProductId == p.Id);

            if (promoItem != null)
            {
                var item = promoItem.Item;
                switch (promoItem.Promo.PromoType)
                {
                    case "PERCENTAGE" when item.DiscountPercent is > 0 and <= 100:
                        var pct = item.DiscountPercent.Value;
                        var discounted = Math.Round(basePrice * (1 - (decimal)pct / 100m), 0);
                        return new PriceInfo(basePrice, Math.Max(0, discounted), $"-{pct}%", 0, 0);

                    case "FIXED_AMOUNT" when item.DiscountAmount is > 0:
                        var amt = item.DiscountAmount.Value;
                        return new PriceInfo(basePrice, Math.Max(0, basePrice - amt), $"-{amt:N0}đ", 0, 0);

                    case "BUY_X_GET_Y" when item.BuyQty is > 0 && item.GetQty is > 0:
                        return new PriceInfo(basePrice, basePrice, $"Mua {item.BuyQty} tặng {item.GetQty}", item.BuyQty.Value, item.GetQty.Value);
                }
            }

            return new PriceInfo(basePrice, basePrice, "", 0, 0);
        }

        private long GetCurrentStoreId()
        {
            var claim = User.FindFirstValue("StoreId");
            if (long.TryParse(claim, out long id) && id > 0) return id;
            return _context.Stores.Select(s => s.Id).FirstOrDefault();
        }

        private long GetCurrentUserId()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (long.TryParse(claim, out long id) && id > 0) return id;
            return _context.Users.Select(u => u.Id).FirstOrDefault();
        }
    }
}
