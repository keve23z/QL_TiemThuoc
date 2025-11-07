using BE_QLTiemThuoc.Data;
using BE_QLTiemThuoc.Model;
using BE_QLTiemThuoc.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BE_QLTiemThuoc.Dto;

namespace BE_QLTiemThuoc.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HoaDonController : ControllerBase
    {
        private readonly AppDbContext _ctx;
        public HoaDonController(AppDbContext ctx)
        {
            _ctx = ctx;
        }

        // GET: api/HoaDon/Search?from=2025-11-01&to=2025-11-07&status=3
        [HttpGet("Search")]
        public async Task<IActionResult> Search([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? status)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                if (from == null || to == null) throw new ArgumentException("Both 'from' and 'to' query parameters are required and must be valid dates.");
                var fromDate = from.Value.Date;
                var toDate = to.Value.Date.AddDays(1).AddTicks(-1); // include entire 'to' day

                var q = _ctx.HoaDons.AsQueryable();
                q = q.Where(h => h.NgayLap >= fromDate && h.NgayLap <= toDate);
                if (status != null) q = q.Where(h => h.TrangThaiGiaoHang == status.Value);

                var list = await q
                    .OrderByDescending(h => h.NgayLap)
                    .Select(h => new
                    {
                        h.MaHD,
                        h.NgayLap,
                        h.MaKH,
                        TenKH = _ctx.KhachHangs.Where(k => k.MAKH == h.MaKH).Select(k => k.HoTen).FirstOrDefault(),
                        h.MaNV,
                        TenNV = _ctx.Set<NhanVien>().Where(n => n.MANV == h.MaNV).Select(nv => nv.HoTen).FirstOrDefault(),
                        h.TongTien,
                        h.GhiChu,
                        h.TrangThaiGiaoHang
                    })
                    .ToListAsync();

                return list;
            });

            return Ok(response);
        }

        // GET: api/HoaDon/{maHd}/ChiTiet
        [HttpGet("{maHd}/ChiTiet")]
        public async Task<IActionResult> GetChiTiet(string maHd)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                // Load invoice header with customer and employee names
                var invoice = await _ctx.HoaDons
                    .Where(h => h.MaHD == maHd)
                    .Select(h => new
                    {
                        h.MaHD,
                        h.NgayLap,
                        h.MaKH,
                        TenKH = _ctx.KhachHangs.Where(k => k.MAKH == h.MaKH).Select(k => k.HoTen).FirstOrDefault(),
                        h.MaNV,
                        TenNV = _ctx.Set<NhanVien>().Where(n => n.MANV == h.MaNV).Select(nv => nv.HoTen).FirstOrDefault(),
                        h.TongTien,
                        h.GhiChu,
                        h.TrangThaiGiaoHang
                    })
                    .FirstOrDefaultAsync();

                // Load details with product and dosage names
                var items = await _ctx.ChiTietHoaDons
                    .Where(ct => ct.MaHD == maHd)
                    .Select(ct => new
                    {
                        ct.MaHD,
                        ct.MaLo,
                        ct.SoLuong,
                        ct.DonGia,
                        ct.ThanhTien,
                        ct.MaLD,
                        MaThuoc = _ctx.TonKhos.Where(t => t.MaLo == ct.MaLo).Select(t => t.MaThuoc).FirstOrDefault(),
                        TenThuoc = _ctx.Thuoc.Where(t => t.MaThuoc == _ctx.TonKhos.Where(x => x.MaLo == ct.MaLo).Select(x => x.MaThuoc).FirstOrDefault()).Select(x => x.TenThuoc).FirstOrDefault(),
                        TenLieuDung = _ctx.Set<LieuDung>().Where(ld => ld.MaLD == ct.MaLD).Select(ld => ld.TenLieuDung).FirstOrDefault()
                    })
                    .ToListAsync();

                return new { Invoice = invoice, Items = items };
            });

            return Ok(response);
        }

        // POST: api/HoaDon/Create
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] HoaDonCreateDto dto)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                if (dto == null) throw new ArgumentNullException(nameof(dto));
                if (dto.Items == null || dto.Items.Count == 0) throw new ArgumentException("Items are required");

                // generator for MaHD
                string GenMaHd() => "HD" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(10, 99).ToString();

                await using var tx = await _ctx.Database.BeginTransactionAsync();
                try
                {
                    var maHd = GenMaHd();
                    var hd = new HoaDon
                    {
                        MaHD = maHd,
                        NgayLap = DateTime.Now,
                        MaKH = dto.MaKH,
                        MaNV = dto.MaNV,
                        GhiChu = dto.GhiChu,
                        // use client-provided total if present; otherwise default to 0 and we'll set later
                        TongTien = dto.TongTien ?? 0m,
                        // Fixed status as requested
                        TrangThaiGiaoHang = 3
                    };
                    await _ctx.Set<HoaDon>().AddAsync(hd);
                    // Ensure HoaDon is inserted before inserting ChiTiet rows to satisfy FK constraints
                    await _ctx.SaveChangesAsync();

                    decimal tong = 0m;

                    foreach (var item in dto.Items)
                    {
                        // resolve MaThuoc if not provided
                        string? maThuoc = item.MaThuoc;
                        if (string.IsNullOrEmpty(maThuoc) && !string.IsNullOrEmpty(item.TenThuoc))
                        {
                            maThuoc = await _ctx.Set<Model.Thuoc.Thuoc>().Where(t => t.TenThuoc == item.TenThuoc).Select(t => t.MaThuoc).FirstOrDefaultAsync();
                        }
                        if (string.IsNullOrEmpty(maThuoc)) throw new KeyNotFoundException("MaThuoc or matching TenThuoc is required for each item");

                        int trangThaiSeal = item.TrangThaiSeal ?? 1; // default to loose units

                        // candidate lots (depending on sale type)
                        // Lock all candidate lot rows up-front (UPDLOCK, ROWLOCK) and order by HanSuDung
                        int sealFlag = (trangThaiSeal == 1) ? 1 : 0;
                        var sql = "SELECT * FROM TON_KHO WITH (UPDLOCK, ROWLOCK) WHERE MaThuoc = {0} AND TrangThaiSeal = {1} AND SoLuongCon > 0 ORDER BY HanSuDung";
                        var candidateLots = await _ctx.TonKhos.FromSqlRaw(sql, maThuoc, sealFlag).AsTracking().ToListAsync();

                        var totalAvailable = candidateLots.Sum(l => l.SoLuongCon);
                        if (totalAvailable < item.SoLuong) throw new InvalidOperationException($"Not enough stock for {maThuoc}");

                        int remaining = item.SoLuong;
                        // consume lots in FIFO by nearest expiry: fully deplete current lot before moving to next
                        foreach (var tk in candidateLots)
                        {
                            if (remaining <= 0) break;
                            var take = Math.Min(remaining, tk.SoLuongCon);
                            if (take <= 0) continue;

                            tk.SoLuongCon -= take;

                            var cthd = new ChiTietHoaDon
                            {
                                MaHD = maHd,
                                MaLo = tk.MaLo,
                                SoLuong = take,
                                DonGia = item.DonGia,
                                ThanhTien = item.DonGia * take,
                                MaLD = item.MaLD
                            };
                            await _ctx.Set<ChiTietHoaDon>().AddAsync(cthd);

                            tong += cthd.ThanhTien;
                            remaining -= take;
                        }
                    }

                    // If client didn't provide TongTien, use computed sum; otherwise keep provided value
                    if (dto.TongTien == null)
                    {
                        hd.TongTien = tong;
                    }
                    // persist changes
                    await _ctx.SaveChangesAsync();
                    await tx.CommitAsync();

                    // load created invoice + details to return full info
                    var created = await _ctx.HoaDons
                        .Where(h => h.MaHD == hd.MaHD)
                        .Select(h => new
                        {
                            h.MaHD,
                            h.NgayLap,
                            h.MaKH,
                            h.MaNV,
                            h.TongTien,
                            h.GhiChu,
                            h.TrangThaiGiaoHang
                        })
                        .FirstOrDefaultAsync();

                    var details = await _ctx.ChiTietHoaDons
                        .Where(ct => ct.MaHD == hd.MaHD)
                        .Select(ct => new
                        {
                            ct.MaHD,
                            ct.MaLo,
                            ct.SoLuong,
                            ct.DonGia,
                            ct.ThanhTien,
                            ct.MaLD
                        })
                        .ToListAsync();

                    return new { Invoice = created, Items = details };
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });

            return Ok(response);
        }
    }
}
