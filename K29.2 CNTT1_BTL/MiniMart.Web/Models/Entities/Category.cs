using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniMart.Web.Models.Entities
{
    [Table("CATEGORIES")]
    public class Category : BaseEntity
    {
        [Required]
        [MaxLength(100)]
        [Column("NAME")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        [Column("DESCRIPTION")]
        public string? Description { get; set; }

        [Column("PARENT_ID")]
        public long? ParentId { get; set; }

        [Column("CATEGORY_LEVEL")]
        public int CategoryLevel { get; set; } = 1;

        [Column("STORE_ID")]
        public long? StoreId { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("STATUS")]
        public string Status { get; set; } = "ACTIVE";

        // Navigation properties
        [ForeignKey("ParentId")]
        public virtual Category? ParentCategory { get; set; }
        public virtual ICollection<Category> SubCategories { get; set; } = new List<Category>();

        [ForeignKey("StoreId")]
        public virtual Store? Store { get; set; }

        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }
}

