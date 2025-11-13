using BE_QLTiemThuoc.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BE_QLTiemThuoc.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ThuocViewController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ThuocViewController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/ThuocView/ChuaTachLe
        [HttpGet("ChuaTachLe")]
        public async Task<IActionResult> GetChuaTachLe()
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

            var list = await q.ToListAsync();
            return Ok(list);
        }

        // GET: api/ThuocView/DaTachLe
        [HttpGet("DaTachLe")]
        public async Task<IActionResult> GetDaTachLe()
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

            var list = await q.ToListAsync();
            return Ok(list);
        }

        // GET: api/ThuocView/TongSoLuongCon
        [HttpGet("TongSoLuongCon")]
        public async Task<IActionResult> GetTongSoLuongCon()
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

            var list = await q.ToListAsync();
            return Ok(list);
        }
    }
}
