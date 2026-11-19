using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniMart.Web.Models.Entities
{
    [Table("INVOICE_ITEMS")]
    public class InvoiceItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("ID")]
        public long Id { get; set; }

        [Required]
        [Column("INVOICE_ID")]
        public long InvoiceId { get; set; }

        [Required]
        [Column("PRODUCT_ID")]
        public long ProductId { get; set; }

        [Required]
        [MaxLength(200)]
        [Column("PRODUCT_NAME")]
        public string ProductName { get; set; } = string.Empty;

        [Required]
        [Column("QUANTITY")]
        public int Quantity { get; set; }

        [Required]
        [Column("UNIT_PRICE", TypeName = "decimal(15,2)")]
        public decimal UnitPrice { get; set; }

        [Column("DISCOUNT", TypeName = "decimal(15,2)")]
        public decimal Discount { get; set; } = 0;

        [Required]
        [Column("SUBTOTAL", TypeName = "decimal(15,2)")]
        public decimal Subtotal { get; set; }

        [Column("STORE_PRODUCT_ID")]
        public long? StoreProductId { get; set; }

        // Navigation properties
        [ForeignKey("InvoiceId")]
        public virtual Invoice Invoice { get; set; } = null!;

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;

        [ForeignKey("StoreProductId")]
        public virtual StoreProduct? StoreProduct { get; set; }

        public virtual ICollection<InvoiceItemBatch> InvoiceItemBatches { get; set; } = new List<InvoiceItemBatch>();
    }
}

