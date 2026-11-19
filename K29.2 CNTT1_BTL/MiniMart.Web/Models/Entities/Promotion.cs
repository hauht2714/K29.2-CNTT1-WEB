using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MiniMart.Web.Models.Entities
{
    [Table("PROMOTIONS")]
    public class Promotion
    {
        [Key]
        [Column("ID")]
        public long Id { get; set; }

        [Required]
        [MaxLength(255)]
        [Column("NAME")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("START_DATE")]
        public DateTime StartDate { get; set; }

        [Required]
        [Column("END_DATE")]
        public DateTime EndDate { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("PROMO_TYPE")]
        public string PromoType { get; set; } = "PERCENTAGE";

        [Column("IS_ACTIVE")]
        public bool IsActive { get; set; } = true;

        [MaxLength(500)]
        [Column("DESCRIPTION")]
        public string? Description { get; set; }

        [Column("CREATED_AT")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual ICollection<PromotionItem> PromotionItems { get; set; } = new List<PromotionItem>();
    }
}

