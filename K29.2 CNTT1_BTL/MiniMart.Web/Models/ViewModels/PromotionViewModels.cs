using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MiniMart.Web.Models.ViewModels
{
    public class PromotionDto
    {
        public long Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên chương trình")]
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Vui lòng chọn ngày kết thúc")]
        public DateTime EndDate { get; set; } = DateTime.Today.AddDays(7);

        [Required]
        public string PromoType { get; set; } = "PERCENTAGE";

        public bool IsActive { get; set; } = true;

        [MaxLength(500)]
        public string? Description { get; set; }

        public List<PromotionItemDto> Items { get; set; } = new();
    }

    public class PromotionItemDto
    {
        public long Id { get; set; }

        [Required]
        public long ProductId { get; set; }

        public decimal? DiscountPercent { get; set; }

        public decimal? DiscountAmount { get; set; }

        public int? BuyQty { get; set; }

        public int? GetQty { get; set; }
    }
}

