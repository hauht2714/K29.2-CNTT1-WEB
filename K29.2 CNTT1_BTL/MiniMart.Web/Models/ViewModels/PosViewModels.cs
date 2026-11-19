using System;
using System.Collections.Generic;

namespace MiniMart.Web.Models.ViewModels
{
    public class CheckoutItemDto
    {
        public long ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class CheckoutRequestDto
    {
        public long? CustomerId { get; set; }
        public string PaymentMethod { get; set; } = "CASH";
        public decimal CashReceived { get; set; } = 0;
        public decimal DiscountAmount { get; set; } = 0;
        public int PointsToRedeem { get; set; } = 0;
        public List<CheckoutItemDto> Items { get; set; } = new List<CheckoutItemDto>();
    }

    public class ReceiptItemDto
    {
        public string ProductName { get; set; } = string.Empty;
        public string Unit { get; set; } = "Cái";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class ReceiptDto
    {
        public string InvoiceCode { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
        public string StoreAddress { get; set; } = string.Empty;
        public string StorePhone { get; set; } = string.Empty;
        public string CashierName { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal PointsDiscount { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = "CASH";
        public decimal CashReceived { get; set; }
        public decimal CashChange { get; set; }
        public int PointsEarned { get; set; }
        public int NewTotalPoints { get; set; }
        public List<ReceiptItemDto> Items { get; set; } = new List<ReceiptItemDto>();
    }
}

