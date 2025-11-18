using System.ComponentModel.DataAnnotations;
using BE_QLTiemThuoc.Dto;

namespace BE_QLTiemThuoc.Dto
{
    public class HuyThuocRequestDto
    {
        [Required]
        [StringLength(20)]
        public string LoaiHuy { get; set; } // "TU_KHO" hoặc "TU_HOA_DON"

        public HuyTuKhoDto HuyTuKho { get; set; }
        public HuyTuHoaDonDto HuyTuHoaDon { get; set; }
    }
}