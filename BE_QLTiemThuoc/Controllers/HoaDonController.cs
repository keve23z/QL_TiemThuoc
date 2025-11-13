using BE_QLTiemThuoc.Data;
using BE_QLTiemThuoc.Model;
using BE_QLTiemThuoc.Services;
using System.Linq;
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
        public async Task<IActionResult> Search([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? status, [FromQuery] string? loai)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync<object>(async () =>
            {
                if (from == null || to == null) throw new ArgumentException("Both 'from' and 'to' query parameters are required and must be valid dates.");
                var fromDate = from.Value.Date;
                var toDate = to.Value.Date.AddDays(1).AddTicks(-1); // include entire 'to' day

                var q = _ctx.HoaDons.AsQueryable();
                q = q.Where(h => h.NgayLap >= fromDate && h.NgayLap <= toDate);
                if (status != null) q = q.Where(h => h.TrangThaiGiaoHang == status.Value);

                // filter by invoice type: 'HD' (direct) and 'HDOL' (online)
                if (!string.IsNullOrEmpty(loai))
                {
                    if (loai.Equals("HDOL", System.StringComparison.OrdinalIgnoreCase))
                    {
                        q = q.Where(h => EF.Functions.Like(h.MaHD, "HDOL%"));
                    }
                    else if (loai.Equals("HD", System.StringComparison.OrdinalIgnoreCase))
                    {
                        // starts with HD but exclude HDOL which is online
                        q = q.Where(h => EF.Functions.Like(h.MaHD, "HD%") && !EF.Functions.Like(h.MaHD, "HDOL%"));
                    }
                }

                var list = await (from h in q
                                   join kh in _ctx.KhachHangs on h.MaKH equals kh.MAKH into khGroup
                                   from kh in khGroup.DefaultIfEmpty()
                                   join nv in _ctx.Set<NhanVien>() on h.MaNV equals nv.MANV into nvGroup
                                   from nv in nvGroup.DefaultIfEmpty()
                                   orderby h.NgayLap descending
                                   select new
                                   {
                                       h.MaHD,
                                       h.NgayLap,
                                       h.MaKH,
                                       TenKH = string.IsNullOrEmpty(kh.HoTen) ? h.MaKH : kh.HoTen,
                                       DiaChiKH = kh.DiaChi,
                                       DienThoaiKH = kh.DienThoai,
                                       h.MaNV,
                                       TenNV = nv.HoTen,
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
            var response = await ApiResponseHelper.ExecuteSafetyAsync<object>(async () =>
            {
                // Load invoice header with customer and employee details
                var invoice = await (from h in _ctx.HoaDons
                                      where h.MaHD == maHd
                                      join kh in _ctx.KhachHangs on h.MaKH equals kh.MAKH into khGroup
                                      from kh in khGroup.DefaultIfEmpty()
                                      join nv in _ctx.Set<NhanVien>() on h.MaNV equals nv.MANV into nvGroup
                                      from nv in nvGroup.DefaultIfEmpty()
                                      select new
                                      {
                                          h.MaHD,
                                          h.NgayLap,
                                          h.MaKH,
                                          TenKH = string.IsNullOrEmpty(kh.HoTen) ? h.MaKH : kh.HoTen,
                                          DiaChiKH = kh.DiaChi,
                                          DienThoaiKH = kh.DienThoai,
                                          h.MaNV,
                                          TenNV = nv.HoTen,
                                          h.TongTien,
                                          h.GhiChu,
                                          h.TrangThaiGiaoHang
                                      })
                    .FirstOrDefaultAsync();

                // Load details with product and dosage names
                var items = await (from ct in _ctx.ChiTietHoaDons
                                   where ct.MaHD == maHd
                                   join ton in _ctx.TonKhos on ct.MaLo equals ton.MaLo into tonGroup
                                   from ton in tonGroup.DefaultIfEmpty()
                                   join thuoc in _ctx.Thuoc on ton.MaThuoc equals thuoc.MaThuoc into thuocGroup
                                   from thuoc in thuocGroup.DefaultIfEmpty()
                                   join lieu in _ctx.Set<LieuDung>() on ct.MaLD equals lieu.MaLD into lieuGroup
                                   from lieu in lieuGroup.DefaultIfEmpty()
                                   select new
                                   {
                                       ct.MaCTHD,
                                       ct.MaHD,
                                       ct.MaLo,
                                       ct.SoLuong,
                                       ct.DonGia,
                                       ct.ThanhTien,
                                       ct.MaLD,
                                       MaThuoc = ton != null ? ton.MaThuoc : null,
                                       TenThuoc = thuoc != null ? thuoc.TenThuoc : null,
                                       TenLieuDung = lieu != null ? lieu.TenLieuDung : null
                                   })
                    .ToListAsync();

                return (object)new { Invoice = invoice, Items = items };
            });

            return Ok(response);
        }

        // GET: api/HoaDon/{maHd}/ChiTiet/Summary
        // Returns grouped summary per MaThuoc and MaLD for a given invoice
        [HttpGet("{maHd}/ChiTiet/Summary")]
        public async Task<IActionResult> GetChiTietSummary(string maHd)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync<object>(async () =>
            {
                if (string.IsNullOrWhiteSpace(maHd)) throw new ArgumentException("maHd is required");

                var q = from ct in _ctx.ChiTietHoaDons
                        where ct.MaHD == maHd
                        join tk in _ctx.TonKhos on ct.MaLo equals tk.MaLo
                        join t in _ctx.Thuoc on tk.MaThuoc equals t.MaThuoc
                        group new { ct, tk, t } by new { t.MaThuoc, ct.MaLD, t.TenThuoc } into g
                        select new
                        {
                            MaThuoc = g.Key.MaThuoc,
                            TenThuoc = g.Key.TenThuoc,
                            MaLD = g.Key.MaLD,
                            TongSoLuong = g.Sum(x => x.ct.SoLuong),
                            HanSuDungGanNhat = g.Max(x => x.tk.HanSuDung),
                            DonGiaTrungBinh = g.Average(x => x.ct.DonGia),
                            TongThanhTien = g.Sum(x => x.ct.ThanhTien)
                        };

                var list = await q.ToListAsync();

                // also load invoice header info to return together with the grouped summary
                var invoice = await (from h in _ctx.HoaDons
                                      where h.MaHD == maHd
                                      join kh in _ctx.KhachHangs on h.MaKH equals kh.MAKH into khGroup
                                      from kh in khGroup.DefaultIfEmpty()
                                      join nv in _ctx.Set<NhanVien>() on h.MaNV equals nv.MANV into nvGroup
                                      from nv in nvGroup.DefaultIfEmpty()
                                      select new
                                      {
                                          h.MaHD,
                                          h.NgayLap,
                                          h.MaKH,
                                          TenKH = string.IsNullOrEmpty(kh.HoTen) ? h.MaKH : kh.HoTen,
                                          DiaChiKH = kh.DiaChi,
                                          DienThoaiKH = kh.DienThoai,
                                          h.MaNV,
                                          TenNV = nv.HoTen,
                                          h.TongTien,
                                          h.GhiChu,
                                          h.TrangThaiGiaoHang
                                      })
                    .FirstOrDefaultAsync();

                return (object)new { Invoice = invoice, Summary = list };
            });

            return Ok(response);
        }

        // POST: api/HoaDon/Create
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] HoaDonCreateDto dto)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync<object>(async () =>
            {
                if (dto == null) throw new ArgumentNullException(nameof(dto));
                if (dto.Items == null || dto.Items.Count == 0) throw new ArgumentException("Items are required");

                string GenMaHd() => "HD" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(10, 99).ToString();

                await using var tx = await _ctx.Database.BeginTransactionAsync();
                try
                {
                    var maHd = GenMaHd();
                    // Require MaKH to be provided and exist
                    if (string.IsNullOrWhiteSpace(dto.MaKH))
                        throw new Exception("Khách hàng không tồn tại");

                    var khExists = await _ctx.KhachHangs.AnyAsync(k => k.MAKH == dto.MaKH);
                    if (!khExists) throw new Exception("Khách hàng không tồn tại");

                    var hd = new HoaDon
                    {
                        MaHD = maHd,
                        NgayLap = DateTime.Now,
                        MaKH = dto.MaKH,
                        MaNV = dto.MaNV,
                        GhiChu = dto.GhiChu,
                        TongTien = dto.TongTien ?? 0m,
                        TrangThaiGiaoHang = 3
                    };
                    await _ctx.Set<HoaDon>().AddAsync(hd);
                    await _ctx.SaveChangesAsync();

                    decimal tong = 0m;

                    // generator for MaCTHD
                    string GenMaCtHd() => "CTHD" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(10, 99).ToString();

                    foreach (var item in dto.Items)
                    {
                        string? maThuoc = item.MaThuoc;
                        // Require MaThuoc to be provided (do not use TenThuoc lookup)
                        if (string.IsNullOrEmpty(maThuoc))
                            throw new KeyNotFoundException("MaThuoc is required for each item");

                        
                        var donVi = string.IsNullOrWhiteSpace(item.DonVi) ? null : item.DonVi.Trim();

                        List<Model.Kho.TonKho> candidateLots;
                        if (!string.IsNullOrEmpty(donVi))
                        {
                            var sql = "SELECT * FROM TON_KHO WITH (UPDLOCK, ROWLOCK) WHERE MaThuoc = {0} AND MaLoaiDonViTinh = {1} AND SoLuongCon > 0 ORDER BY HanSuDung";
                            candidateLots = await _ctx.TonKhos.FromSqlRaw(sql, maThuoc, donVi).AsTracking().ToListAsync();
                        }
                        else
                        {
                            var sql = "SELECT * FROM TON_KHO WITH (UPDLOCK, ROWLOCK) WHERE MaThuoc = {0} AND SoLuongCon > 0 ORDER BY HanSuDung";
                            candidateLots = await _ctx.TonKhos.FromSqlRaw(sql, maThuoc).AsTracking().ToListAsync();
                        }

                        var totalAvailable = candidateLots.Sum(l => l.SoLuongCon);
                        if (totalAvailable < item.SoLuong)
                        {
                            throw new InvalidOperationException($"Hàng thuốc {maThuoc} trong kho không đủ. Yêu cầu {item.SoLuong}, có {totalAvailable}.");
                        }

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
                                MaCTHD = GenMaCtHd(),
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
                            ct.MaCTHD,
                            ct.MaHD,
                            ct.MaLo,
                            ct.SoLuong,
                            ct.DonGia,
                            ct.ThanhTien,
                            ct.MaLD
                        })
                        .ToListAsync();

                    return (object)new { Invoice = created, Items = details };
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });

            return Ok(response);
        }

        // POST: api/HoaDon/UpdateStatus/{maHd}/{status}
        // status: -1 = huỷ, 0 = đã đặt, 1 = đã xác nhận, 2 = đã giao, 3 = đã nhận
        [HttpPost("UpdateStatus/{maHd}/{status}")]
        public async Task<IActionResult> UpdateStatus(string maHd, int status)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync<object>(async () =>
            {
                if (string.IsNullOrWhiteSpace(maHd)) throw new ArgumentException("maHd is required");

                var allowed = new[] { 0, 1, 2, 3 };
                if (!allowed.Contains(status)) throw new ArgumentException("Invalid status value");
                var hd = await _ctx.HoaDons.FirstOrDefaultAsync(h => h.MaHD == maHd);
                if (hd == null) throw new KeyNotFoundException("Không tìm thấy hoá đơn.");

                hd.TrangThaiGiaoHang = status;
                await _ctx.SaveChangesAsync();

                return (object)new { maHd = hd.MaHD, trangThaiGiaoHang = hd.TrangThaiGiaoHang };
            });

            return Ok(response);
        }

        // GET: api/HoaDon/HistoryByKhachHang/{maKh}
        // Returns historical invoices for a customer where TrangThaiGiaoHang is -1 (cancelled) or 3 (received)
        [HttpGet("HistoryByKhachHang/{maKh}")]
        public async Task<IActionResult> HistoryByKhachHang(string maKh)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync<object>(async () =>
            {
                if (string.IsNullOrWhiteSpace(maKh)) throw new ArgumentException("maKh is required");

                // Historical: cancelled (-1) or received (3)
                var history = await (from h in _ctx.HoaDons
                                     where h.MaKH == maKh && (h.TrangThaiGiaoHang == -1 || h.TrangThaiGiaoHang == 3)
                                     join nv in _ctx.Set<NhanVien>() on h.MaNV equals nv.MANV into nvGroup
                                     from nv in nvGroup.DefaultIfEmpty()
                                     orderby h.NgayLap descending
                                     select new
                                     {
                                         h.MaHD,
                                         h.NgayLap,
                                         h.TongTien,
                                         h.GhiChu,
                                         h.TrangThaiGiaoHang,
                                         h.MaNV,
                                         TenNV = nv.HoTen
                                     })
                    .ToListAsync();

                // Current: statuses 0 (placed), 1 (confirmed), 2 (delivered)
                var current = await (from h in _ctx.HoaDons
                                     where h.MaKH == maKh && (h.TrangThaiGiaoHang == 0 || h.TrangThaiGiaoHang == 1 || h.TrangThaiGiaoHang == 2)
                                     join nv in _ctx.Set<NhanVien>() on h.MaNV equals nv.MANV into nvGroup2
                                     from nv in nvGroup2.DefaultIfEmpty()
                                     orderby h.NgayLap descending
                                     select new
                                     {
                                         h.MaHD,
                                         h.NgayLap,
                                         h.TongTien,
                                         h.GhiChu,
                                         h.TrangThaiGiaoHang,
                                         h.MaNV,
                                         TenNV = nv.HoTen
                                     })
                    .ToListAsync();

                return (object)new { History = history, Current = current };
            });

            return Ok(response);
        }

        // POST: api/HoaDon/Cancel/{maHd}
        // Cancel an invoice: set TrangThaiGiaoHang = -1 and restore stock by MaLo
        [HttpPost("Cancel/{maHd}")]
        public async Task<IActionResult> Cancel(string maHd)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync<object>(async () =>
            {
                if (string.IsNullOrWhiteSpace(maHd)) throw new ArgumentException("maHd is required");

                await using var tx = await _ctx.Database.BeginTransactionAsync();
                try
                {
                    var hd = await _ctx.HoaDons.FirstOrDefaultAsync(h => h.MaHD == maHd);
                    if (hd == null) throw new KeyNotFoundException("Không tìm thấy hoá đơn.");

                    if (hd.TrangThaiGiaoHang == -1)
                        return (object)new { message = "Hoá đơn đã bị huỷ trước đó", maHd = hd.MaHD, trangThaiGiaoHang = hd.TrangThaiGiaoHang };

                    // Load line items
                    var items = await _ctx.ChiTietHoaDons.Where(ct => ct.MaHD == maHd).ToListAsync();

                    var missingLots = new List<string>();

                    foreach (var it in items)
                    {
                        // only restore if MaLo present
                        if (string.IsNullOrWhiteSpace(it.MaLo))
                        {
                            missingLots.Add(it.MaCTHD ?? "(unknown)");
                            continue;
                        }

                        var lot = await _ctx.TonKhos.FirstOrDefaultAsync(t => t.MaLo == it.MaLo);
                        if (lot == null)
                        {
                            // Data integrity: referenced lot missing
                            throw new KeyNotFoundException($"Không tìm thấy lô {it.MaLo} để trả lại tồn kho.");
                        }

                        lot.SoLuongCon += it.SoLuong;
                    }

                    // mark invoice cancelled
                    hd.TrangThaiGiaoHang = -1;
                    await _ctx.SaveChangesAsync();
                    await tx.CommitAsync();

                    return (object)new { maHd = hd.MaHD, trangThaiGiaoHang = hd.TrangThaiGiaoHang, warnings = missingLots.Any() ? missingLots : null };
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });

            return Ok(response);
        }

        // POST: api/HoaDon/CreateOnline
        // Similar to Create but for online orders:
        // - MaHD starts with 'HDOL'
        // - MaNV is null
        // - TrangThaiGiaoHang = 0
        // - When saving chi tiết, MaLD is always null
        [HttpPost("CreateOnline")]
        public async Task<IActionResult> CreateOnline([FromBody] HoaDonCreateDto dto)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync<object>(async () =>
            {
                if (dto == null) throw new ArgumentNullException(nameof(dto));
                if (dto.Items == null || dto.Items.Count == 0) throw new ArgumentException("Items are required");

                // generator for MaHD (online prefix)
                string GenMaHd() => "HDOL" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(10, 99).ToString();

                await using var tx = await _ctx.Database.BeginTransactionAsync();
                try
                {
                    var maHd = GenMaHd();

                    // Require MaKH to be provided and exist
                    if (string.IsNullOrWhiteSpace(dto.MaKH))
                        throw new Exception("Khách hàng không tồn tại");

                    var khExists = await _ctx.KhachHangs.AnyAsync(k => k.MAKH == dto.MaKH);
                    if (!khExists) throw new Exception("Khách hàng không tồn tại");

                    var hd = new HoaDon
                    {
                        MaHD = maHd,
                        NgayLap = DateTime.Now,
                        MaKH = dto.MaKH,
                        MaNV = null, // online: no employee
                        GhiChu = dto.GhiChu,
                        // use client-provided total if present; otherwise default to 0 and we'll set later
                        TongTien = dto.TongTien ?? 0m,
                        // Online orders default status
                        TrangThaiGiaoHang = 0
                    };

                    await _ctx.Set<HoaDon>().AddAsync(hd);
                    await _ctx.SaveChangesAsync();

                    decimal tong = 0m;

                    // generator for MaCTHD
                    string GenMaCtHd() => "CTHD" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(10, 99).ToString();

                    // For online orders we DO NOT touch inventory. Save line items without assigning lots or decreasing stock.
                    foreach (var item in dto.Items)
                    {
                        // Require MaThuoc to be provided
                        string? maThuoc = item.MaThuoc;
                        if (string.IsNullOrEmpty(maThuoc))
                            throw new KeyNotFoundException("MaThuoc is required for each item");

                        var cthd = new ChiTietHoaDon
                        {
                            MaCTHD = GenMaCtHd(),
                            MaHD = maHd,
                            MaLo = null, // no lot assigned for online orders
                            SoLuong = item.SoLuong,
                            DonGia = item.DonGia,
                            ThanhTien = item.DonGia * item.SoLuong,
                            MaLD = null // online order: MaLD is null
                        };
                        await _ctx.Set<ChiTietHoaDon>().AddAsync(cthd);

                        tong += cthd.ThanhTien;
                    }

                    if (dto.TongTien == null)
                    {
                        hd.TongTien = tong;
                    }

                    // persist changes (updated TonKho + ChiTietHoaDon rows)
                    await _ctx.SaveChangesAsync();
                    await tx.CommitAsync();

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
                            ct.MaCTHD,
                            ct.MaHD,
                            ct.MaLo,
                            ct.SoLuong,
                            ct.DonGia,
                            ct.ThanhTien,
                            ct.MaLD
                        })
                        .ToListAsync();

                    return (object)new { Invoice = created, Items = details };
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
