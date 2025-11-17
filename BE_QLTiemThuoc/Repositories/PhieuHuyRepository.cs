using BE_QLTiemThuoc.Data;
using BE_QLTiemThuoc.Model.Kho;
using Microsoft.EntityFrameworkCore;
using BE_QLTiemThuoc.Model;

namespace BE_QLTiemThuoc.Repositories
{
    public class PhieuHuyRepository
    {
        private readonly AppDbContext _context;

        public PhieuHuyRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PhieuHuy?> GetByIdAsync(string maPH)
        {
            return await _context.Set<PhieuHuy>()
                .Include(p => p.ChiTietPhieuHuys)
                .ThenInclude(ct => ct.TonKho)
                .ThenInclude(t => t.Thuoc)
                .FirstOrDefaultAsync(p => p.MaPH == maPH);
        }

        public async Task<List<PhieuHuy>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.Set<PhieuHuy>()
                .Include(p => p.ChiTietPhieuHuys)
                .Where(p => p.NgayHuy >= startDate && p.NgayHuy <= endDate)
                .OrderByDescending(p => p.NgayHuy)
                .ToListAsync();
        }

        public async Task AddAsync(PhieuHuy phieuHuy)
        {
            _context.Set<PhieuHuy>().Add(phieuHuy);
            await _context.SaveChangesAsync();
        }

        public async Task AddChiTietRangeAsync(IEnumerable<ChiTietPhieuHuy> chiTietItems)
        {
            _context.Set<ChiTietPhieuHuy>().AddRange(chiTietItems);
            await _context.SaveChangesAsync();
        }

        public async Task<TonKho?> GetTonKhoByMaLoAsync(string maLo)
        {
            return await _context.TonKhos
                .Include(t => t.Thuoc)
                .FirstOrDefaultAsync(t => t.MaLo == maLo);
        }

        public async Task UpdateTonKhoAsync(TonKho tonKho)
        {
            _context.TonKhos.Update(tonKho);
            await _context.SaveChangesAsync();
        }

        public async Task<HoaDon?> GetHoaDonByIdAsync(string maHD)
        {
            return await _context.HoaDons.FirstOrDefaultAsync(h => h.MaHD == maHD);
        }

        public async Task UpdateHoaDonAsync(HoaDon hoaDon)
        {
            _context.HoaDons.Update(hoaDon);
            await _context.SaveChangesAsync();
        }

        public async Task<int> GetTongSoLuongHoaDonAsync(string maHD)
        {
            return await _context.Set<ChiTietHoaDon>()
                .Where(ct => ct.MaHD == maHD)
                .SumAsync(ct => ct.SoLuong);
        }
    }
}