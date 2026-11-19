using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniMart.Web.Models.Entities
{
    [Table("IMPORT_RECEIPT_ITEMS")]
    public class ImportReceiptItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("ID")]
        public long Id { get; set; }

        [Required]
        [Column("RECEIPT_ID")]
        public long ReceiptId { get; set; }

        [Required]
        [Column("PRODUCT_ID")]
        public long ProductId { get; set; }

        [Required]
        [Column("QUANTITY")]
        public int Quantity { get; set; }

        [Required]
        [Column("IMPORT_PRICE", TypeName = "decimal(15,2)")]
        public decimal ImportPrice { get; set; }

        [Required]
        [Column("SUBTOTAL", TypeName = "decimal(15,2)")]
        public decimal Subtotal { get; set; }

        [Column("EXPIRY_DATE")]
        public DateTime? ExpiryDate { get; set; }

        // Navigation properties
        [ForeignKey("ReceiptId")]
        public virtual ImportReceipt Receipt { get; set; } = null!;

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;
    }
}

