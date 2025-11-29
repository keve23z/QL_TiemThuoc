using BE_QLTiemThuoc.Data;
using BE_QLTiemThuoc.Model.Ban;
using Microsoft.EntityFrameworkCore;

namespace BE_QLTiemThuoc.Repositories
{
 public class DanhGiaThuocRepository
 {
 private readonly AppDbContext _context;
 public DanhGiaThuocRepository(AppDbContext context)
 {
 _context = context;
 }

 public Task<DanhGiaThuoc?> GetByIdAsync(string maDanhGia) => _context.DanhGiaThuocs.FirstOrDefaultAsync(d => d.MaDanhGia == maDanhGia);
 public Task<DanhGiaThuoc?> GetByKhachHangAndThuocAsync(string maKh, string maThuoc) => _context.DanhGiaThuocs.FirstOrDefaultAsync(d => d.MaKH == maKh && d.MaThuoc == maThuoc);
 public async Task AddAsync(DanhGiaThuoc dg)
 {
 await _context.DanhGiaThuocs.AddAsync(dg);
 }
 public void Remove(DanhGiaThuoc dg)
 {
 _context.DanhGiaThuocs.Remove(dg);
 }
 public Task SaveChangesAsync() => _context.SaveChangesAsync();
 public Task<List<DanhGiaThuoc>> GetByThuocAsync(string maThuoc) => _context.DanhGiaThuocs.Where(d => d.MaThuoc == maThuoc).OrderByDescending(d=>d.NgayDanhGia).ToListAsync();
 public Task<List<DanhGiaThuoc>> GetByKhachHangAsync(string maKh) => _context.DanhGiaThuocs.Where(d => d.MaKH == maKh).OrderByDescending(d=>d.NgayDanhGia).ToListAsync();
 public Task<bool> HasCompletedOrderForThuocAsync(string maKh, string maThuoc)
 {
 // TrangThaiGiaoHang =3 => Completed
 return _context.HoaDons
 .Join(_context.ChiTietHoaDons,
 hd => hd.MaHD,
 ct => ct.MaHD,
 (hd, ct) => new { hd, ct })
 .AnyAsync(x => x.hd.MaKH == maKh && x.ct.MaThuoc == maThuoc && x.hd.TrangThaiGiaoHang ==3);
 }
 }
}
