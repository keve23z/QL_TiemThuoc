using System.ComponentModel.DataAnnotations;

namespace BE_QLTiemThuoc.Dto
{
    public class GiaThuocDto
    {
        // Optional for create (service can generate if missing)
        public string? MaGiaThuoc { get; set; }

        [Required]
        public string MaThuoc { get; set; }

        [Required]
        public string MaLoaiDonVi { get; set; }

        // number of base units (e.g. 1)
        public int SoLuong { get; set; } = 1;

        [Required]
        public decimal DonGia { get; set; }

        // whether this price row is active
        public bool TrangThai { get; set; } = true;
    }
}
