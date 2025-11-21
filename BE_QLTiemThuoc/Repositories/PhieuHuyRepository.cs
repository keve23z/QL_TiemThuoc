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

        public async Task<List<PhieuHuy>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, bool? loaiHuy = null)
        {
            var query = _context.Set<PhieuHuy>()
                .Include(p => p.ChiTietPhieuHuys)
                .Where(p => p.NgayHuy >= startDate && p.NgayHuy <= endDate);

            if (loaiHuy.HasValue)
            {
                query = query.Where(p => p.LoaiHuy == loaiHuy.Value);
            }

            return await query
                .OrderByDescending(p => p.NgayHuy)
                .ToListAsync();
        }

        public async Task<Dictionary<string, string?>> GetNhanVienNamesAsync(List<string> maNvs)
        {
            if (maNvs == null || !maNvs.Any()) return new Dictionary<string, string?>();

            var list = await _context.Set<Model.NhanVien>()
                .Where(nv => maNvs.Contains(nv.MaNV))
                .Select(nv => new { nv.MaNV, nv.HoTen })
                .ToListAsync();

            return list.ToDictionary(x => x.MaNV, x => x.HoTen);
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

        public async Task DeleteChiTietPhieuHuyByMaPHAsync(string maPH)
        {
            var items = await _context.Set<ChiTietPhieuHuy>().Where(ct => ct.MaPH == maPH).ToListAsync();
            if (items.Any())
            {
                _context.Set<ChiTietPhieuHuy>().RemoveRange(items);
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdatePhieuHuyAsync(PhieuHuy phieuHuy)
        {
            _context.Set<PhieuHuy>().Update(phieuHuy);
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

        public async Task<List<TonKho>> GetTonKhoByMaLoListAsync(List<string> maLoList)
        {
            return await _context.Set<TonKho>()
                .Where(tk => maLoList.Contains(tk.MaLo))
                .Include(tk => tk.Thuoc)
                .ToListAsync();
        }

        public async Task<List<ChiTietHoaDon>> GetChiTietHoaDonByMaHDAsync(string maHD)
        {
            return await _context.Set<ChiTietHoaDon>()
                .Where(ct => ct.MaHD == maHD)
                .ToListAsync();
        }

        public async Task UpdateChiTietHoaDonRangeAsync(IEnumerable<ChiTietHoaDon> items)
        {
            _context.Set<ChiTietHoaDon>().UpdateRange(items);
            await _context.SaveChangesAsync();
        }

        public async Task DeletePhieuHuyAsync(string maPH)
        {
            var ph = await _context.Set<PhieuHuy>().FirstOrDefaultAsync(p => p.MaPH == maPH);
            if (ph != null)
            {
                _context.Set<PhieuHuy>().Remove(ph);
                await _context.SaveChangesAsync();
            }
        }
    }
}