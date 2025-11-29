using BE_QLTiemThuoc.Data;
using BE_QLTiemThuoc.Model.Ban;
using Microsoft.EntityFrameworkCore;

namespace BE_QLTiemThuoc.Repositories
{
 public class BinhLuanRepository
 {
 private readonly AppDbContext _ctx;
 public BinhLuanRepository(AppDbContext ctx){ _ctx = ctx; }

 public Task<BinhLuan?> GetByIdAsync(string id) => _ctx.BinhLuans.FirstOrDefaultAsync(b=>b.MaBL==id);
 public async Task AddAsync(BinhLuan e){ await _ctx.BinhLuans.AddAsync(e); }
 public void Remove(BinhLuan e){ _ctx.BinhLuans.Remove(e); }
 public Task SaveAsync() => _ctx.SaveChangesAsync();
 // All comments for a product (roots + replies)
 public Task<List<BinhLuan>> GetAllByThuocAsync(string maThuoc) => _ctx.BinhLuans.Where(b=>b.MaThuoc==maThuoc).OrderByDescending(b=>b.ThoiGian).ToListAsync();
 // Only root comments (kept for backward compatibility if needed)
 public Task<List<BinhLuan>> GetRootByThuocAsync(string maThuoc) => _ctx.BinhLuans.Where(b=>b.MaThuoc==maThuoc && b.TraLoiChoBinhLuan==null).OrderByDescending(b=>b.ThoiGian).ToListAsync();
 }
}
