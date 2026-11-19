using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniMart.Web.Models.Entities
{
    [Table("PRICE_HISTORY")]
    public class PriceHistory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("ID")]
        public long Id { get; set; }

        [Column("STORE_PRODUCT_ID")]
        public long? StoreProductId { get; set; }

        [Column("PRICE", TypeName = "decimal(15,2)")]
        public decimal? Price { get; set; }

        [Column("START_DATE")]
        public DateTime? StartDate { get; set; }

        [Column("END_DATE")]
        public DateTime? EndDate { get; set; }

        // Navigation properties
        [ForeignKey("StoreProductId")]
        public virtual StoreProduct? StoreProduct { get; set; }
    }
}

