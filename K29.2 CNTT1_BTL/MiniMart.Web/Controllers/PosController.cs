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

            var now = DateTime.Now;
            var activePromos = await _context.Promotions
                .Include(pr => pr.PromotionItems)
                .Where(pr => pr.IsActive && pr.StartDate <= now && pr.EndDate >= now)
                .ToListAsync();

            var result = list.Select(p =>
            {
                var sp = p.StoreProducts.FirstOrDefault(s => s.StoreId == storeId) 
                         ?? p.StoreProducts.FirstOrDefault();
                decimal basePrice = sp?.Price ?? p.DefaultPrice ?? 0;
                decimal finalPrice = basePrice;
                string promoTag = "";

                var promoItem = activePromos
                    .SelectMany(pr => pr.PromotionItems.Select(pi => new { Promo = pr, Item = pi }))
                    .FirstOrDefault(x => x.Item.ProductId == p.Id);

                if (promoItem != null)
                {
                    if (promoItem.Promo.PromoType == "PERCENTAGE" && promoItem.Item.DiscountPercent.HasValue)
                    {
                        var pct = promoItem.Item.DiscountPercent.Value;
                        finalPrice = Math.Max(0, basePrice * (1 - (decimal)pct / 100m));
                        promoTag = $"-{pct}%";
                    }
                    else if (promoItem.Promo.PromoType == "FIXED_AMOUNT" && promoItem.Item.DiscountAmount.HasValue)
                    {
                        var amt = promoItem.Item.DiscountAmount.Value;
                        finalPrice = Math.Max(0, basePrice - amt);
                        promoTag = $"-{amt:N0}đ";
                    }
                    else if (promoItem.Promo.PromoType == "BUY_X_GET_Y")
                    {
                        promoTag = $"Mua {promoItem.Item.BuyQty} tặng {promoItem.Item.GetQty}";
                    }
                }

                return new
                {
                    id = p.Id,
                    storeProductId = sp?.Id ?? 0,
                    productCode = p.ProductCode,
                    name = p.Name,
                    barcode = p.Barcode ?? "",
                    unit = p.Unit,
                    originalPrice = basePrice,
                    price = finalPrice,
                    promoTag = promoTag,
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
        public async Task<IActionResult> Checkout([FromBody] CheckoutRequestDto req)
        {
            if (req == null || req.Items == null || !req.Items.Any())
            {
                return Json(new { success = false, message = "Giỏ hàng đang trống." });
            }

            var storeId = GetCurrentStoreId();
            var cashierId = GetCurrentUserId();

            var store = await _context.Stores.FindAsync(storeId) ?? await _context.Stores.FirstOrDefaultAsync();
            var cashier = await _context.Users.FindAsync(cashierId);

            Customer? customer = null;
            if (req.CustomerId.HasValue)
            {
                customer = await _context.Customers.FindAsync(req.CustomerId.Value);
            }

            // Tính toán tiền
            decimal subtotal = req.Items.Sum(i => i.UnitPrice * i.Quantity);
            decimal discount = Math.Max(0, req.DiscountAmount);

            int pointsToUse = 0;
            decimal pointsDiscount = 0;

            if (customer != null && req.PointsToRedeem > 0)
            {
                pointsToUse = Math.Min(req.PointsToRedeem, customer.TotalPoints);
                pointsDiscount = pointsToUse * 1000m;
            }

            decimal totalAmount = Math.Max(0, subtotal - discount - pointsDiscount);

            if (req.PaymentMethod == "CASH" && req.CashReceived < totalAmount)
            {
                return Json(new { success = false, message = "Tiền khách đưa chưa đủ để thanh toán." });
            }

            decimal cashChange = req.PaymentMethod == "CASH" ? Math.Max(0, req.CashReceived - totalAmount) : 0;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Tạo hóa đơn
                var invoiceCode = "HD" + DateTime.Now.ToString("yyyyMMdd") + (DateTime.Now.Ticks % 100000).ToString("D5");
                var invoice = new Invoice
                {
                    InvoiceCode = invoiceCode,
                    StoreId = store?.Id ?? 1,
                    CustomerId = customer?.Id,
                    CashierId = cashier?.Id ?? 1,
                    Subtotal = subtotal,
                    DiscountAmount = discount,
                    PointsDiscount = pointsDiscount,
                    TotalAmount = totalAmount,
                    PaymentMethod = req.PaymentMethod,
                    CashReceived = req.PaymentMethod == "CASH" ? req.CashReceived : totalAmount,
                    CashChange = cashChange,
                    Status = "COMPLETED",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();

                var receiptItems = new List<ReceiptItemDto>();

                // 2. Xử lý từng món hàng và trừ kho FIFO
                foreach (var item in req.Items)
                {
                    var sp = await _context.StoreProducts
                        .FirstOrDefaultAsync(s => s.StoreId == invoice.StoreId && s.ProductId == item.ProductId);

                    if (sp == null || sp.StockQuantity < item.Quantity)
                    {
                        await transaction.RollbackAsync();
                        return Json(new { success = false, message = $"Sản phẩm '{item.ProductName}' không đủ tồn kho để bán." });
                    }

                    // Trừ tồn kho StoreProduct
                    sp.StockQuantity -= item.Quantity;
                    sp.UpdatedAt = DateTime.Now;

                    // Tạo InvoiceItem
                    var invItem = new InvoiceItem
                    {
                        InvoiceId = invoice.Id,
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Discount = 0,
                        Subtotal = item.UnitPrice * item.Quantity,
                        StoreProductId = sp.Id
                    };
                    _context.InvoiceItems.Add(invItem);
                    await _context.SaveChangesAsync();

                    // Trừ lô hàng theo FIFO (ProductBatches)
                    var batches = await _context.ProductBatches
                        .Where(b => b.StoreProductId == sp.Id && b.RemainingQuantity > 0)
                        .OrderBy(b => b.ExpiryDate ?? DateTime.MaxValue)
                        .ToListAsync();

                    int needed = item.Quantity;
                    foreach (var batch in batches)
                    {
                        if (needed <= 0) break;

                        int available = batch.RemainingQuantity ?? 0;
                        int deduct = Math.Min(available, needed);

                        batch.RemainingQuantity = available - deduct;
                        needed -= deduct;

                        // Lưu vết xuất lô
                        _context.InvoiceItemBatches.Add(new InvoiceItemBatch
                        {
                            InvoiceItemId = invItem.Id,
                            BatchId = batch.Id,
                            Quantity = deduct
                        });
                    }

                    // Ghi nhận biến động kho
                    _context.InventoryTransactions.Add(new InventoryTransaction
                    {
                        StoreId = invoice.StoreId,
                        ProductId = item.ProductId,
                        Type = "SALE",
                        Quantity = -item.Quantity,
                        ReferenceId = invoice.Id,
                        Note = $"Bán hàng HĐ {invoice.InvoiceCode}",
                        CreatedAt = DateTime.Now
                    });

                    receiptItems.Add(new ReceiptItemDto
                    {
                        ProductName = item.ProductName,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Subtotal = item.UnitPrice * item.Quantity
                    });
                }

                // 3. Tích & tiêu điểm khách hàng
                int pointsEarned = 0;
                int newTotalPoints = 0;

                if (customer != null)
                {
                    // Trừ điểm đã dùng
                    if (pointsToUse > 0)
                    {
                        customer.TotalPoints -= pointsToUse;
                    }

                    // Tích điểm mới: 1 điểm cho mỗi 10.000đ thực thanh toán
                    pointsEarned = (int)(totalAmount / 10000m);
                    customer.TotalPoints += pointsEarned;
                    customer.AccumulatedPoints += pointsEarned;
                    customer.LastTransactionDate = DateTime.Now;

                    // Cập nhật hạng thành viên
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
                    StoreName = store?.Name ?? "MiniMart",
                    StoreAddress = store?.Address ?? "",
                    StorePhone = store?.Phone ?? "",
                    CashierName = cashier?.FullName ?? "Thu ngân",
                    CustomerName = customer?.FullName ?? "Khách lẻ",
                    CustomerPhone = customer?.Phone ?? "",
                    CreatedAt = invoice.CreatedAt,
                    Subtotal = subtotal,
                    DiscountAmount = discount,
                    PointsDiscount = pointsDiscount,
                    TotalAmount = totalAmount,
                    PaymentMethod = req.PaymentMethod,
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

