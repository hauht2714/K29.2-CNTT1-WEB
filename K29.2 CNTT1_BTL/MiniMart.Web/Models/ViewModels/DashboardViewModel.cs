using System.Collections.Generic;
using MiniMart.Web.Models.Entities;

namespace MiniMart.Web.Models.ViewModels
{
    public class DashboardViewModel
    {
        public decimal TodayRevenue { get; set; } = 0;
        public int TodayInvoicesCount { get; set; } = 0;
        public decimal MonthRevenue { get; set; } = 0;
        public int MonthInvoicesCount { get; set; } = 0;

        public int TotalProductsCount { get; set; } = 0;
        public int TotalCustomersCount { get; set; } = 0;
        public int LowStockCount { get; set; } = 0;
        public int NearExpiryBatchCount { get; set; } = 0;

        // Dữ liệu biểu đồ 7 ngày gần nhất
        public List<string> ChartDates { get; set; } = new List<string>();
        public List<decimal> ChartRevenues { get; set; } = new List<decimal>();
        public List<int> ChartOrders { get; set; } = new List<int>();

        // Cơ cấu phương thức thanh toán
        public int CashOrdersCount { get; set; } = 0;
        public int CardOrdersCount { get; set; } = 0;
        public int TransferOrdersCount { get; set; } = 0;

        // Top sản phẩm bán chạy
        public List<TopProductDto> TopSellingProducts { get; set; } = new List<TopProductDto>();

        public List<Invoice> RecentInvoices { get; set; } = new List<Invoice>();
        public List<StoreProduct> LowStockProducts { get; set; } = new List<StoreProduct>();
    }

    public class TopProductDto
    {
        public long ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public string ProductCode { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public int QuantitySold { get; set; }
        public decimal TotalRevenue { get; set; }
        public int PercentageOfMax { get; set; } = 100;
    }
}
