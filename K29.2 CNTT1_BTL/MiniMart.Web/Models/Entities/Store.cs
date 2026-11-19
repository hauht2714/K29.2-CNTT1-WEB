using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniMart.Web.Models.Entities
{
    [Table("STORES")]
    public class Store : BaseEntity
    {
        [Required]
        [MaxLength(20)]
        [Column("STORE_CODE")]
        public string StoreCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        [Column("NAME")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        [Column("ADDRESS")]
        public string Address { get; set; } = string.Empty;

        [MaxLength(20)]
        [Column("PHONE")]
        public string? Phone { get; set; }

        [MaxLength(100)]
        [Column("EMAIL")]
        public string? Email { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("STORE_TYPE")]
        public string StoreType { get; set; } = "MINI_MART";

        [Required]
        [MaxLength(20)]
        [Column("STATUS")]
        public string Status { get; set; } = "ACTIVE";

        // Navigation properties
        public virtual ICollection<User> Users { get; set; } = new List<User>();
        public virtual ICollection<StoreProduct> StoreProducts { get; set; } = new List<StoreProduct>();
        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
        public virtual ICollection<ImportReceipt> ImportReceipts { get; set; } = new List<ImportReceipt>();
    }
}

