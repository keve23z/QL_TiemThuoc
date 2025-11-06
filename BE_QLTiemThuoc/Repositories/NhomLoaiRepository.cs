using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BE_QLTiemThuoc.Data;
using BE_QLTiemThuoc.Model.Thuoc;
using System.Linq;

namespace BE_QLTiemThuoc.Repositories
{
    public class NhomLoaiRepository
    {
        private readonly AppDbContext _context;

        public NhomLoaiRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<NhomLoai>> GetAllAsync()
        {
            return await _context.NhomLoai.ToListAsync();
        }

        public async Task<NhomLoai?> GetByIdAsync(string maNhom)
        {
            return await _context.NhomLoai.FindAsync(maNhom);
        }

        public async Task<List<LoaiThuoc>> GetLoaiByNhomAsync(string maNhom)
        {
            return await _context.LoaiThuoc.Where(l => l.MaNhomLoai == maNhom).ToListAsync();
        }
    }
}
