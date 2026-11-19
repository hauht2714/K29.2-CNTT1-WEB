using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniMart.Web.Models.Entities
{
    [Table("STORE_PRODUCTS")]
    public class StoreProduct
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("ID")]
        public long Id { get; set; }

        [Required]
        [Column("STORE_ID")]
        public long StoreId { get; set; }

        [Required]
        [Column("PRODUCT_ID")]
        public long ProductId { get; set; }

        [Required]
        [Column("PRICE", TypeName = "decimal(15,2)")]
        public decimal Price { get; set; }

        [Column("STOCK_QUANTITY")]
        public int StockQuantity { get; set; } = 0;

        [Column("MIN_STOCK")]
        public int MinStock { get; set; } = 10;

        [Column("MAX_STOCK")]
        public int MaxStock { get; set; } = 1000;

        [MaxLength(20)]
        [Column("STATUS")]
        public string Status { get; set; } = "ACTIVE";

        [Column("CREATED_AT")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("UPDATED_AT")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("StoreId")]
        public virtual Store Store { get; set; } = null!;

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;

        public virtual ICollection<ProductBatch> ProductBatches { get; set; } = new List<ProductBatch>();
        public virtual ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();
    }
}

