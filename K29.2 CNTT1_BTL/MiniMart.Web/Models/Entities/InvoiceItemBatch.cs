using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniMart.Web.Models.Entities
{
    [Table("INVOICE_ITEM_BATCHES")]
    public class InvoiceItemBatch
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("ID")]
        public long Id { get; set; }

        [Required]
        [Column("INVOICE_ITEM_ID")]
        public long InvoiceItemId { get; set; }

        [Required]
        [Column("BATCH_ID")]
        public long BatchId { get; set; }

        [Required]
        [Column("QUANTITY")]
        public int Quantity { get; set; }

        // Navigation properties
        [ForeignKey("InvoiceItemId")]
        public virtual InvoiceItem InvoiceItem { get; set; } = null!;

        [ForeignKey("BatchId")]
        public virtual ProductBatch Batch { get; set; } = null!;
    }
}

