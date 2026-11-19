using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniMart.Web.Models.Entities
{
    [Table("PROMOTION_ITEMS")]
    public class PromotionItem
    {
        [Key]
        [Column("ID")]
        public long Id { get; set; }

        [Column("PROMOTION_ID")]
        public long? PromotionId { get; set; }

        [Column("PRODUCT_ID")]
        public long? ProductId { get; set; }

        [Column("DISCOUNT_PERCENT", TypeName = "decimal(5,2)")]
        public decimal? DiscountPercent { get; set; }

        [Column("DISCOUNT_AMOUNT", TypeName = "decimal(18,2)")]
        public decimal? DiscountAmount { get; set; }

        [Column("BUY_QTY")]
        public int? BuyQty { get; set; }

        [Column("GET_QTY")]
        public int? GetQty { get; set; }

        // Navigation properties
        [ForeignKey("PromotionId")]
        public virtual Promotion? Promotion { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }
    }
}

