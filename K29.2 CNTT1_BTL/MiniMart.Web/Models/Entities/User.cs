using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniMart.Web.Models.Entities
{
    [Table("USERS")]
    public class User : BaseEntity
    {
        [Required]
        [MaxLength(50)]
        [Column("USERNAME")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        [Column("PASSWORD")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Column("FULL_NAME")]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("EMAIL")]
        public string? Email { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("ROLE")]
        public string Role { get; set; } = "CASHIER";

        [Column("STORE_ID")]
        public long? StoreId { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("STATUS")]
        public string Status { get; set; } = "ACTIVE";

        [Column("FAILED_LOGIN_ATTEMPTS")]
        public int FailedLoginAttempts { get; set; } = 0;

        [Column("LOCKED_UNTIL")]
        public DateTime? LockedUntil { get; set; }

        [Column("VERSION")]
        public int Version { get; set; } = 0;

        // Navigation properties
        [ForeignKey("StoreId")]
        public virtual Store? Store { get; set; }
    }
}

