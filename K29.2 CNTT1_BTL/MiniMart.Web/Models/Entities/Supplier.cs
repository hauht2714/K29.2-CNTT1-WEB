using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniMart.Web.Models.Entities
{
    [Table("SUPPLIERS")]
    public class Supplier : BaseEntity
    {
        [Required]
        [MaxLength(20)]
        [Column("SUPPLIER_CODE")]
        public string SupplierCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        [Column("NAME")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        [Column("ADDRESS")]
        public string? Address { get; set; }

        [MaxLength(20)]
        [Column("PHONE")]
        public string? Phone { get; set; }

        [MaxLength(100)]
        [Column("EMAIL")]
        public string? Email { get; set; }

        [MaxLength(20)]
        [Column("TAX_CODE")]
        public string? TaxCode { get; set; }

        [MaxLength(100)]
        [Column("CONTACT_PERSON")]
        public string? ContactPerson { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("STATUS")]
        public string Status { get; set; } = "ACTIVE";

        // Navigation properties
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
        public virtual ICollection<ImportReceipt> ImportReceipts { get; set; } = new List<ImportReceipt>();
    }
}

