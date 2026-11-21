using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MiniMart.Web.Models.Entities;

namespace MiniMart.Web.Data
{
    /// <summary>
    /// Giữ cho tổng RemainingQuantity của các lô (PRODUCT_BATCHES) khớp với StockQuantity của StoreProduct
    /// khi xuất/nhập/chuyển/kiểm kê kho.
    /// </summary>
    public static class StockBatchHelper
    {
        private static readonly Random _rnd = new Random();

        public static string NewBatchBarcode()
        {
            lock (_rnd)
            {
                return "BAT" + DateTime.Now.ToString("yyMMdd") + _rnd.Next(10000000, 99999999);
            }
        }

        /// <summary>
        /// Trừ số lượng khỏi các lô của một StoreProduct theo thứ tự hết hạn sớm nhất trước (FEFO).
        /// Trả về danh sách lô đã trừ và số lượng trừ ở mỗi lô. Tổng có thể nhỏ hơn qty nếu lô không đủ.
        /// </summary>
        public static async Task<List<(ProductBatch Batch, int Qty)>> DeductAsync(MiniMartDbContext context, long storeProductId, int qty)
        {
            var result = new List<(ProductBatch, int)>();
            if (qty <= 0) return result;

            var batches = await context.ProductBatches
                .Where(b => b.StoreProductId == storeProductId && b.RemainingQuantity > 0)
                .OrderBy(b => b.ExpiryDate ?? DateTime.MaxValue)
                .ThenBy(b => b.Id)
                .ToListAsync();

            int needed = qty;
            foreach (var batch in batches)
            {
                if (needed <= 0) break;

                // Lô có thể đã bị trừ trong cùng request (giá trị trong bộ nhớ mới hơn DB)
                int available = batch.RemainingQuantity ?? 0;
                if (available <= 0) continue;

                int deduct = Math.Min(available, needed);
                batch.RemainingQuantity = available - deduct;
                needed -= deduct;
                result.Add((batch, deduct));
            }

            return result;
        }

        /// <summary>
        /// Chuyển số lượng từ lô của kho nguồn sang lô mới ở kho đích (giữ nguyên hạn dùng và giá nhập).
        /// Phần thiếu (kho nguồn không có đủ lô) được tạo thành một lô không hạn dùng ở kho đích.
        /// </summary>
        public static async Task MoveAsync(MiniMartDbContext context, StoreProduct source, StoreProduct dest, int qty, decimal? fallbackImportPrice)
        {
            var deducted = await DeductAsync(context, source.Id, qty);

            foreach (var (batch, moved) in deducted)
            {
                context.ProductBatches.Add(new ProductBatch
                {
                    StoreProduct = dest,
                    ImportDate = batch.ImportDate ?? DateTime.Today,
                    ExpiryDate = batch.ExpiryDate,
                    Quantity = moved,
                    RemainingQuantity = moved,
                    ImportPrice = batch.ImportPrice,
                    BatchBarcode = NewBatchBarcode()
                });
            }

            int shortfall = qty - deducted.Sum(d => d.Qty);
            if (shortfall > 0)
            {
                AddBatch(context, dest, shortfall, fallbackImportPrice, null);
            }
        }

        public static void AddBatch(MiniMartDbContext context, StoreProduct sp, int qty, decimal? importPrice, DateTime? expiryDate)
        {
            if (qty <= 0) return;
            context.ProductBatches.Add(new ProductBatch
            {
                StoreProduct = sp,
                ImportDate = DateTime.Today,
                ExpiryDate = expiryDate,
                Quantity = qty,
                RemainingQuantity = qty,
                ImportPrice = importPrice,
                BatchBarcode = NewBatchBarcode()
            });
        }
    }
}
