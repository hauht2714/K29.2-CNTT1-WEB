using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniMart.Web.Models.Entities
{
    [Table("CUSTOMERS")]
    public class Customer : BaseEntity
    {
        [Required]
        [MaxLength(20)]
        [Column("MEMBER_CODE")]
        public string MemberCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        [Column("FULL_NAME")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        [Column("PHONE")]
        public string Phone { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("EMAIL")]
        public string? Email { get; set; }

        [Column("DATE_OF_BIRTH")]
        public DateTime? DateOfBirth { get; set; }

        [MaxLength(500)]
        [Column("ADDRESS")]
        public string? Address { get; set; }

        [Column("TOTAL_POINTS")]
        public int TotalPoints { get; set; } = 0;

        [Column("ACCUMULATED_POINTS")]
        public int AccumulatedPoints { get; set; } = 0;

        [MaxLength(20)]
        [Column("MEMBERSHIP_TIER")]
        public string MembershipTier { get; set; } = "MEMBER";

        [MaxLength(20)]
        [Column("STATUS")]
        public string Status { get; set; } = "ACTIVE";

        [Column("LAST_TRANSACTION_DATE")]
        public DateTime? LastTransactionDate { get; set; }

        [Column("VERSION")]
        public int Version { get; set; } = 0;

        // Navigation properties
        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    }
}

