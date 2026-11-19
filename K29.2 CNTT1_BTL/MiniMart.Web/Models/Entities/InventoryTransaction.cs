using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniMart.Web.Models.Entities
{
    [Table("INVENTORY_TRANSACTIONS")]
    public class InventoryTransaction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("ID")]
        public long Id { get; set; }

        [Column("STORE_ID")]
        public long? StoreId { get; set; }

        [Column("PRODUCT_ID")]
        public long? ProductId { get; set; }

        [MaxLength(20)]
        [Column("TYPE")]
        public string? Type { get; set; }

        [Column("QUANTITY")]
        public int? Quantity { get; set; }

        [Column("REFERENCE_ID")]
        public long? ReferenceId { get; set; }

        [MaxLength(500)]
        [Column("NOTE")]
        public string? Note { get; set; }

        [Column("CREATED_AT")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("StoreId")]
        public virtual Store? Store { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }
    }
}

