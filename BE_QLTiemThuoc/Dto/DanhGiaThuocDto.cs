namespace BE_QLTiemThuoc.Dto
{
 public class DanhGiaThuocCreateDto
 {
 public string MaKH { get; set; } = null!;
 public string MaThuoc { get; set; } = null!;
 public int SoSao { get; set; }
 public string? NoiDung { get; set; }
 }

 public class DanhGiaThuocUpdateDto
 {
 public int SoSao { get; set; }
 public string? NoiDung { get; set; }
 }

 public class DanhGiaThuocViewDto
 {
 public string MaDanhGia { get; set; } = null!;
 public string MaKH { get; set; } = null!;
 public string MaThuoc { get; set; } = null!;
 public int SoSao { get; set; }
 public string? NoiDung { get; set; }
 public DateTime NgayDanhGia { get; set; }
 }
}
