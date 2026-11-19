using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniMart.Web.Models.Entities
{
    [Table("IMPORT_RECEIPTS")]
    public class ImportReceipt : BaseEntity
    {
        [Required]
        [MaxLength(20)]
        [Column("RECEIPT_CODE")]
        public string ReceiptCode { get; set; } = string.Empty;

        [Required]
        [Column("SUPPLIER_ID")]
        public long SupplierId { get; set; }

        [Required]
        [Column("STORE_ID")]
        public long StoreId { get; set; }

        [Required]
        [Column("IMPORT_DATE")]
        public DateTime ImportDate { get; set; } = DateTime.Today;

        [Required]
        [Column("TOTAL_AMOUNT", TypeName = "decimal(15,2)")]
        public decimal TotalAmount { get; set; } = 0;

        [Required]
        [MaxLength(20)]
        [Column("STATUS")]
        public string Status { get; set; } = "DRAFT";

        [MaxLength(500)]
        [Column("NOTE")]
        public string? Note { get; set; }

        [Required]
        [Column("CREATED_BY")]
        public long CreatedBy { get; set; }

        [Column("CONFIRMED_BY")]
        public long? ConfirmedBy { get; set; }

        [Column("CANCELLED_BY")]
        public long? CancelledBy { get; set; }

        // Navigation properties
        [ForeignKey("SupplierId")]
        public virtual Supplier Supplier { get; set; } = null!;

        [ForeignKey("StoreId")]
        public virtual Store Store { get; set; } = null!;

        [ForeignKey("CreatedBy")]
        public virtual User CreatedByUser { get; set; } = null!;

        [ForeignKey("ConfirmedBy")]
        public virtual User? ConfirmedByUser { get; set; }

        [ForeignKey("CancelledBy")]
        public virtual User? CancelledByUser { get; set; }

        public virtual ICollection<ImportReceiptItem> ImportReceiptItems { get; set; } = new List<ImportReceiptItem>();
    }
}

