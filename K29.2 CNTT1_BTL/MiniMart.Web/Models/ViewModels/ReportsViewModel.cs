using System;
using System.Collections.Generic;

namespace MiniMart.Web.Models.ViewModels
{
    public class ReportsViewModel
    {
        public string TimeRange { get; set; } = "thisMonth";
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public long? SelectedStoreId { get; set; }

        // Các chỉ số KPI cốt lõi
        public decimal TotalRevenue { get; set; } = 0;
        public decimal TotalDiscounts { get; set; } = 0;
        public decimal EstimatedCost { get; set; } = 0;
        public decimal GrossProfit { get; set; } = 0;
        public double ProfitMargin { get; set; } = 0;
        public int TotalInvoices { get; set; } = 0;
        public decimal AverageOrderValue { get; set; } = 0;

        // Dữ liệu biểu đồ doanh thu & lợi nhuận theo ngày
        public List<string> ChartDates { get; set; } = new List<string>();
        public List<decimal> ChartRevenues { get; set; } = new List<decimal>();
        public List<decimal> ChartProfits { get; set; } = new List<decimal>();

        // Cơ cấu doanh thu theo danh mục
        public List<string> CategoryLabels { get; set; } = new List<string>();
        public List<decimal> CategoryRevenues { get; set; } = new List<decimal>();

        // Cơ cấu phương thức thanh toán
        public List<string> PaymentLabels { get; set; } = new List<string>();
        public List<int> PaymentCounts { get; set; } = new List<int>();

        // Top 10 sản phẩm bán chạy nhất
        public List<TopProductDto> TopProducts { get; set; } = new List<TopProductDto>();

        // Doanh số theo thu ngân / nhân viên
        public List<CashierSalesDto> CashierSales { get; set; } = new List<CashierSalesDto>();
    }

    public class CashierSalesDto
    {
        public long CashierId { get; set; }
        public string CashierName { get; set; } = "";
        public string Username { get; set; } = "";
        public int InvoicesCount { get; set; }
        public decimal TotalAmount { get; set; }
    }
}

