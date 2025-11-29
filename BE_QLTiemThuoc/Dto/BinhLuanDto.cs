namespace BE_QLTiemThuoc.Dto
{
 public class BinhLuanCreateDto
 {
 public string MaThuoc { get; set; } = null!;
 public string? MaKH { get; set; } // one of MaKH or MaNV must be provided (not both)
 public string? MaNV { get; set; }
 public string NoiDung { get; set; } = null!;
 public string? TraLoiChoBinhLuan { get; set; }
 }

 public class BinhLuanViewDto
 {
 public string MaBL { get; set; } = null!;
 public string MaThuoc { get; set; } = null!;
 public string? MaKH { get; set; }
 public string? MaNV { get; set; }
 public string NoiDung { get; set; } = null!;
 public DateTime ThoiGian { get; set; }
 public string? TraLoiChoBinhLuan { get; set; }
 public List<BinhLuanViewDto> Replies { get; set; } = new();
 }
}
