using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniMart.Web.Models.Entities
{
    [Table("STOCK_TRANSFERS")]
    public class StockTransfer
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("ID")]
        public long Id { get; set; }

        [Column("FROM_STORE_ID")]
        public long? FromStoreId { get; set; }

        [Column("TO_STORE_ID")]
        public long? ToStoreId { get; set; }

        [MaxLength(20)]
        [Column("STATUS")]
        public string? Status { get; set; }

        [MaxLength(20)]
        [Column("TRANSFER_CODE")]
        public string? TransferCode { get; set; }

        [MaxLength(500)]
        [Column("NOTE")]
        public string? Note { get; set; }

        [Column("CREATED_AT")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("FromStoreId")]
        public virtual Store? FromStore { get; set; }

        [ForeignKey("ToStoreId")]
        public virtual Store? ToStore { get; set; }

        public virtual ICollection<StockTransferItem> StockTransferItems { get; set; } = new List<StockTransferItem>();
    }
}

