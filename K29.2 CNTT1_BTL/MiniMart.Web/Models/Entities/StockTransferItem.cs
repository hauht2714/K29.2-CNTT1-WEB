using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniMart.Web.Models.Entities
{
    [Table("STOCK_TRANSFER_ITEMS")]
    public class StockTransferItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("ID")]
        public long Id { get; set; }

        [Column("TRANSFER_ID")]
        public long? TransferId { get; set; }

        [Column("QUANTITY")]
        public int? Quantity { get; set; }

        [Column("STORE_PRODUCT_ID")]
        public long? StoreProductId { get; set; }

        [Column("BATCH_ID")]
        public long? BatchId { get; set; }

        // Navigation properties
        [ForeignKey("TransferId")]
        public virtual StockTransfer? Transfer { get; set; }

        [ForeignKey("StoreProductId")]
        public virtual StoreProduct? StoreProduct { get; set; }

        [ForeignKey("BatchId")]
        public virtual ProductBatch? Batch { get; set; }
    }
}

