using BE_QLTiemThuoc.Data;
using Microsoft.EntityFrameworkCore;

namespace BE_QLTiemThuoc.Repositories
{
    public class ThuocViewRepository
    {
        private readonly AppDbContext _context;

        public ThuocViewRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<dynamic>> GetChuaTachLeAsync()
        {
            var q = from tk in _context.TonKhos
                    join t in _context.Thuoc on tk.MaThuoc equals t.MaThuoc
                    where tk.TrangThaiSeal == false && tk.SoLuongCon > 0
                    orderby tk.HanSuDung
                    select new
                    {
                        tk.MaLo,
                        tk.MaThuoc,
                        TenThuoc = t.TenThuoc,
                        DonViGoc = tk.MaLoaiDonViTinh,
                        TenLoaiDonViGoc = _context.Set<BE_QLTiemThuoc.Model.Thuoc.LoaiDonVi>().Where(d => d.MaLoaiDonVi == tk.MaLoaiDonViTinh).Select(d => d.TenLoaiDonVi).FirstOrDefault(),
                        tk.SoLuongCon,
                        tk.HanSuDung,
                        tk.TrangThaiSeal,
                        tk.GhiChu
                    };

            return await q.ToListAsync<dynamic>();
        }

        public async Task<List<dynamic>> GetDaTachLeAsync()
        {
            var q = from tk in _context.TonKhos
                    join t in _context.Thuoc on tk.MaThuoc equals t.MaThuoc
                    where tk.TrangThaiSeal == true && tk.SoLuongCon > 0
                    orderby tk.HanSuDung
                    select new
                    {
                        tk.MaLo,
                        tk.MaThuoc,
                        TenThuoc = t.TenThuoc,
                        TrangThai = "Đã tách lẻ",
                        DonViLe = tk.MaLoaiDonViTinh,
                        TenLoaiDonViLe = _context.Set<BE_QLTiemThuoc.Model.Thuoc.LoaiDonVi>().Where(d => d.MaLoaiDonVi == tk.MaLoaiDonViTinh).Select(d => d.TenLoaiDonVi).FirstOrDefault(),
                        SoLuongConLe = tk.SoLuongCon,
                        tk.HanSuDung,
                        tk.TrangThaiSeal,
                        tk.GhiChu
                    };

            return await q.ToListAsync<dynamic>();
        }

        public async Task<List<dynamic>> GetTongSoLuongConAsync()
        {
            var q = from tk in _context.TonKhos
                    join t in _context.Thuoc on tk.MaThuoc equals t.MaThuoc
                    where tk.TrangThaiSeal == false && tk.SoLuongCon > 0
                    group tk by new { tk.MaThuoc, t.TenThuoc, tk.MaLoaiDonViTinh } into g
                    orderby g.Key.TenThuoc
                    select new
                    {
                        MaThuoc = g.Key.MaThuoc,
                        TenThuoc = g.Key.TenThuoc,
                        DonViTinh = g.Key.MaLoaiDonViTinh,
                        TenLoaiDonVi = _context.Set<BE_QLTiemThuoc.Model.Thuoc.LoaiDonVi>().Where(d => d.MaLoaiDonVi == g.Key.MaLoaiDonViTinh).Select(d => d.TenLoaiDonVi).FirstOrDefault(),
                        TongSoLuongCon = g.Sum(x => x.SoLuongCon)
                    };

            return await q.ToListAsync<dynamic>();
        }
    }
}