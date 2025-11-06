using BE_QLTiemThuoc.Data;
using BE_QLTiemThuoc.Model.Kho;
using BE_QLTiemThuoc.Model;
using BE_QLTiemThuoc.Model.Thuoc;
using BE_QLTiemThuoc.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BE_QLTiemThuoc.Dto; 

namespace BE_QLTiemThuoc.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PhieuNhapController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PhieuNhapController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/PhieuNhap/GetByDateRange?startDate=2025-01-01&endDate=2025-12-31
        [HttpGet("GetByDateRange")]
        public async Task<IActionResult> GetByDateRange(DateTime startDate, DateTime endDate)
        {
            if (startDate > endDate)
            {
                return BadRequest(new ApiResponse<string>
                {
                    Status = -1,
                    Message = "Start date must be earlier than or equal to the end date.",
                    Data = null
                });
            }

            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var phieuNhaps = await _context.PhieuNhaps
                    .Where(pn => pn.NgayNhap >= startDate && pn.NgayNhap <= endDate)
                    .Select(pn => new
                    {
                        pn.MaPN,
                        pn.NgayNhap,
                        pn.TongTien,
                        pn.GhiChu,
                        TenNCC = _context.NhaCungCaps
                            .Where(ncc => ncc.MaNCC == pn.MaNCC)
                            .Select(ncc => ncc.TenNCC)
                            .FirstOrDefault(),
                        TenNV = _context.Set<NhanVien>()
                            .Where(nv => nv.MANV == pn.MaNV) // Corrected property name from 'MaNV' to 'MANV'
                            .Select(nv => nv.HoTen) // Updated to use 'HoTen' for the employee's name
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                return phieuNhaps;
            });

            return Ok(response);
        }

        
        [HttpPost("AddPhieuNhap")]
        public async Task<IActionResult> AddPhieuNhap([FromBody] PhieuNhapDto phieuNhapDto)
        {
            // LoThuocHSDs is optional: if missing, server will generate lots from ChiTietPhieuNhaps
            if (phieuNhapDto == null || phieuNhapDto.ChiTietPhieuNhaps == null)
            {
                return BadRequest(new ApiResponse<string>
                {
                    Status = -1,
                    Message = "Invalid input data.",
                    Data = null
                });
            }

            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                // Use an explicit transaction to ensure atomicity
                await using var tx = await _context.Database.BeginTransactionAsync();
                try
                {
                    // 1) Create or generate MaPN and insert PhieuNhap
                    var entryDate = (phieuNhapDto.NgayNhap != default(DateTime)) ? phieuNhapDto.NgayNhap : DateTime.Now;
                    string? maPN = phieuNhapDto.MaPN;
                    if (string.IsNullOrEmpty(maPN))
                    {
                        var year = entryDate.Year;
                        var month = entryDate.Month;
                        var countInMonth = await _context.PhieuNhaps
                            .Where(p => p.NgayNhap.Year == year && p.NgayNhap.Month == month)
                            .CountAsync();
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

                    await _context.PhieuNhaps.AddAsync(phieuNhap);
                    await _context.SaveChangesAsync(); // persist PhieuNhap so MaPN exists

                    // 2) Create ChiTietPhieuNhap rows with generated MaCTPN
                    var existingCount = await _context.Set<ChiTietPhieuNhap>().Where(c => c.MaPN == phieuNhap.MaPN).CountAsync();
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

                        // reflect back to DTO for later Lo creation
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
                        await _context.Set<ChiTietPhieuNhap>().AddRangeAsync(chiTietEntities);
                        await _context.SaveChangesAsync(); // persist CTPN rows so Lo can FK to them
                    }

                    // 3) Upsert TON_KHO rows by (MaThuoc, HanSuDung):
                    //    - If an existing lot matches same MaThuoc + HanSuDung, increment SoLuongNhap & SoLuongCon
                    //    - Otherwise create a new lot with generated MaLo
                    // Map MaLoaiDonViNhap (code) -> DonViTinh (name)
                    var unitCodes = phieuNhapDto.ChiTietPhieuNhaps
                        .Select(x => x.MaLoaiDonViNhap)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => s!.Trim())
                        .Distinct()
                        .ToList();

                    var unitNameByCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (unitCodes.Any())
                    {
                        var unitRows = await _context.Set<LoaiDonVi>()
                            .Where(ld => ld.MaLoaiDonVi != null && unitCodes.Contains(ld.MaLoaiDonVi!))
                            .Select(ld => new { ld.MaLoaiDonVi, ld.TenLoaiDonVi })
                            .ToListAsync();
                        foreach (var u in unitRows)
                        {
                            if (!string.IsNullOrEmpty(u.MaLoaiDonVi))
                                unitNameByCode[u.MaLoaiDonVi] = u.TenLoaiDonVi ?? u.MaLoaiDonVi;
                        }
                    }

                    // Helper to generate unique MaLo under 20 chars: LO + yyMMddHHmmss + idx(2)
                    string GenMaLo(int index)
                    {
                        var ts = DateTime.UtcNow.ToString("yyMMddHHmmss");
                        var id = $"LO{ts}{index:D2}"; // length ~ 2 + 12 + 2 = 16
                        return id;
                    }

                    // Build key sets to fetch existing lots in bulk
                    var keyPairs = phieuNhapDto.ChiTietPhieuNhaps
                        .Select(d => new { MaThuoc = d.MaThuoc ?? string.Empty, HSD = d.HanSuDung ?? phieuNhap.NgayNhap })
                        .ToList();
                    var maThuocSet = keyPairs.Select(k => k.MaThuoc).Distinct().ToList();
                    var hsdSet = keyPairs.Select(k => k.HSD.Date).Distinct().ToList();

                    var existingLotsQuery = _context.TonKhos
                        .Where(t => maThuocSet.Contains(t.MaThuoc) && hsdSet.Contains(t.HanSuDung.Date));
                    var existingLots = await existingLotsQuery.ToListAsync();

                    // Create a lookup by (MaThuoc, HSD)
                    var lotLookup = new Dictionary<(string, DateTime), TonKho>();
                    foreach (var lot in existingLots)
                    {
                        var key = (lot.MaThuoc, lot.HanSuDung.Date);
                        if (!lotLookup.ContainsKey(key)) lotLookup[key] = lot; // pick first if duplicates
                    }

                    int lotIndex = 0;
                    foreach (var dto in phieuNhapDto.ChiTietPhieuNhaps)
                    {
                        var maThuoc = dto.MaThuoc ?? string.Empty;
                        var hsd = (dto.HanSuDung ?? phieuNhap.NgayNhap).Date;
                        var key = (maThuoc, hsd);

                        if (lotLookup.TryGetValue(key, out var existing))
                        {
                            // Increment quantities on existing lot
                            existing.SoLuongNhap += dto.SoLuong;
                            existing.SoLuongCon += dto.SoLuong;
                            // Keep existing DonViTinh/TrangThaiSeal as-is
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
                            await _context.TonKhos.AddAsync(newLot);
                            lotLookup[key] = newLot;
                        }
                    }

                    await _context.SaveChangesAsync();

                    await tx.CommitAsync();

                    // Return the created MaPN so the client can optionally print or fetch details
                    return phieuNhap.MaPN ?? "";
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
            });

            return Ok(response);
        }

    [HttpGet("GetChiTietPhieuNhapByMaPN")]
        public async Task<IActionResult> GetChiTietPhieuNhapByMaPN(string maPN)
        {
            if (string.IsNullOrEmpty(maPN))
            {
                return BadRequest(new ApiResponse<string>
                {
                    Status = -1,
                    Message = "MaPN cannot be null or empty.",
                    Data = null
                });
            }

            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                // Get main PhieuNhap info
                var phieuNhap = await _context.PhieuNhaps
                    .Where(pn => pn.MaPN == maPN)
                    .Select(pn => new
                    {
                        pn.MaPN,
                        pn.NgayNhap,
                        pn.TongTien,
                        pn.GhiChu,
                        TenNCC = _context.NhaCungCaps
                            .Where(ncc => ncc.MaNCC == pn.MaNCC)
                            .Select(ncc => ncc.TenNCC)
                            .FirstOrDefault(),
                        TenNV = _context.Set<NhanVien>()
                            .Where(nv => nv.MANV == pn.MaNV)
                            .Select(nv => nv.HoTen)
                            .FirstOrDefault()
                    })
                    .FirstOrDefaultAsync();

                // Get chi tiết list
                var chiTietPhieuNhaps = await _context.Set<ChiTietPhieuNhap>()
                    .Where(ctpn => ctpn.MaPN == maPN)
                    .Select(ctpn => new
                    {
                        ctpn.MaCTPN,
                        ctpn.MaPN,
                        ctpn.MaThuoc,
                        TenThuoc = _context.Thuoc
                            .Where(t => t.MaThuoc == ctpn.MaThuoc)
                            .Select(t => t.TenThuoc)
                            .FirstOrDefault(),
                        ctpn.SoLuong,
                        ctpn.DonGia,
                        ctpn.ThanhTien,
                        HanSuDung = ctpn.HanSuDung,
                        ctpn.GhiChu
                    })
                    .ToListAsync();

                return new {
                    PhieuNhap = phieuNhap,
                    ChiTiet = chiTietPhieuNhaps
                };
            });

            return Ok(response);
        }

        // GET: api/PhieuNhap/TonKhoByMaPN?maPN=PN0001/25-11
        // Since TON_KHO no longer stores MaPN, we derive affected lots by joining (MaThuoc, HanSuDung)
        // from ChiTietPhieuNhap with TON_KHO. TrangThaiSeal is returned as 0/1 for FE compatibility.
        [HttpGet("TonKhoByMaPN")]
        public async Task<IActionResult> GetTonKhoByMaPN(string maPN)
        {
            if (string.IsNullOrEmpty(maPN))
            {
                return BadRequest(new ApiResponse<string>
                {
                    Status = -1,
                    Message = "MaPN cannot be null or empty.",
                    Data = null
                });
            }

            var response = await ApiResponseHelper.ExecuteSafetyAsync(async () =>
            {
                var rows = await (
                    from tk in _context.TonKhos
                    join ct in _context.ChiTietPhieuNhaps.Where(c => c.MaPN == maPN)
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
                return rows;
            });

            return Ok(response);
        }
    }
}