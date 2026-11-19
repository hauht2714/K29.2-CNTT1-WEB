using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniMart.Web.Models.Entities
{
    [Table("PRODUCTS")]
    public class Product : BaseEntity
    {
        [Required]
        [MaxLength(20)]
        [Column("PRODUCT_CODE")]
        public string ProductCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        [Column("NAME")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        [Column("DESCRIPTION")]
        public string? Description { get; set; }

        [Column("CATEGORY_ID")]
        public long? CategoryId { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("UNIT")]
        public string Unit { get; set; } = "Cái";

        [MaxLength(50)]
        [Column("BARCODE")]
        public string? Barcode { get; set; }

        [MaxLength(500)]
        [Column("IMAGE_URL")]
        public string? ImageUrl { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("STATUS")]
        public string Status { get; set; } = "ACTIVE";

        [Column("VERSION")]
        public int Version { get; set; } = 0;

        [Column("SUPPLIER_ID")]
        public long? SupplierId { get; set; }

        [Column("DEFAULT_PRICE", TypeName = "decimal(18,2)")]
        public decimal? DefaultPrice { get; set; }

        [NotMapped]
        public decimal? LastImportPrice { get; set; }

        // Navigation properties
        [ForeignKey("CategoryId")]
        public virtual Category? Category { get; set; }

        [ForeignKey("SupplierId")]
        public virtual Supplier? Supplier { get; set; }

        public virtual ICollection<StoreProduct> StoreProducts { get; set; } = new List<StoreProduct>();
    }
}

