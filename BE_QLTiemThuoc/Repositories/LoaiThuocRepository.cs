using BE_QLTiemThuoc.Data;
using BE_QLTiemThuoc.Model.Thuoc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace BE_QLTiemThuoc.Repositories
{
    public class LoaiThuocRepository
    {
        private readonly AppDbContext _context;
        public LoaiThuocRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<LoaiThuoc>> GetAllAsync()
        {
            return await _context.LoaiThuoc.OrderBy(x => x.TenLoaiThuoc).ToListAsync();
        }

        public async Task<LoaiThuoc?> GetByIdAsync(string ma)
        {
            if (string.IsNullOrWhiteSpace(ma)) return null;
            return await _context.LoaiThuoc.FindAsync(ma);
        }

        public async Task<List<LoaiThuoc>> GetByNhomAsync(string maNhom)
        {
            if (string.IsNullOrWhiteSpace(maNhom)) return new List<LoaiThuoc>();
            return await _context.LoaiThuoc.Where(l => l.MaNhomLoai == maNhom).ToListAsync();
        }

        public async Task CreateAsync(LoaiThuoc item)
        {
            await _context.LoaiThuoc.AddAsync(item);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(LoaiThuoc item)
        {
            _context.LoaiThuoc.Update(item);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(LoaiThuoc item)
        {
            _context.LoaiThuoc.Remove(item);
            await _context.SaveChangesAsync();
        }
    }
}
