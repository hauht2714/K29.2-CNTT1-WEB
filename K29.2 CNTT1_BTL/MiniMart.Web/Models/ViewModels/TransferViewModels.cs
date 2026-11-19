using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MiniMart.Web.Models.ViewModels
{
    public class CreateTransferDto
    {
        [Required(ErrorMessage = "Vui lòng chọn chi nhánh xuất kho")]
        public long FromStoreId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn chi nhánh nhận hàng")]
        public long ToStoreId { get; set; }

        public string? Note { get; set; }

        public bool CompleteImmediately { get; set; } = true;

        public List<TransferItemInputDto> Items { get; set; } = new();
    }

    public class TransferItemInputDto
    {
        [Required]
        public long StoreProductId { get; set; }

        [Range(1, 100000, ErrorMessage = "Số lượng phải lớn hơn 0")]
        public int Quantity { get; set; }
    }
}

