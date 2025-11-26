using BE_QLTiemThuoc.Data;
using BE_QLTiemThuoc.Model.Kho;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BE_QLTiemThuoc.Repositories
{
    public class PhieuXuLyHoanHuyRepository
    {
        private readonly AppDbContext _context;

        public PhieuXuLyHoanHuyRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<PhieuXuLyHoanHuy>> GetAllAsync(DateTime? start = null, DateTime? end = null, string? maNV_Tao = null, bool? loaiNguon = null, int? trangThai = null)
        {
            var q = _context.PhieuXuLyHoanHuys
                .AsNoTracking()
                .AsQueryable();

            if (start.HasValue)
            {
                var s = start.Value.Date;
                q = q.Where(p => p.NgayTao.HasValue && p.NgayTao.Value >= s);
            }
            if (end.HasValue)
            {
                var e = end.Value.Date.AddDays(1).AddTicks(-1);
                q = q.Where(p => p.NgayTao.HasValue && p.NgayTao.Value <= e);
            }

            if (!string.IsNullOrWhiteSpace(maNV_Tao))
            {
                q = q.Where(p => p.MaNV_Tao == maNV_Tao);
            }

            if (loaiNguon.HasValue)
            {
                q = q.Where(p => p.LoaiNguon.HasValue && p.LoaiNguon.Value == loaiNguon.Value);
            }

            if (trangThai.HasValue)
            {
                q = q.Where(p => p.TrangThai.HasValue && p.TrangThai.Value == trangThai.Value);
            }

            return await q.OrderByDescending(p => p.NgayTao).ToListAsync();
        }

        public async Task<dynamic?> GetDetailsByMaAsync(string maPXH)
        {
            if (string.IsNullOrWhiteSpace(maPXH)) return null;

            var header = await _context.PhieuXuLyHoanHuys
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.MaPXH == maPXH);

            if (header == null) return null;

            var ctList = await _context.Set<ChiTietPhieuXuLy>()
                .AsNoTracking()
                .Where(ct => ct.MaPXH == maPXH)
                .ToListAsync();

            dynamic outObj = new System.Dynamic.ExpandoObject();
            var od = (IDictionary<string, object>)outObj;
            od["Phieu"] = header;
            od["ChiTiets"] = ctList;
            return outObj;
        }
    }
}
