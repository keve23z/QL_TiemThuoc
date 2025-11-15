using BE_QLTiemThuoc.Data;
using BE_QLTiemThuoc.Model;
using BE_QLTiemThuoc.Model.Thuoc;
using BE_QLTiemThuoc.Services;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BE_QLTiemThuoc.Dto;
using System.Net.Mail;
using System.Net;
using System.Globalization;

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

        // GET: api/HoaDonChiTiet/{maHd}
        [HttpGet("ChiTiet/{maHd}")]
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
                                          h.TrangThaiGiaoHang,
                                          TrangThaiGiaoHangName = (h.TrangThaiGiaoHang == -1) ? "Hủy" :
                                                                  (h.TrangThaiGiaoHang == 0) ? "Đã đặt" :
                                                                  (h.TrangThaiGiaoHang == 1) ? "Đã xác nhận" :
                                                                  (h.TrangThaiGiaoHang == 2) ? "Đã giao" :
                                                                  (h.TrangThaiGiaoHang == 3) ? "Đã nhận" : "Không xác định"
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
                                   join ldv in _ctx.Set<LoaiDonVi>() on (ct.MaLoaiDonVi ?? ton.MaLoaiDonViTinh) equals ldv.MaLoaiDonVi into ldvGroup
                                   from ldv in ldvGroup.DefaultIfEmpty()
                                   select new
                                   {
                                       ct.MaCTHD,
                                       ct.MaHD,
                                       ct.MaLo,
                                       ct.SoLuong,
                                       ct.DonGia,
                                       ct.ThanhTien,
                                       ct.MaLD,
                                       MaThuoc = ct.MaThuoc ?? (ton != null ? ton.MaThuoc : null),
                                       TenThuoc = thuoc != null ? thuoc.TenThuoc : null,
                                       TenLieuDung = lieu != null ? lieu.TenLieuDung : null,
                                       MaLoaiDonVi = ct.MaLoaiDonVi ?? (ton != null ? ton.MaLoaiDonViTinh : null),
                                       TenLoaiDonVi = ldv != null ? ldv.TenLoaiDonVi : null
                                   })
                    .ToListAsync();

                return (object)new { Invoice = invoice, Items = items };
            });

            return Ok(response);
        }

        // GET: api/HoaDon/ChiTiet/Summary/{maHd}
        // Returns grouped summary per MaThuoc and MaLD for a given invoice
        [HttpGet("ChiTiet/Summary/{maHd}")]
        public async Task<IActionResult> GetChiTietSummary(string maHd)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync<object>(async () =>
            {
                if (string.IsNullOrWhiteSpace(maHd)) throw new ArgumentException("maHd is required");

                var q = from ct in _ctx.ChiTietHoaDons
                        where ct.MaHD == maHd
                        join tk in _ctx.TonKhos on ct.MaLo equals tk.MaLo
                        join t in _ctx.Thuoc on tk.MaThuoc equals t.MaThuoc
                        join lieu in _ctx.Set<LieuDung>() on ct.MaLD equals lieu.MaLD into lieuGroup
                        from lieu in lieuGroup.DefaultIfEmpty()
                        join ldv in _ctx.Set<LoaiDonVi>() on (ct.MaLoaiDonVi ?? tk.MaLoaiDonViTinh) equals ldv.MaLoaiDonVi into ldvGroup
                        from ldv in ldvGroup.DefaultIfEmpty()
                        group new { ct, tk, t, lieu, ldv } by new { MaThuoc = (ct.MaThuoc ?? t.MaThuoc), MaLD = ct.MaLD, TenThuoc = t.TenThuoc, MaLoaiDonVi = ct.MaLoaiDonVi ?? tk.MaLoaiDonViTinh } into g
                        select new
                        {
                            MaThuoc = g.Key.MaThuoc,
                            TenThuoc = g.Key.TenThuoc,
                            MaLD = g.Key.MaLD,
                            TenLD = g.Max(x => x.lieu != null ? x.lieu.TenLieuDung : null),
                            MaLoaiDonVi = g.Key.MaLoaiDonVi,
                            TenLoaiDonVi = g.Max(x => x.ldv != null ? x.ldv.TenLoaiDonVi : null),
                            TongSoLuong = g.Sum(x => x.ct.SoLuong),
                            HanSuDungGanNhat = g.Max(x => (DateTime?)x.tk.HanSuDung),
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
                                          h.TrangThaiGiaoHang,
                                          TrangThaiGiaoHangName = (h.TrangThaiGiaoHang == -1) ? "Hủy" :
                                                                  (h.TrangThaiGiaoHang == 0) ? "Đã đặt" :
                                                                  (h.TrangThaiGiaoHang == 1) ? "Đã xác nhận" :
                                                                  (h.TrangThaiGiaoHang == 2) ? "Đã giao" :
                                                                  (h.TrangThaiGiaoHang == 3) ? "Đã nhận" : "Không xác định"
                                      })
                    .FirstOrDefaultAsync();

                return (object)new { Invoice = invoice, Summary = list };
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
        // POST: api/HoaDon/Create
        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] HoaDonCreateDto dto)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync<object>(async () =>
            {
                if (dto == null) throw new ArgumentNullException(nameof(dto));
                if (dto.Items == null || dto.Items.Count == 0) throw new ArgumentException("Items are required");

                // Validate items: each item must include MaThuoc and SoLuong > 0
                if (!dto.Items.Any(i => !string.IsNullOrWhiteSpace(i.MaThuoc) && i.SoLuong > 0))
                    throw new ArgumentException("Items must include at least one entry with a valid 'MaThuoc' and 'SoLuong' > 0.");

                foreach (var itm in dto.Items)
                {
                    if (string.IsNullOrWhiteSpace(itm.MaThuoc)) throw new ArgumentException("Each item must provide 'MaThuoc'.");
                    if (itm.SoLuong <= 0) throw new ArgumentException($"Invalid 'SoLuong' for MaThuoc '{itm.MaThuoc}'. Quantity must be greater than 0.");
                }

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
                                    MaLD = item.MaLD,
                                    MaLoaiDonVi = donVi,
                                    MaThuoc = tk.MaThuoc
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
                            ct.MaLD,
                            ct.MaLoaiDonVi
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
                            MaLD = null, // online order: MaLD is null
                            MaLoaiDonVi = string.IsNullOrWhiteSpace(item.DonVi) ? null : item.DonVi.Trim(),
                            MaThuoc = string.IsNullOrWhiteSpace(item.MaThuoc) ? null : item.MaThuoc.Trim()
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
                            ct.MaLD,
                            ct.MaLoaiDonVi
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

        // POST: api/HoaDon/SendToCustomer
        // Body: { "maKhachHang": "KH001", "maHd": "HD20251114123456" }
        [HttpPost("SendToCustomer")]
        public async Task<IActionResult> SendToCustomer([FromBody] SendInvoiceRequest req)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync<object>(async () =>
            {
                if (req == null) throw new ArgumentNullException(nameof(req));
                if (string.IsNullOrWhiteSpace(req.MaKhachHang)) throw new ArgumentException("MaKhachHang is required.");
                if (string.IsNullOrWhiteSpace(req.MaHd)) throw new ArgumentException("MaHd is required.");

                // Load invoice header
                var invoice = await _ctx.HoaDons.FirstOrDefaultAsync(h => h.MaHD == req.MaHd && h.MaKH == req.MaKhachHang);
                if (invoice == null) throw new KeyNotFoundException($"Không tìm thấy hoá đơn '{req.MaHd}' cho khách hàng '{req.MaKhachHang}'.");

                // Find customer display info
                var kh = await _ctx.KhachHangs.FirstOrDefaultAsync(k => k.MAKH == req.MaKhachHang);

                // Prefer email from TaiKhoan if available
                var taiKhoan = await _ctx.TaiKhoans.FirstOrDefaultAsync(t => t.MaKH == req.MaKhachHang && !string.IsNullOrEmpty(t.EMAIL));
                string? toEmail = taiKhoan?.EMAIL;
                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    throw new InvalidOperationException("Không tìm thấy email của khách hàng. Vui lòng đảm bảo khách hàng có tài khoản và email đã được lưu.");
                }

                // Load grouped items (aggregate same medicines as in ChiTiet/Summary)
                var items = await (from ct in _ctx.ChiTietHoaDons
                                   where ct.MaHD == req.MaHd
                                   join tk in _ctx.TonKhos on ct.MaLo equals tk.MaLo into tkGroup
                                   from tk in tkGroup.DefaultIfEmpty()
                                   join t in _ctx.Thuoc on (ct.MaThuoc ?? (tk != null ? tk.MaThuoc : null)) equals t.MaThuoc into tGroup
                                   from t in tGroup.DefaultIfEmpty()
                                   join lieu in _ctx.Set<LieuDung>() on ct.MaLD equals lieu.MaLD into lieuGroup
                                   from lieu in lieuGroup.DefaultIfEmpty()
                                   join ldv in _ctx.Set<LoaiDonVi>() on (ct.MaLoaiDonVi ?? (tk != null ? tk.MaLoaiDonViTinh : null)) equals ldv.MaLoaiDonVi into ldvGroup
                                   from ldv in ldvGroup.DefaultIfEmpty()
                                   group new { ct, tk, t, lieu, ldv } by new { MaThuoc = (ct.MaThuoc ?? (tk != null ? tk.MaThuoc : null)), MaLD = ct.MaLD, TenThuoc = t.TenThuoc, MaLoaiDonVi = ct.MaLoaiDonVi ?? (tk != null ? tk.MaLoaiDonViTinh : null) } into g
                                   select new
                                   {
                                       MaThuoc = g.Key.MaThuoc,
                                       TenThuoc = g.Key.TenThuoc,
                                       MaLD = g.Key.MaLD,
                                       TenLD = g.Max(x => x.lieu != null ? x.lieu.TenLieuDung : null),
                                       MaLoaiDonVi = g.Key.MaLoaiDonVi,
                                       TenLoaiDonVi = g.Max(x => x.ldv != null ? x.ldv.TenLoaiDonVi : null),
                                       TongSoLuong = g.Sum(x => x.ct.SoLuong),
                                       HanSuDungGanNhat = g.Max(x => (DateTime?)x.tk.HanSuDung),
                                       DonGiaTrungBinh = g.Average(x => x.ct.DonGia),
                                       TongThanhTien = g.Sum(x => x.ct.ThanhTien)
                                   })
                    .ToListAsync();

                // Build professional pharmacy invoice (matching real-world pharmacy design)
                var customerName = kh != null && !string.IsNullOrWhiteSpace(kh.HoTen) ? kh.HoTen : req.MaKhachHang;
                var nv = await _ctx.Set<NhanVien>().FirstOrDefaultAsync(n => n.MANV == invoice.MaNV);
                var employeeName = nv != null ? nv.HoTen : (string.IsNullOrWhiteSpace(invoice.MaNV) ? "(không xác định)" : invoice.MaNV);

                var culture = new CultureInfo("vi-VN");
                var html = new System.Text.StringBuilder();
                
                // Mobile-optimized HTML template with responsive design
                html.Append(@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        * { box-sizing: border-box; overflow: visible; text-overflow: unset; white-space: normal; word-wrap: break-word; }
        body { font-family: Arial, sans-serif; margin: 0; padding: 0; background-color: #f5f5f5; font-size: 14px; line-height: 1.4; }
        .invoice-container { max-width: 100%; width: 100%; margin: 0; background: white; }
        .header { background: linear-gradient(135deg, #2E8B57 0%, #20B2AA 100%); color: white; padding: 20px 15px; text-align: center; }
        .header h1 { margin: 0; font-size: 20px; font-weight: bold; line-height: 1.2; }
        .header h2 { margin: 8px 0 0 0; font-size: 14px; font-weight: normal; opacity: 0.9; }
        .content { padding: 15px; }
        
        /* Mobile-first layout */
        .invoice-info { margin-bottom: 20px; }
        .info-section { margin-bottom: 15px; padding: 0; background: transparent; border-radius: 0; border-left: none; }
        .info-section h3 { color: #2E8B57; font-size: 14px; margin: 0 0 10px 0; font-weight: bold; border-bottom: 2px solid #2E8B57; padding-bottom: 5px; }
        .info-row { margin: 6px 0; display: flex; }
        .info-label { font-weight: bold; color: #555; min-width: 90px; font-size: 13px; }
        .info-value { flex: 1; font-size: 13px; word-break: break-word; overflow: visible; text-overflow: unset; white-space: normal; }
        
        /* Compact mobile table */
        .table-container { margin: 20px 0; }
        .items-table { width: 100%; border-collapse: collapse; font-size: 12px; }
        .items-table th { background: #2E8B57; color: white; padding: 8px 4px; text-align: center; font-weight: bold; font-size: 11px; }
        .items-table td { padding: 8px 4px; border-bottom: 1px solid #eee; text-align: center; font-size: 11px; vertical-align: top; overflow: visible; text-overflow: unset; white-space: normal; word-wrap: break-word; }
        .items-table tbody tr:nth-child(even) { background-color: #f9f9f9; }
        
        /* Mobile table columns */
        .col-stt { width: 7%; }
        .col-name { width: 26%; text-align: left !important; font-weight: 500; }
        .col-unit { width: 11%; }
        .col-qty { width: 7%; font-weight: bold; }
        .col-price { width: 12%; text-align: right !important; }
        .col-total { width: 12%; text-align: right !important; font-weight: bold; color: #2E8B57; }
        .col-exp { width: 11%; font-size: 10px; }
        .col-dose { width: 14%; }
        
        .total-section { background: #f8f9fa; padding: 15px; margin: 20px 0; border-radius: 6px; border-left: 4px solid #2E8B57; }
        .total-row { display: flex; justify-content: space-between; margin: 8px 0; align-items: center; }
        .total-label { font-weight: bold; color: #555; font-size: 16px; }
        .total-amount { font-weight: bold; color: #2E8B57; font-size: 18px; }
        .total-words { margin: 12px 0; font-style: italic; color: #666; background: white; padding: 10px; border-radius: 4px; font-size: 13px; overflow: visible; text-overflow: unset; white-space: normal; word-wrap: break-word; }
        .footer { background: #2E8B57; color: white; padding: 15px; text-align: center; }
        .footer-text { margin: 6px 0; font-size: 12px; word-wrap: break-word; white-space: normal; line-height: 1.5; overflow: visible; text-overflow: unset; }
        .thank-you { font-size: 14px; font-weight: bold; margin-bottom: 12px; }
        
        /* Desktop overrides */
        @media (min-width: 768px) {
            body { font-size: 16px; }
            .header { padding: 30px 20px; }
            .header h1 { font-size: 28px; }
            .header h2 { font-size: 18px; }
            .content { padding: 25px 20px; }
            .invoice-info { display: flex; width: 100%; margin-bottom: 30px; }
            .info-section { flex: 1; width: 50%; padding: 0 20px; background: transparent; }
            .info-section:first-child { padding-left: 0; }
            .info-section:last-child { padding-right: 0; }
            .info-section h3 { color: #2E8B57; font-size: 16px; margin: 0 0 15px 0; border-bottom: 2px solid #2E8B57; padding-bottom: 5px; font-weight: bold; }
            .info-row { margin: 10px 0; display: flex; align-items: flex-start; }
            .info-label { font-weight: bold; color: #555; min-width: 120px; font-size: 14px; }
            .info-value { flex: 1; font-size: 14px; overflow: visible; text-overflow: unset; white-space: normal; word-wrap: break-word; }
            .items-table { font-size: 14px; }
            .items-table th, .items-table td { padding: 12px 8px; font-size: 13px; overflow: visible; text-overflow: unset; white-space: normal; word-wrap: break-word; }
            .footer { padding: 20px; }
            .footer-text { font-size: 14px; word-wrap: break-word; white-space: normal; line-height: 1.5; overflow: visible; text-overflow: unset; }
        }
    </style>
</head>
<body>
    <div class='invoice-container'>
        <div class='header'>
            <h1>🏥 NHÀ THUỐC MELON</h1>
            <h2>HÓA ĐƠN BÁN THUỐC</h2>
        </div>
        
        <div class='content'>
            <div class='invoice-info'>
                <div class='info-section'>
                    <h3>📋 Thông Tin Hóa Đơn</h3>");
                
                html.Append($@"
                    <div class='info-row'>
                        <span class='info-label'>Số HĐ:</span>
                        <span class='info-value' style='font-weight: bold; color: #2E8B57;'>{System.Net.WebUtility.HtmlEncode(invoice.MaHD)}</span>
                    </div>
                    <div class='info-row'>
                        <span class='info-label'>Ngày lập:</span>
                        <span class='info-value'>{invoice.NgayLap:dd/MM/yyyy HH:mm}</span>
                    </div>
                    <div class='info-row'>
                        <span class='info-label'>Nhân viên:</span>
                        <span class='info-value'>{System.Net.WebUtility.HtmlEncode(employeeName)}</span>
                    </div>");
                    
                html.Append($@"
                </div>
                
                <div class='info-section'>
                    <h3>👤 Thông Tin Khách Hàng</h3>
                    <div class='info-row'>
                        <span class='info-label'>Tên KH:</span>
                        <span class='info-value' style='font-weight: bold;'>{System.Net.WebUtility.HtmlEncode(customerName)}</span>
                    </div>");
                    
                if (kh != null)
                {
                    if (!string.IsNullOrWhiteSpace(kh.DienThoai))
                    {
                        html.Append($@"
                    <div class='info-row'>
                        <span class='info-label'>SĐT:</span>
                        <span class='info-value'>{System.Net.WebUtility.HtmlEncode(kh.DienThoai)}</span>
                    </div>");
                    }
                    if (!string.IsNullOrWhiteSpace(kh.DiaChi))
                    {
                        html.Append($@"
                    <div class='info-row'>
                        <span class='info-label'>Địa chỉ:</span>
                        <span class='info-value'>{System.Net.WebUtility.HtmlEncode(kh.DiaChi)}</span>
                    </div>");
                    }
                }
                
                html.Append(@"
                </div>
            </div>
            
            <div class='table-container'>
                <table class='items-table'>
                    <thead>
                        <tr>
                            <th class='col-stt'>STT</th>
                            <th class='col-name'>Tên Thuốc</th>
                            <th class='col-unit'>Đơn Vị</th>
                            <th class='col-qty'>SL</th>
                            <th class='col-price'>Đơn Giá</th>
                            <th class='col-total'>Thành Tiền</th>
                            <th class='col-exp'>HSD</th>
                            <th class='col-dose'>Liều Dùng</th>
                        </tr>
                    </thead>
                    <tbody>");
                    
                int idx = 1;
                foreach (var it in items)
                {
                    string hanSuDung = it.HanSuDungGanNhat.HasValue ? it.HanSuDungGanNhat.Value.ToString("dd/MM/yyyy") : "---";
                    html.Append($@"
                        <tr>
                            <td class='col-stt'>{idx++}</td>
                            <td class='col-name'>{System.Net.WebUtility.HtmlEncode(it.TenThuoc ?? "")}</td>
                            <td class='col-unit'>{System.Net.WebUtility.HtmlEncode(it.TenLoaiDonVi ?? it.MaLoaiDonVi ?? "")}</td>
                            <td class='col-qty'>{it.TongSoLuong}</td>
                            <td class='col-price'>{it.DonGiaTrungBinh.ToString("N0", culture)}đ</td>
                            <td class='col-total'>{it.TongThanhTien.ToString("N0", culture)}đ</td>
                            <td class='col-exp'>{System.Net.WebUtility.HtmlEncode(hanSuDung)}</td>
                            <td class='col-dose'>{System.Net.WebUtility.HtmlEncode(it.TenLD ?? "---")}</td>
                        </tr>");
                }
                
                html.Append(@"
                    </tbody>
                </table>
            </div>
            
            <div class='total-section'>
                <div class='total-row'>
                    <span class='total-label'>TỔNG TIỀN:</span>");
                    
                var totalLong = Convert.ToInt64(Math.Round(invoice.TongTien));
                var totalWords = NumberToVietnameseWords(totalLong);
                
                html.Append($@"
                    <span class='total-amount'>{invoice.TongTien.ToString("N0", culture)}đ</span>
                </div>
                <div class='total-words'>
                    <strong>Bằng chữ:</strong> {System.Net.WebUtility.HtmlEncode(totalWords)} đồng
                </div>
            </div>
        </div>
        
        <div class='footer'>
            <div class='thank-you'>Cảm ơn quý khách đã sử dụng dịch vụ!</div>
            <div class='footer-text'>📧 Email: support@nhathươcmelon.com | 📞 Hotline: 1900 xxxx</div>
            <div class='footer-text'>🏠 Địa chỉ: 123 Đường ABC, Quận XYZ, TP. Hồ Chí Minh</div>
        </div>
    </div>
</body>
</html>");

                // Send email via SMTP (reuse settings from TaiKhoanController)
                var smtp = new SmtpClient("smtp.gmail.com")
                {
                    Credentials = new NetworkCredential("chaytue0203@gmail.com", "kctw ltds teaj luvb"),
                    EnableSsl = true,
                    Port = 587
                };

                var mail = new MailMessage("khangtuong040@gmail.com", toEmail)
                {
                    Subject = $"Xác nhận hoá đơn {invoice.MaHD} - Tại nhà thuốc Melon",
                    Body = html.ToString(),
                    IsBodyHtml = true
                };

                await smtp.SendMailAsync(mail);

                return new { SentTo = toEmail, MaHD = invoice.MaHD };
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
                    if (hd == null) throw new KeyNotFoundException($"Không tìm thấy hóa đơn {maHd}.");

                    var details = await _ctx.ChiTietHoaDons
                        .Where(ct => ct.MaHD == maHd)
                        .Select(ct => new
                        {
                            ct.MaCTHD,
                            ct.MaHD,
                            ct.MaLo,
                            ct.SoLuong,
                            ct.DonGia,
                            ct.ThanhTien,
                            ct.MaLD,
                            ct.MaLoaiDonVi,
                            ct.MaThuoc
                        })
                        .ToListAsync();

                    var missingLots = new List<string>();

                    foreach (var it in details)
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

       
        
        private static string NumberToVietnameseWords(long number)
        {
            if (number == 0) return "không";

            string[] units = { "", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };
            string[] scales = { "", " nghìn", " triệu", " tỷ" };

            List<string> parts = new();

            int scaleIdx = 0;
            while (number > 0)
            {
                int group = (int)(number % 1000);
                if (group != 0)
                {
                    string groupText = ReadThreeDigits(group, units);
                    parts.Insert(0, groupText + scales[scaleIdx]);
                }
                number /= 1000;
                scaleIdx++;
                if (scaleIdx >= scales.Length && number > 0 && scaleIdx < 6)
                {
                    // extend scales if needed (e.g., nghìn tỷ)
                    var extra = (scaleIdx % 3 == 1) ? " nghìn" : (scaleIdx % 3 == 2) ? " triệu" : " tỷ";
                    // avoid modifying original array
                    // but for typical invoices this won't be needed
                }
            }

            return string.Join(" ", parts).Trim();

            static string ReadThreeDigits(int n, string[] unitsLocal)
            {
                int hundreds = n / 100;
                int tens = (n / 10) % 10;
                int ones = n % 10;
                var sb = new System.Text.StringBuilder();

                if (hundreds > 0)
                {
                    sb.Append(unitsLocal[hundreds]).Append(" trăm");
                    if (tens == 0 && ones > 0) sb.Append(" lẻ");
                }
                else
                {
                    if (tens > 0 || ones > 0) sb.Append("");
                }

                if (tens > 1)
                {
                    sb.Append(" ").Append(unitsLocal[tens]).Append(" mươi");
                    if (ones == 1) sb.Append(" mốt");
                    else if (ones == 4) sb.Append(" bốn");
                    else if (ones == 5) sb.Append(" lăm");
                    else if (ones > 0) sb.Append(" ").Append(unitsLocal[ones]);
                }
                else if (tens == 1)
                {
                    sb.Append(" mười");
                    if (ones == 5) sb.Append(" lăm");
                    else if (ones > 0) sb.Append(" ").Append(unitsLocal[ones]);
                }
                else // tens == 0
                {
                    if (ones > 0)
                    {
                        if (sb.Length > 0) sb.Append(" ");
                        sb.Append(unitsLocal[ones]);
                    }
                }

                return sb.ToString().Trim();
            }
        }
    }
}
