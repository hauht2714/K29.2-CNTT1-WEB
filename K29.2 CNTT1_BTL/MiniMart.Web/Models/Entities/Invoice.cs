using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniMart.Web.Models.Entities
{
    [Table("INVOICES")]
    public class Invoice : BaseEntity
    {
        [Required]
        [MaxLength(20)]
        [Column("INVOICE_CODE")]
        public string InvoiceCode { get; set; } = string.Empty;

        [Required]
        [Column("STORE_ID")]
        public long StoreId { get; set; }

        [Column("CUSTOMER_ID")]
        public long? CustomerId { get; set; }

        [Required]
        [Column("CASHIER_ID")]
        public long CashierId { get; set; }

        [Required]
        [Column("SUBTOTAL", TypeName = "decimal(15,2)")]
        public decimal Subtotal { get; set; }

        [Column("DISCOUNT_AMOUNT", TypeName = "decimal(15,2)")]
        public decimal DiscountAmount { get; set; } = 0;

        [Column("POINTS_DISCOUNT", TypeName = "decimal(15,2)")]
        public decimal PointsDiscount { get; set; } = 0;

        [Required]
        [Column("TOTAL_AMOUNT", TypeName = "decimal(15,2)")]
        public decimal TotalAmount { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("PAYMENT_METHOD")]
        public string PaymentMethod { get; set; } = "CASH";

        [Column("CASH_RECEIVED", TypeName = "decimal(15,2)")]
        public decimal? CashReceived { get; set; }

        [Column("CASH_CHANGE", TypeName = "decimal(15,2)")]
        public decimal? CashChange { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("STATUS")]
        public string Status { get; set; } = "COMPLETED";

        [MaxLength(500)]
        [Column("CANCEL_REASON")]
        public string? CancelReason { get; set; }

        // Navigation properties
        [ForeignKey("StoreId")]
        public virtual Store Store { get; set; } = null!;

        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }

        [ForeignKey("CashierId")]
        public virtual User Cashier { get; set; } = null!;

        public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
    }
}

