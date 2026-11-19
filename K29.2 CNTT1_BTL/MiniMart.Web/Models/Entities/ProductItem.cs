using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniMart.Web.Models.Entities
{
    [Table("PRODUCT_ITEMS")]
    public class ProductItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("ID")]
        public long Id { get; set; }

        [Required]
        [Column("BATCH_ID")]
        public long BatchId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("UNIT_BARCODE")]
        public string UnitBarcode { get; set; } = string.Empty;

        [Column("IS_SOLD")]
        public bool IsSold { get; set; } = false;

        [Column("INVOICE_ITEM_ID")]
        public long? InvoiceItemId { get; set; }

        [Column("IS_EXPIRED")]
        public bool IsExpired { get; set; } = false;

        // Navigation properties
        [ForeignKey("BatchId")]
        public virtual ProductBatch Batch { get; set; } = null!;
    }
}

