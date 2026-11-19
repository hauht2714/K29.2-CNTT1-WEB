using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MiniMart.Web.Models.ViewModels
{
    public class CreateImportReceiptDto
    {
        [Required(ErrorMessage = "Vui lòng chọn nhà cung cấp")]
        public long SupplierId { get; set; }

        public long StoreId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn ngày nhập")]
        public DateTime ImportDate { get; set; } = DateTime.Today;

        public string? Note { get; set; }

        public bool CompleteImmediately { get; set; } = true;

        public List<ImportItemInputDto> Items { get; set; } = new();
    }

    public class ImportItemInputDto
    {
        [Required]
        public long ProductId { get; set; }

        [Range(1, 100000, ErrorMessage = "Số lượng phải từ 1 trở lên")]
        public int Quantity { get; set; }

        [Range(0, 1000000000, ErrorMessage = "Giá nhập không hợp lệ")]
        public decimal ImportPrice { get; set; }

        public DateTime? ExpiryDate { get; set; }
    }
}

