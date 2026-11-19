using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniMart.Web.Models.Entities
{
    [Table("PRODUCT_BATCHES")]
    public class ProductBatch
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("ID")]
        public long Id { get; set; }

        [Required]
        [Column("STORE_PRODUCT_ID")]
        public long StoreProductId { get; set; }

        [Column("IMPORT_DATE")]
        public DateTime? ImportDate { get; set; }

        [Column("EXPIRY_DATE")]
        public DateTime? ExpiryDate { get; set; }

        [Column("QUANTITY")]
        public int? Quantity { get; set; }

        [Column("REMAINING_QUANTITY")]
        public int? RemainingQuantity { get; set; }

        [Column("IMPORT_PRICE", TypeName = "decimal(15,2)")]
        public decimal? ImportPrice { get; set; }

        [MaxLength(50)]
        [Column("BATCH_BARCODE")]
        public string? BatchBarcode { get; set; }

        // Navigation properties
        [ForeignKey("StoreProductId")]
        public virtual StoreProduct StoreProduct { get; set; } = null!;

        public virtual ICollection<ProductItem> ProductItems { get; set; } = new List<ProductItem>();
        public virtual ICollection<InvoiceItemBatch> InvoiceItemBatches { get; set; } = new List<InvoiceItemBatch>();
    }
}

