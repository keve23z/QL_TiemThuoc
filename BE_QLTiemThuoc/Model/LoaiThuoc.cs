using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BE_QLTiemThuoc.Model
{
    public class LoaiThuoc
    {
        [Key]
        [StringLength(10)]
        public string MaLoaiThuoc { get; set; }

        [Required]
        [StringLength(255)]
        public string TenLoaiThuoc { get; set; }

        [StringLength(10)]
        public string MaNhomLoai { get; set; }

        [StringLength(50)]
        public string Icon { get; set; }

    }
}
