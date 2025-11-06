using BE_QLTiemThuoc.Dto;
using BE_QLTiemThuoc.Model;
using BE_QLTiemThuoc.Model.Kho;
using BE_QLTiemThuoc.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BE_QLTiemThuoc.Services
{
    public class PhieuNhapService
    {
        private readonly PhieuNhapRepository _repo;

        public PhieuNhapService(PhieuNhapRepository repo)
        {
            _repo = repo;
        }

        public async Task<List<object>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            var ctx = _repo.Context;
            var phieuNhaps = await ctx.PhieuNhaps
                .Where(pn => pn.NgayNhap >= startDate && pn.NgayNhap <= endDate)
                .Select(pn => new
                {
                    pn.MaPN,
                    pn.NgayNhap,
                    pn.TongTien,
                    pn.GhiChu,
                    TenNCC = ctx.NhaCungCaps.Where(ncc => ncc.MaNCC == pn.MaNCC).Select(ncc => ncc.TenNCC).FirstOrDefault(),
                    TenNV = ctx.Set<NhanVien>().Where(nv => nv.MANV == pn.MaNV).Select(nv => nv.HoTen).FirstOrDefault()
                })
                .ToListAsync();

            return phieuNhaps.Cast<object>().ToList();
        }

        public async Task<string> AddPhieuNhapAsync(PhieuNhapDto phieuNhapDto)
        {
            if (phieuNhapDto == null || phieuNhapDto.ChiTietPhieuNhaps == null) throw new ArgumentException("Invalid input data");

            // Start transaction via repository
            await using var tx = await _repo.BeginTransactionAsync();
            try
            {
                var entryDate = (phieuNhapDto.NgayNhap != default(DateTime)) ? phieuNhapDto.NgayNhap : DateTime.Now;
                string? maPN = phieuNhapDto.MaPN;
                if (string.IsNullOrEmpty(maPN))
                {
                    var year = entryDate.Year;
                    var month = entryDate.Month;
                    var countInMonth = await _repo.CountPhieuNhapsInMonthAsync(year, month);
                    var nextSeq = countInMonth + 1;
                    var yy = year % 100;
                    maPN = $"PN{nextSeq.ToString("D4")}/{yy:D2}-{month:D2}";
                }

                var phieuNhap = new PhieuNhap
                {
                    MaPN = maPN,
                    NgayNhap = entryDate,
                    MaNCC = phieuNhapDto.MaNCC,
                    MaNV = phieuNhapDto.MaNV,
                    TongTien = phieuNhapDto.TongTien,
                    GhiChu = phieuNhapDto.GhiChu
                };

                await _repo.AddPhieuNhapAsync(phieuNhap);

                var existingCount = await _repo.CountChiTietByMaPNAsync(phieuNhap.MaPN!);
                int idx = 0;
                string maPNSuffix = "000";
                try
                {
                    var pn = phieuNhap.MaPN ?? string.Empty;
                    var parts = pn.Split('/');
                    if (parts.Length > 0)
                    {
                        var left = parts[0];
                        if (left.StartsWith("PN")) left = left.Substring(2);
                        if (!string.IsNullOrEmpty(left))
                        {
                            var digits = left.Length <= 3 ? left : left.Substring(left.Length - 3);
                            maPNSuffix = digits.PadLeft(3, '0');
                        }
                    }
                }
                catch { maPNSuffix = "000"; }

                var chiTietEntities = new List<ChiTietPhieuNhap>();
                foreach (var dto in phieuNhapDto.ChiTietPhieuNhaps)
                {
                    idx++;
                    string? maCTPN = dto.MaCTPN;
                    if (string.IsNullOrEmpty(maCTPN))
                    {
                        var seq = existingCount + idx;
                        var yy = phieuNhap.NgayNhap.Year % 100;
                        var mm = phieuNhap.NgayNhap.Month;
                        maCTPN = $"CTPN{seq.ToString("D3")}/{maPNSuffix}/{yy:D2}-{mm:D2}";
                    }
                    dto.MaCTPN = maCTPN;

                    var ent = new ChiTietPhieuNhap
                    {
                        MaCTPN = maCTPN ?? string.Empty,
                        MaPN = phieuNhap.MaPN!,
                        MaThuoc = dto.MaThuoc ?? string.Empty,
                        SoLuong = dto.SoLuong,
                        DonGia = dto.DonGia,
                        ThanhTien = dto.ThanhTien,
                        HanSuDung = dto.HanSuDung ?? phieuNhap.NgayNhap,
                        GhiChu = null
                    };
                    chiTietEntities.Add(ent);
                }

                if (chiTietEntities.Any())
                {
                    await _repo.AddChiTietRangeAsync(chiTietEntities);
                }

                // Units mapping
                var unitCodes = phieuNhapDto.ChiTietPhieuNhaps
                    .Select(x => x.MaLoaiDonViNhap)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!.Trim())
                    .Distinct()
                    .ToList();

                var unitNameByCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (unitCodes.Any())
                {
                    var unitRows = await _repo.GetLoaiDonViByCodesAsync(unitCodes);
                    foreach (var u in unitRows)
                    {
                        if (!string.IsNullOrEmpty(u.MaLoaiDonVi))
                            unitNameByCode[u.MaLoaiDonVi] = u.TenLoaiDonVi ?? u.MaLoaiDonVi;
                    }
                }

                // Build key sets
                var keyPairs = phieuNhapDto.ChiTietPhieuNhaps
                    .Select(d => new { MaThuoc = d.MaThuoc ?? string.Empty, HSD = d.HanSuDung ?? phieuNhap.NgayNhap })
                    .ToList();
                var maThuocSet = keyPairs.Select(k => k.MaThuoc).Distinct().ToList();
                var hsdSet = keyPairs.Select(k => k.HSD.Date).Distinct().ToList();

                var existingLots = await _repo.GetExistingLotsAsync(maThuocSet, hsdSet);
                var lotLookup = new Dictionary<(string, DateTime), TonKho>();
                foreach (var lot in existingLots)
                {
                    var key = (lot.MaThuoc, lot.HanSuDung.Date);
                    if (!lotLookup.ContainsKey(key)) lotLookup[key] = lot;
                }

                int lotIndex = 0;
                foreach (var dto in phieuNhapDto.ChiTietPhieuNhaps)
                {
                    var maThuoc = dto.MaThuoc ?? string.Empty;
                    var hsd = (dto.HanSuDung ?? phieuNhap.NgayNhap).Date;
                    var key = (maThuoc, hsd);

                    if (lotLookup.TryGetValue(key, out var existing))
                    {
                        existing.SoLuongNhap += dto.SoLuong;
                        existing.SoLuongCon += dto.SoLuong;
                    }
                    else
                    {
                        lotIndex++;
                        var maLo = GenMaLo(lotIndex);
                        var code = (dto.MaLoaiDonViNhap ?? string.Empty).Trim();
                        var rawDonViTinh = unitNameByCode.ContainsKey(code) ? unitNameByCode[code] : (string.IsNullOrEmpty(code) ? "" : code);
                        var donViTinh = (rawDonViTinh ?? string.Empty);
                        if (donViTinh.Length > 20) donViTinh = donViTinh.Substring(0, 20);
                        var ghi = phieuNhapDto.GhiChu ?? string.Empty;
                        if (ghi.Length > 255) ghi = ghi.Substring(0, 255);

                        var newLot = new TonKho
                        {
                            MaLo = maLo,
                            MaThuoc = maThuoc,
                            HanSuDung = hsd,
                            TrangThaiSeal = false,
                            DonViTinh = donViTinh,
                            SoLuongNhap = dto.SoLuong,
                            SoLuongCon = dto.SoLuong,
                            GhiChu = ghi
                        };
                        await _repo.Context.TonKhos.AddAsync(newLot);
                        lotLookup[key] = newLot;
                    }
                }

                await _repo.SaveChangesAsync();

                await tx.CommitAsync();
                return phieuNhap.MaPN ?? string.Empty;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            static string GenMaLo(int index)
            {
                var ts = DateTime.UtcNow.ToString("yyMMddHHmmss");
                var id = $"LO{ts}{index:D2}";
                return id;
            }
        }

        public async Task<object> GetChiTietPhieuNhapByMaPNAsync(string maPN)
        {
            // This method will be called by the controller; keep the same projection as before.
            // For simplicity reuse repository's context via internal access (not ideal but keeps changes minimal).
                var ctx = _repo.Context;
            var phieuNhap = await ctx.PhieuNhaps
                .Where(pn => pn.MaPN == maPN)
                .Select(pn => new
                {
                    pn.MaPN,
                    pn.NgayNhap,
                    pn.TongTien,
                    pn.GhiChu,
                    TenNCC = ctx.NhaCungCaps.Where(ncc => ncc.MaNCC == pn.MaNCC).Select(ncc => ncc.TenNCC).FirstOrDefault(),
                    TenNV = ctx.Set<NhanVien>().Where(nv => nv.MANV == pn.MaNV).Select(nv => nv.HoTen).FirstOrDefault()
                })
                .FirstOrDefaultAsync();

            var chiTietPhieuNhaps = await ctx.Set<ChiTietPhieuNhap>()
                .Where(ctpn => ctpn.MaPN == maPN)
                .Select(ctpn => new
                {
                    ctpn.MaCTPN,
                    ctpn.MaPN,
                    ctpn.MaThuoc,
                    TenThuoc = ctx.Thuoc.Where(t => t.MaThuoc == ctpn.MaThuoc).Select(t => t.TenThuoc).FirstOrDefault(),
                    ctpn.SoLuong,
                    ctpn.DonGia,
                    ctpn.ThanhTien,
                    HanSuDung = ctpn.HanSuDung,
                    ctpn.GhiChu
                })
                .ToListAsync();

            return new { PhieuNhap = phieuNhap, ChiTiet = chiTietPhieuNhaps };
        }

        public async Task<List<object>> GetTonKhoByMaPNAsync(string maPN)
        {
            var ctx = _repo.Context;
            var rows = await (
                from tk in ctx.TonKhos
                join ct in ctx.ChiTietPhieuNhaps.Where(c => c.MaPN == maPN)
                    on new { tk.MaThuoc, tk.HanSuDung } equals new { ct.MaThuoc, HanSuDung = ct.HanSuDung!.Value }
                select new
                {
                    tk.MaLo,
                    tk.MaThuoc,
                    tk.HanSuDung,
                    TrangThaiSeal = tk.TrangThaiSeal ? 1 : 0,
                    tk.DonViTinh,
                    tk.SoLuongNhap,
                    tk.SoLuongCon,
                    tk.GhiChu
                }
            ).Distinct().ToListAsync();

            return rows.Cast<object>().ToList();
        }

    }
}
