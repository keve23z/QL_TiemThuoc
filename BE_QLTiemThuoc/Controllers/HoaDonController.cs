using BE_QLTiemThuoc.Data;
using BE_QLTiemThuoc.Model;
using BE_QLTiemThuoc.Model.Thuoc;
using BE_QLTiemThuoc.Services;
using System.Text;
using System.IO;
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

        // GET: api/HoaDon/ExportXlsx
        // Exports same data as Export but returns an HTML-based Excel file (.xls) to avoid encoding problems when opening in Excel
        [HttpGet("ExportXlsx")]
        public async Task<IActionResult> ExportXlsx([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int? status, [FromQuery] string? loai, [FromQuery] string? maNv)
        {
            try
            {
                if (from == null || to == null) return BadRequest(new { status = 0, message = "Cần cung cấp ngày 'from' và 'to'." });
                var fromDate = from.Value.Date;
                var toDate = to.Value.Date.AddDays(1).AddTicks(-1);
                var culture = new CultureInfo("vi-VN");

                var q = _ctx.HoaDons.AsQueryable();
                q = q.Where(h => h.NgayLap >= fromDate && h.NgayLap <= toDate);
                if (status != null) q = q.Where(h => h.TrangThaiGiaoHang == status.Value);
                if (!string.IsNullOrEmpty(loai))
                {
                    if (loai.Equals("HDOL", StringComparison.OrdinalIgnoreCase)) q = q.Where(h => EF.Functions.Like(h.MaHD, "HDOL%"));
                    else if (loai.Equals("HD", StringComparison.OrdinalIgnoreCase)) q = q.Where(h => EF.Functions.Like(h.MaHD, "HD%") && !EF.Functions.Like(h.MaHD, "HDOL%"));
                }
                // If maNv is provided, do NOT filter invoices by it. Instead capture the employee's name
                // to show in the report header (user requested: "không select mã nhân viên cho điều kiện danh sách hoá đơn").
                string? reportEmployeeName = null;
                if (!string.IsNullOrWhiteSpace(maNv))
                {
                    var rep = await _ctx.Set<NhanVien>().FirstOrDefaultAsync(n => n.MANV == maNv);
                    if (rep != null) reportEmployeeName = rep.HoTen;
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
                                       h.TrangThaiGiaoHang,
                                       TrangThaiGiaoHangName = (h.TrangThaiGiaoHang == -1) ? "Hủy" :
                                                               (h.TrangThaiGiaoHang == 0) ? "Đã đặt" :
                                                               (h.TrangThaiGiaoHang == 1) ? "Đã xác nhận" :
                                                               (h.TrangThaiGiaoHang == 2) ? "Đã giao" :
                                                               (h.TrangThaiGiaoHang == 3) ? "Đã nhận" : "Không xác định"
                                   }).ToListAsync();

                var sb = new StringBuilder();
                sb.AppendLine("<html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\"/></head><body>");
                // Title depends on invoice type filter 'loai'
                string reportTitle;
                if (!string.IsNullOrWhiteSpace(loai) && loai.Equals("HD", StringComparison.OrdinalIgnoreCase))
                {
                    reportTitle = $"BÁO CÁO HÓA ĐƠN MUA HÀNG TRỰC TIẾP TỪ NGÀY {fromDate:dd/MM/yyyy} ĐẾN {to.Value.Date:dd/MM/yyyy}";
                }
                else if (!string.IsNullOrWhiteSpace(loai) && loai.Equals("HDOL", StringComparison.OrdinalIgnoreCase))
                {
                    reportTitle = $"BÁO CÁO HÓA ĐƠN MUA HÀNG ONLINE TỪ NGÀY {fromDate:dd/MM/yyyy} ĐẾN {to.Value.Date:dd/MM/yyyy}";
                }
                else
                {
                    reportTitle = $"BÁO CÁO HÓA ĐƠN MUA HÀNG TỪ NGÀY {fromDate:dd/MM/yyyy} ĐẾN {to.Value.Date:dd/MM/yyyy}";
                }

                sb.AppendLine($"<h3>{System.Net.WebUtility.HtmlEncode(reportTitle)}</h3>");
                sb.AppendLine($"<div>Thời gian xuất: {DateTime.Now:dd/MM/yyyy HH:mm:ss}</div>");
                sb.AppendLine("<table border=\"1\" cellpadding=\"4\" cellspacing=\"0\">");
                sb.AppendLine("<tr><th>MaHD</th><th>NgayLap</th><th>MaKH</th><th>TenKH</th><th>DiaChiKH</th><th>DienThoaiKH</th><th>MaNV</th><th>TenNV</th><th>TongTien</th><th>GhiChu</th><th>TrangThaiGiaoHangName</th></tr>");

                if (!string.IsNullOrWhiteSpace(reportEmployeeName))
                {
                    sb.AppendLine($"<tr><td colspan=\"11\"><strong>Nhân viên xuất báo cáo:</strong> {System.Net.WebUtility.HtmlEncode(reportEmployeeName)}</td></tr>");
                }

                foreach (var it in list)
                {
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(it.MaHD)}</td>");
                    sb.AppendLine($"<td>{it.NgayLap:yyyy-MM-dd HH:mm}</td>");
                    sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(it.MaKH)}</td>");
                    sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(it.TenKH)}</td>");
                    sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(it.DiaChiKH)}</td>");
                    sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(it.DienThoaiKH)}</td>");
                    sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(it.MaNV)}</td>");
                    sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(it.TenNV)}</td>");
                    sb.AppendLine($"<td>{Convert.ToInt64(Math.Round(it.TongTien))}</td>");
                    sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(it.GhiChu)}</td>");
                    sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(it.TrangThaiGiaoHangName)}</td>");
                    sb.AppendLine("</tr>");
                }

                sb.AppendLine("</table>");

                // Summary: total count, total amount, amount in words
                var totalCount = list.Count;
                var totalAmount = list.Sum(x => x.TongTien);
                var totalAmountPlain = Convert.ToInt64(Math.Round(totalAmount)).ToString();
                var totalInWords = NumberToVietnameseWords(Convert.ToInt64(Math.Round(totalAmount))) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(totalInWords))
                {
                    totalInWords = "Không đồng.";
                }
                else
                {
                    totalInWords = char.ToUpper(totalInWords[0]) + (totalInWords.Length > 1 ? totalInWords.Substring(1) : string.Empty) + " đồng.";
                }

                sb.AppendLine("<br/>");
                sb.AppendLine("<table border=\"0\" cellpadding=\"2\" cellspacing=\"0\">\n<tr>");
                sb.AppendLine($"<td><strong>Số lượng hoá đơn xuất:</strong> {totalCount}</td>");
                sb.AppendLine($"<td style=\"padding-left:40px\"><strong>Tổng tiền:</strong> {System.Net.WebUtility.HtmlEncode(totalAmountPlain)}</td>");
                sb.AppendLine("</tr>");
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td colspan=\"2\"><strong>Bằng chữ:</strong> {System.Net.WebUtility.HtmlEncode(totalInWords)}</td>");
                sb.AppendLine("</tr>");
                sb.AppendLine("</table>");

                // Signature block (right aligned)
                sb.AppendLine("<br/><br/>");
                sb.AppendLine("<div style=\"width:100%;display:flex;justify-content:flex-end;\">\n<div style=\"text-align:center;\">\n<p>Ký tên</p>\n<br/><br/>\n<p>______________________</p>\n</div>\n</div>");

                sb.AppendLine("</body></html>");

                var fileName = $"Bao_cao_hoa_don_{fromDate:yyyyMMdd}_{to.Value.Date:yyyyMMdd}.xls";
                var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
                return File(bytes, "application/vnd.ms-excel; charset=utf-8", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = 0, message = "Đã xảy ra lỗi máy chủ: " + ex.Message });
            }
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
                                       HanSuDung = ct.HanSuDung,
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
                            HanSuDungGanNhat = g.Max(x => (DateTime?)x.ct.HanSuDung),
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
                        // Require client to provide an expected/threshold HanSuDung for direct invoice
                        if (item.HanSuDung == null)
                            throw new ArgumentException($"Hạn sử dụng (HanSuDung) phải được cung cấp cho MaThuoc '{maThuoc}'.");

                        var donVi = string.IsNullOrWhiteSpace(item.DonVi) ? null : item.DonVi.Trim();
                        List<Model.Kho.TonKho> candidateLots;
                        var requestedHsd = item.HanSuDung.Value;
                        if (!string.IsNullOrEmpty(donVi))
                        {
                            var sql = "SELECT * FROM TON_KHO WITH (UPDLOCK, ROWLOCK) WHERE MaThuoc = {0} AND MaLoaiDonViTinh = {1} AND SoLuongCon > 0 AND HanSuDung >= {2} ORDER BY HanSuDung";
                            candidateLots = await _ctx.TonKhos.FromSqlRaw(sql, maThuoc, donVi, requestedHsd).AsTracking().ToListAsync();
                        }
                        else
                        {
                            var sql = "SELECT * FROM TON_KHO WITH (UPDLOCK, ROWLOCK) WHERE MaThuoc = {0} AND SoLuongCon > 0 AND HanSuDung >= {1} ORDER BY HanSuDung";
                            candidateLots = await _ctx.TonKhos.FromSqlRaw(sql, maThuoc, requestedHsd).AsTracking().ToListAsync();
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
                                        ,HanSuDung = requestedHsd
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
                    var created = await (from h in _ctx.HoaDons
                                          where h.MaHD == hd.MaHD
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
                                          }).FirstOrDefaultAsync();

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
                            ct.HanSuDung,
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
        public async Task<IActionResult> CreateOnline([FromBody] HoaDonCreateOLDto dto)
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
                            ,HanSuDung = null
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

                    var created = await (from h in _ctx.HoaDons
                                          where h.MaHD == hd.MaHD
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
                                          }).FirstOrDefaultAsync();

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
                            ct.HanSuDung,
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

        
        // NOTE: UpdateItems endpoint removed per user request — ConfirmOnline will directly allocate lots like direct Create.

        // POST: api/HoaDon/SendToCustomer/{maHd}
        // Body: pass only invoice id (maHd) as route parameter
        [HttpPost("SendToCustomer/{maHd}")]
        public async Task<IActionResult> SendToCustomer(string maHd)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync<object>(async () =>
            {
                if (string.IsNullOrWhiteSpace(maHd)) throw new ArgumentException("MaHd is required.");

                // Load invoice header
                var invoice = await _ctx.HoaDons.FirstOrDefaultAsync(h => h.MaHD == maHd);
                if (invoice == null) throw new KeyNotFoundException($"Không tìm thấy hoá đơn '{maHd}'.");

                // Find customer display info from invoice.MaKH
                var kh = await _ctx.KhachHangs.FirstOrDefaultAsync(k => k.MAKH == invoice.MaKH);

                // Prefer email from TaiKhoan if available
                var taiKhoan = await _ctx.TaiKhoans.FirstOrDefaultAsync(t => t.MaKH == invoice.MaKH && !string.IsNullOrEmpty(t.EMAIL));
                string? toEmail = taiKhoan?.EMAIL;
                if (string.IsNullOrWhiteSpace(toEmail))
                {
                    throw new InvalidOperationException("Không tìm thấy email của khách hàng. Vui lòng đảm bảo khách hàng có tài khoản và email đã được lưu.");
                }

                // Load grouped items (aggregate same medicines as in ChiTiet/Summary)
                var items = await (from ct in _ctx.ChiTietHoaDons
                                   where ct.MaHD == invoice.MaHD
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
                                       HanSuDungGanNhat = g.Max(x => (DateTime?)x.ct.HanSuDung),
                                       DonGiaTrungBinh = g.Average(x => x.ct.DonGia),
                                       TongThanhTien = g.Sum(x => x.ct.ThanhTien)
                                   })
                    .ToListAsync();

                // Build professional pharmacy invoice (matching real-world pharmacy design)
                var customerName = kh != null && !string.IsNullOrWhiteSpace(kh.HoTen) ? kh.HoTen : invoice.MaKH;
                var nv = await _ctx.Set<NhanVien>().FirstOrDefaultAsync(n => n.MANV == invoice.MaNV);
                // compute status display name
                string statusName = (invoice.TrangThaiGiaoHang == -1) ? "Hủy" :
                                    (invoice.TrangThaiGiaoHang == 0) ? "Đã đặt" :
                                    (invoice.TrangThaiGiaoHang == 1) ? "Đã xác nhận" :
                                    (invoice.TrangThaiGiaoHang == 2) ? "Đã giao" :
                                    (invoice.TrangThaiGiaoHang == 3) ? "Đã nhận" : "Không xác định";

                // For placed (online) orders (status 0) there is no employee to display
                var isPlaced = invoice.TrangThaiGiaoHang == 0;
                var isCancelled = invoice.TrangThaiGiaoHang == -1;
                var employeeName = isPlaced ? null : (nv != null ? nv.HoTen : (string.IsNullOrWhiteSpace(invoice.MaNV) ? "(không xác định)" : invoice.MaNV));
                var totalSectionStyle = isCancelled ? "background:#fff0f0; border-left:4px solid #c82333;" : string.Empty;
                var containerStyle = isCancelled ? "background:#fff0f0;" : string.Empty;
                var headerStyle = isCancelled ? "background: linear-gradient(135deg, #c82333 0%, #e55353 100%); color: white;" : string.Empty;

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
    <div class='invoice-container' style='{containerStyle}'>
        <div class='header' style='{headerStyle}'>
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
                    </div>");

                // only show employee row when not a placed (status 0) order
                if (!isPlaced)
                {
                    html.Append($@"
                    <div class='info-row'>
                        <span class='info-label'>Nhân viên:</span>
                        <span class='info-value'>{WebUtility.HtmlEncode(employeeName ?? "(không xác định)")}</span>
                    </div>");
                }
                    
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
                            <th class='col-total'>Thành Tiền</th>");

                // conditionally include expiry and dosage columns (hide for placed orders)
                if (!isPlaced)
                {
                    html.Append(@"<th class='col-exp'>HSD</th>");
                    html.Append(@"<th class='col-dose'>Liều Dùng</th>");
                }

                html.Append(@"                        </tr>
                    </thead>
                    <tbody>");
                    
                int idx = 1;
                foreach (var it in items)
                {
                    string hanSuDung = it.HanSuDungGanNhat.HasValue ? it.HanSuDungGanNhat.Value.ToString("dd/MM/yyyy") : "---";
                    html.Append($@"
                        <tr>
                            <td class='col-stt'>{idx++}</td>
                            <td class='col-name'>{WebUtility.HtmlEncode(it.TenThuoc ?? "")}</td>
                            <td class='col-unit'>{WebUtility.HtmlEncode(it.TenLoaiDonVi ?? it.MaLoaiDonVi ?? "")}</td>
                            <td class='col-qty'>{it.TongSoLuong}</td>
                            <td class='col-price'>{it.DonGiaTrungBinh.ToString("N0", culture)}đ</td>
                            <td class='col-total'>{it.TongThanhTien.ToString("N0", culture)}đ</td>");

                    if (!isPlaced)
                    {
                        html.Append($@"<td class='col-exp'>{WebUtility.HtmlEncode(hanSuDung)}</td>");
                        html.Append($@"<td class='col-dose'>{WebUtility.HtmlEncode(it.TenLD ?? "---")}</td>");
                    }

                    html.Append("</tr>");
                }
                
                html.Append($@"
                    </tbody>
                </table>
            </div>
            
            <div class='total-section' style='{totalSectionStyle}'>");
                    
                var totalLong = Convert.ToInt64(Math.Round(invoice.TongTien));
                var totalWords = NumberToVietnameseWords(totalLong);
                
                // if order is placed (status 0) or cancelled (-1) render status as its own row above the total
                if (isPlaced || isCancelled)
                {
                    var statusStyle = isCancelled ? "color:#c82333; font-weight:bold;" : "font-size:14px";
                    html.Append($@"
                    <div class='total-row' style='margin-bottom:8px'>
                        <span class='total-label'>Trạng thái:  </span>
                        <span class='total-amount' style='{statusStyle}'>{WebUtility.HtmlEncode(statusName)}</span>
                    </div>");
                }

                html.Append($@"
                <div class='total-row'>
                    <span class='total-label'>TỔNG TIỀN:</span>
                    <span class='total-amount'>{invoice.TongTien.ToString("N0", culture)}đ</span>
                </div>
                <div class='total-words'>
                    <strong>Bằng chữ:</strong> {WebUtility.HtmlEncode(totalWords)} đồng
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

                var subject = isCancelled ? $"Xác nhận hoá đơn {invoice.MaHD} - Hủy - Tại nhà thuốc Melon" : $"Xác nhận hoá đơn {invoice.MaHD} - Tại nhà thuốc Melon";
                var mail = new MailMessage("khangtuong040@gmail.com", toEmail)
                {
                    Subject = subject,
                    Body = html.ToString(),
                    IsBodyHtml = true
                };

                await smtp.SendMailAsync(mail);

                return new { SentTo = toEmail, MaHD = invoice.MaHD };
            });

            return Ok(response);
        }

        
        private static string[] units = { "không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };
        private static string[] scales = { "", " nghìn", " triệu", " tỷ" };

        private static string ReadThreeDigits(long n, bool readZeroHundred)
        {
            int hundreds = (int)(n / 100);
            int tens = (int)((n / 10) % 10);
            int ones = (int)(n % 10);
            var result = new System.Text.StringBuilder();

            if (hundreds > 0 || readZeroHundred)
            {
                result.Append(units[hundreds]).Append(" trăm ");
                if (tens == 0 && ones > 0) result.Append("lẻ ");
            }

            if (tens > 1) // 20-99
            {
                result.Append(units[tens]).Append(" mươi ");
                if (ones == 1) result.Append("mốt");
                else if (ones == 4) result.Append("bốn"); // (hoặc "tư" tùy ngữ cảnh)
                else if (ones == 5) result.Append("lăm");
                else if (ones > 0) result.Append(units[ones]);
            }
            else if (tens == 1) // 10-19
            {
                result.Append("mười ");
                if (ones == 5) result.Append("lăm");
                else if (ones > 0) result.Append(units[ones]);
            }
            else // 0-9
            {
                if (ones > 0 && (hundreds > 0 || tens > 0)) result.Append(units[ones]);
                else if (ones > 0 && hundreds == 0 && tens == 0) result.Append(units[ones]);
                // else (ones == 0) -> do nothing
            }

            return result.ToString().Trim();
        }

        public static string NumberToVietnameseWords(long number)
        {
            if (number == 0) return "không";

            List<string> parts = new List<string>();
            int scaleIdx = 0;
            bool needsZeroReading = false;

            while (number > 0)
            {
                long group = number % 1000;
                if (group > 0)
                {
                    string groupText = ReadThreeDigits(group, needsZeroReading);
                    parts.Insert(0, groupText + scales[scaleIdx]);
                    needsZeroReading = true; // Bất kỳ nhóm nào > 0 đều kích hoạt đọc "không trăm"
                }
                else
                {
                    if (needsZeroReading && parts.Count > 0)
                    {
                        parts.Insert(0, "không trăm");
                    }
                    needsZeroReading = false;
                }

                number /= 1000;
                scaleIdx++;
            }

            return string.Join(" ", parts).Trim().Replace("  ", " ");
        }
        
        // POST: api/HoaDon/ConfirmOnline
        // Body: { MaHD, MaNV, Items: [{ MaThuoc, DonVi, SoLuong, DonGia, MaLD, HanSuDung }] }
        // This confirms an online order: assigns lots, decrements stock, saves requested HanSuDung and MaLD,
        // and sets TrangThaiGiaoHang = 1 (Đã xác nhận).
        [HttpPost("ConfirmOnline")]
        public async Task<IActionResult> ConfirmOnline([FromBody] ConfirmOnlineDto dto)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync<object>(async () =>
            {
                if (dto == null) throw new ArgumentNullException(nameof(dto));
                if (string.IsNullOrWhiteSpace(dto.MaHD)) throw new ArgumentException("MaHD is required.");
                if (string.IsNullOrWhiteSpace(dto.MaNV)) throw new ArgumentException("MaNV is required.");

                await using var tx = await _ctx.Database.BeginTransactionAsync();
                try
                {
                    var hd = await _ctx.HoaDons.FirstOrDefaultAsync(h => h.MaHD == dto.MaHD);
                    if (hd == null) throw new KeyNotFoundException($"Hoá đơn '{dto.MaHD}' không tồn tại.");

                    // set employee and status
                    hd.MaNV = dto.MaNV;
                    hd.TrangThaiGiaoHang = 1; // confirmed

                    // generator for MaCTHD
                    string GenMaCtHd() => "CTHD" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(10, 99).ToString();

                    // Load items to process from existing invoice details (placeholders with MaLo == null).
                    List<HoaDonItemDto> itemsToProcess = new();
                    var placeholders = await _ctx.ChiTietHoaDons
                        .Where(ct => ct.MaHD == dto.MaHD && ct.MaLo == null)
                        .ToListAsync();

                    if (placeholders == null || placeholders.Count == 0)
                        throw new ArgumentException("Không có mục nào để xác nhận. Vui lòng đảm bảo hoá đơn có các mục chờ (placeholders).");

                    // convert placeholders to item requests (require that HanSuDung was previously set)
                    foreach (var ph in placeholders)
                    {
                        if (string.IsNullOrWhiteSpace(ph.MaThuoc)) throw new ArgumentException("Placeholder thiếu MaThuoc.");
                        if (ph.SoLuong <= 0) throw new ArgumentException($"Placeholder có SoLuong không hợp lệ cho MaThuoc '{ph.MaThuoc}'.");
                        if (ph.HanSuDung == null) throw new ArgumentException($"Placeholder cho MaThuoc '{ph.MaThuoc}' chưa có HanSuDung. Vui lòng cập nhật hạn sử dụng trước khi xác nhận.");

                        itemsToProcess.Add(new HoaDonItemDto
                        {
                            MaThuoc = ph.MaThuoc,
                            DonVi = ph.MaLoaiDonVi,
                            SoLuong = ph.SoLuong,
                            DonGia = ph.DonGia,
                            MaLD = ph.MaLD,
                            HanSuDung = ph.HanSuDung
                        });
                    }
                    // remove placeholders — we'll create allocated rows below
                    if (placeholders.Any()) _ctx.Set<ChiTietHoaDon>().RemoveRange(placeholders);

                    foreach (var item in itemsToProcess)
                    {
                        if (string.IsNullOrWhiteSpace(item.MaThuoc)) throw new ArgumentException("Each item must provide 'MaThuoc'.");
                        if (item.SoLuong <= 0) throw new ArgumentException($"Invalid 'SoLuong' for MaThuoc '{item.MaThuoc}'.");
                        if (item.HanSuDung == null) throw new ArgumentException($"Hạn sử dụng (HanSuDung) phải được cung cấp cho MaThuoc '{item.MaThuoc}'.");

                        var donVi = string.IsNullOrWhiteSpace(item.DonVi) ? null : item.DonVi.Trim();
                        var requestedHsd = item.HanSuDung.Value;

                        // Prepare candidate lots (with row locking) ordered by nearest HSD
                        List<Model.Kho.TonKho> candidateLots;
                        if (!string.IsNullOrEmpty(donVi))
                        {
                            var sql = "SELECT * FROM TON_KHO WITH (UPDLOCK, ROWLOCK) WHERE MaThuoc = {0} AND MaLoaiDonViTinh = {1} AND SoLuongCon > 0 AND HanSuDung >= {2} ORDER BY HanSuDung";
                            candidateLots = await _ctx.TonKhos.FromSqlRaw(sql, item.MaThuoc, donVi, requestedHsd).AsTracking().ToListAsync();
                        }
                        else
                        {
                            var sql = "SELECT * FROM TON_KHO WITH (UPDLOCK, ROWLOCK) WHERE MaThuoc = {0} AND SoLuongCon > 0 AND HanSuDung >= {1} ORDER BY HanSuDung";
                            candidateLots = await _ctx.TonKhos.FromSqlRaw(sql, item.MaThuoc, requestedHsd).AsTracking().ToListAsync();
                        }

                        var totalAvailable = candidateLots.Sum(l => l.SoLuongCon);
                        if (totalAvailable < item.SoLuong)
                        {
                            throw new InvalidOperationException($"Hàng thuốc {item.MaThuoc} trong kho không đủ theo hạn yêu cầu. Yêu cầu {item.SoLuong}, có {totalAvailable}.");
                        }

                        int remaining = item.SoLuong;
                        foreach (var tk in candidateLots)
                        {
                            if (remaining <= 0) break;
                            var take = Math.Min(remaining, tk.SoLuongCon);
                            if (take <= 0) continue;

                            tk.SoLuongCon -= take;

                            var cthd = new ChiTietHoaDon
                            {
                                MaCTHD = GenMaCtHd(),
                                MaHD = dto.MaHD,
                                MaLo = tk.MaLo,
                                SoLuong = take,
                                DonGia = item.DonGia,
                                ThanhTien = item.DonGia * take,
                                MaLD = string.IsNullOrWhiteSpace(item.MaLD) ? null : item.MaLD,
                                MaLoaiDonVi = string.IsNullOrWhiteSpace(item.DonVi) ? null : item.DonVi.Trim(),
                                MaThuoc = tk.MaThuoc,
                                HanSuDung = requestedHsd
                            };
                            await _ctx.Set<ChiTietHoaDon>().AddAsync(cthd);

                            remaining -= take;
                        }
                    }

                    // recompute total for the invoice
                    await _ctx.SaveChangesAsync();
                    hd.TongTien = await _ctx.ChiTietHoaDons.Where(ct => ct.MaHD == hd.MaHD).SumAsync(ct => ct.ThanhTien);

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
                            ct.HanSuDung,
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

        // PUT: api/HoaDon/UpdateDetails
        // Body: { MaHD, Items: [{ MaCTHD, HanSuDung, MaLD }] }
        // Upsert HanSuDung and MaLD on ChiTietHoaDon rows for a given invoice (PUT allows insert/update semantics).
        [HttpPut("UpdateDetails")]
        public async Task<IActionResult> UpdateDetails([FromBody] UpdateHoaDonDetailsDto dto)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync<object>(async () =>
            {
                if (dto == null) throw new ArgumentNullException(nameof(dto));
                if (string.IsNullOrWhiteSpace(dto.MaHD)) throw new ArgumentException("MaHD is required.");
                if (dto.Items == null || dto.Items.Count == 0) throw new ArgumentException("Items are required.");

                // Validate invoice exists
                var hd = await _ctx.HoaDons.FirstOrDefaultAsync(h => h.MaHD == dto.MaHD);
                if (hd == null) throw new KeyNotFoundException($"Hoá đơn '{dto.MaHD}' không tồn tại.");

                // Prepare existing lines map (for items that provide MaCTHD)
                var ids = dto.Items.Where(i => !string.IsNullOrWhiteSpace(i.MaCTHD)).Select(i => i.MaCTHD).ToList();
                var existingLines = new List<ChiTietHoaDon>();
                if (ids.Count > 0)
                {
                    existingLines = await _ctx.ChiTietHoaDons.Where(ct => ct.MaHD == dto.MaHD && ids.Contains(ct.MaCTHD)).ToListAsync();
                }

                // generator for MaCTHD for new inserted lines
                string GenMaCtHd() => "CTHD" + DateTime.Now.ToString("yyyyMMddHHmmss") + new Random().Next(10, 99).ToString();

                // Upsert: update existing lines, insert new ones when MaCTHD missing or not found
                foreach (var upd in dto.Items)
                {
                    ChiTietHoaDon? line = null;
                    if (!string.IsNullOrWhiteSpace(upd.MaCTHD))
                    {
                        line = existingLines.FirstOrDefault(l => l.MaCTHD == upd.MaCTHD);
                    }

                    if (line != null)
                    {
                        // update provided fields
                        if (upd.HanSuDung != null) line.HanSuDung = upd.HanSuDung;
                        line.MaLD = string.IsNullOrWhiteSpace(upd.MaLD) ? null : upd.MaLD.Trim();
                        if (!string.IsNullOrWhiteSpace(upd.DonVi)) line.MaLoaiDonVi = upd.DonVi.Trim();
                        if (!string.IsNullOrWhiteSpace(upd.MaThuoc)) line.MaThuoc = upd.MaThuoc.Trim();
                        if (upd.SoLuong != null) line.SoLuong = upd.SoLuong.Value;
                        if (upd.DonGia != null) line.DonGia = upd.DonGia.Value;
                        // recompute ThanhTien if DonGia and SoLuong are available
                        line.ThanhTien = (line.DonGia) * (line.SoLuong);
                    }
                    else
                    {
                        // insert new line - require essential fields
                        if (string.IsNullOrWhiteSpace(upd.MaThuoc)) throw new ArgumentException("MaThuoc is required for new invoice detail items.");
                        if (upd.SoLuong == null || upd.SoLuong <= 0) throw new ArgumentException($"SoLuong must be provided and > 0 for MaThuoc '{upd.MaThuoc}'.");
                        if (upd.DonGia == null || upd.DonGia < 0) throw new ArgumentException($"DonGia must be provided for MaThuoc '{upd.MaThuoc}'.");

                        var newLine = new ChiTietHoaDon
                        {
                            MaCTHD = GenMaCtHd(),
                            MaHD = dto.MaHD,
                            MaLo = null,
                            MaThuoc = upd.MaThuoc.Trim(),
                            MaLoaiDonVi = string.IsNullOrWhiteSpace(upd.DonVi) ? null : upd.DonVi.Trim(),
                            SoLuong = upd.SoLuong.Value,
                            DonGia = upd.DonGia.Value,
                            ThanhTien = upd.DonGia.Value * upd.SoLuong.Value,
                            MaLD = string.IsNullOrWhiteSpace(upd.MaLD) ? null : upd.MaLD.Trim(),
                            HanSuDung = upd.HanSuDung
                        };
                        await _ctx.Set<ChiTietHoaDon>().AddAsync(newLine);
                    }
                }

                await _ctx.SaveChangesAsync();

                // Return updated lines for the invoice
                var details = await _ctx.ChiTietHoaDons
                    .Where(ct => ct.MaHD == dto.MaHD)
                    .Select(ct => new
                    {
                        ct.MaCTHD,
                        ct.MaHD,
                        ct.MaLo,
                        ct.SoLuong,
                        ct.DonGia,
                        ct.ThanhTien,
                        ct.HanSuDung,
                        ct.MaLD,
                        ct.MaLoaiDonVi
                    }).ToListAsync();

                return (object)new { Invoice = new { hd.MaHD, hd.NgayLap, hd.MaKH, hd.MaNV, hd.TongTien, hd.GhiChu, hd.TrangThaiGiaoHang }, Items = details };
            });

            return Ok(response);
        }

        // PATCH: api/HoaDon/UpdateStatus
        // Body: { MaHD, TrangThaiGiaoHang }
        // Update the invoice delivery/status field `TrangThaiGiaoHang`.
        [HttpPatch("UpdateStatus")]
        public async Task<IActionResult> UpdateStatus([FromBody] UpdateHoaDonStatusDto dto)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync<object>(async () =>
            {
                if (dto == null) throw new ArgumentNullException(nameof(dto));
                if (string.IsNullOrWhiteSpace(dto.MaHD)) throw new ArgumentException("MaHD is required.");

                var hd = await _ctx.HoaDons.FirstOrDefaultAsync(h => h.MaHD == dto.MaHD);
                if (hd == null) throw new KeyNotFoundException($"Hoá đơn '{dto.MaHD}' không tồn tại.");

                hd.TrangThaiGiaoHang = dto.TrangThaiGiaoHang;
                await _ctx.SaveChangesAsync();

                return new { hd.MaHD, hd.TrangThaiGiaoHang };
            });

            return Ok(response);
        }

        // DELETE: api/HoaDon/Delete/{maHd}
        // Remove an invoice and its details, and restore stock for any allocated lots.
        [HttpDelete("Delete/{maHd}")]
        public async Task<IActionResult> Delete(string maHd)
        {
            var response = await ApiResponseHelper.ExecuteSafetyAsync<object>(async () =>
            {
                if (string.IsNullOrWhiteSpace(maHd)) throw new ArgumentException("maHd is required.");

                await using var tx = await _ctx.Database.BeginTransactionAsync();
                try
                {
                    var hd = await _ctx.HoaDons.FirstOrDefaultAsync(h => h.MaHD == maHd);
                    if (hd == null) throw new KeyNotFoundException($"Hoá đơn '{maHd}' không tồn tại.");

                    // Load all details for the invoice
                    var details = await _ctx.ChiTietHoaDons.Where(ct => ct.MaHD == maHd).ToListAsync();

                    // For details that have a MaLo assigned, restore the TonKho.SoLuongCon
                    var allocated = details.Where(d => !string.IsNullOrWhiteSpace(d.MaLo)).ToList();
                    if (allocated.Count > 0)
                    {
                        var maLos = allocated.Select(a => a.MaLo).Distinct().ToList();
                        var tonKhos = await _ctx.TonKhos.Where(t => maLos.Contains(t.MaLo)).ToListAsync();

                        foreach (var a in allocated)
                        {
                            var tk = tonKhos.FirstOrDefault(t => t.MaLo == a.MaLo);
                            if (tk != null)
                            {
                                tk.SoLuongCon += a.SoLuong;
                            }
                        }
                    }

                    // Remove all detail rows, then remove the invoice
                    if (details.Count > 0) _ctx.Set<ChiTietHoaDon>().RemoveRange(details);
                    _ctx.Set<HoaDon>().Remove(hd);

                    await _ctx.SaveChangesAsync();
                    await tx.CommitAsync();

                    return new { Deleted = maHd };
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
