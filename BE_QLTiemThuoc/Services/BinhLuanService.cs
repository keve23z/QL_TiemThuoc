using BE_QLTiemThuoc.Dto;
using BE_QLTiemThuoc.Model.Ban;
using BE_QLTiemThuoc.Repositories;
using Microsoft.EntityFrameworkCore; // added
using Microsoft.Data.SqlClient; // added

namespace BE_QLTiemThuoc.Services
{
 public class BinhLuanService
 {
 private readonly BinhLuanRepository _repo;
 public BinhLuanService(BinhLuanRepository repo){ _repo = repo; }

 public async Task<BinhLuanViewDto?> GetAsync(string maBL)
 {
 var target = await _repo.GetByIdAsync(maBL);
 if(target==null) return null;
 var flat = await _repo.GetAllByThuocAsync(target.MaThuoc); // load all for product
 return BuildSubTree(flat, target.MaBL);
 }

 public async Task<List<BinhLuanViewDto>> GetByThuocAsync(string maThuoc)
 {
 var flat = await _repo.GetAllByThuocAsync(maThuoc);
 return BuildForest(flat);
 }

 public async Task<BinhLuanViewDto> CreateAsync(BinhLuanCreateDto dto)
 {
 if(string.IsNullOrWhiteSpace(dto.MaThuoc)) throw new Exception("MaThuoc required");
 if(string.IsNullOrWhiteSpace(dto.NoiDung)) throw new Exception("NoiDung required");
 var hasAuthor = !string.IsNullOrWhiteSpace(dto.MaKH) ^ !string.IsNullOrWhiteSpace(dto.MaNV); // XOR
 if(!hasAuthor) throw new Exception("Provide MaKH or MaNV (exactly one)");

 if(!string.IsNullOrWhiteSpace(dto.TraLoiChoBinhLuan))
 {
 var parent = await _repo.GetByIdAsync(dto.TraLoiChoBinhLuan) ?? throw new Exception("Parent comment not found");
 if(parent.MaThuoc != dto.MaThuoc) throw new Exception("Parent belongs to different product");
 // Single direct reply per parent (root or reply). This allows a linear chain.
 var flat = await _repo.GetAllByThuocAsync(dto.MaThuoc);
 if(flat.Any(b=>b.TraLoiChoBinhLuan == dto.TraLoiChoBinhLuan)) throw new Exception("Bình lu?n này ?ã ???c tr? l?i. Không th? tr? l?i thêm.");
 }

 var e = new BinhLuan{
 MaBL = Guid.NewGuid().ToString("N").Substring(0,20),
 MaThuoc = dto.MaThuoc,
 MaKH = dto.MaKH,
 MaNV = dto.MaNV,
 NoiDung = dto.NoiDung,
 TraLoiChoBinhLuan = dto.TraLoiChoBinhLuan,
 ThoiGian = DateTime.UtcNow
 };
 await _repo.AddAsync(e);
 try
 {
 await _repo.SaveAsync();
 }
 catch (DbUpdateException ex)
 {
 if(ex.InnerException is SqlException sql)
 {
 if(sql.Number==2601 || sql.Number==2627 || sql.Message.Contains("Bình lu?n này ?ã ???c tr? l?i"))
 throw new Exception("Bình lu?n này ?ã ???c tr? l?i. Không th? tr? l?i thêm.");
 }
 throw; // rethrow other errors
 }
 return ToDto(e);
 }

 public async Task<bool> DeleteAsync(string maBL)
 {
 var e = await _repo.GetByIdAsync(maBL);
 if(e==null) return false;
 _repo.Remove(e); // cascade replies
 await _repo.SaveAsync();
 return true;
 }

 private static List<BinhLuanViewDto> BuildForest(List<BinhLuan> flat)
 {
 var childrenMap = BuildChildrenMap(flat);
 return flat
 .Where(b=>b.TraLoiChoBinhLuan==null)
 .OrderByDescending(b=>b.ThoiGian)
 .Select(root=>ToDtoRecursive(root, childrenMap))
 .ToList();
 }

 private static BinhLuanViewDto BuildSubTree(List<BinhLuan> flat, string rootId)
 {
 var childrenMap = BuildChildrenMap(flat);
 var root = flat.First(b=>b.MaBL==rootId);
 return ToDtoRecursive(root, childrenMap);
 }

 private static Dictionary<string,List<BinhLuan>> BuildChildrenMap(List<BinhLuan> flat)
 {
 var dict = new Dictionary<string,List<BinhLuan>>();
 foreach(var bl in flat)
 {
 if(bl.TraLoiChoBinhLuan!=null)
 {
 if(!dict.TryGetValue(bl.TraLoiChoBinhLuan, out var list))
 {
 list = new List<BinhLuan>();
 dict[bl.TraLoiChoBinhLuan] = list;
 }
 list.Add(bl);
 }
 }
 return dict;
 }

 private static BinhLuanViewDto ToDtoRecursive(BinhLuan e, Dictionary<string,List<BinhLuan>> childrenMap)
 {
 var dto = ToDto(e);
 if(childrenMap.TryGetValue(e.MaBL, out var kids))
 dto.Replies = kids.OrderByDescending(c=>c.ThoiGian).Select(c=>ToDtoRecursive(c, childrenMap)).ToList();
 return dto;
 }

 private static BinhLuanViewDto ToDto(BinhLuan e) => new(){
 MaBL = e.MaBL,
 MaThuoc = e.MaThuoc,
 MaKH = e.MaKH,
 MaNV = e.MaNV,
 NoiDung = e.NoiDung,
 ThoiGian = e.ThoiGian,
 TraLoiChoBinhLuan = e.TraLoiChoBinhLuan,
 Replies = new List<BinhLuanViewDto>()
 };
 }
}
