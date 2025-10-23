using BE_QLTiemThuoc.Data;
using BE_QLTiemThuoc.Model.Kho;
using BE_QLTiemThuoc.Model;
using BE_QLTiemThuoc.Model.Thuoc;
using BE_QLTiemThuoc.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BE_QLTiemThuoc.Model.Kho.Dto; // Add this using directive

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
                            ThanhTien = dto.ThanhTien
                        };
                        chiTietEntities.Add(ent);
                    }

                    if (chiTietEntities.Any())
                    {
                        await _context.Set<ChiTietPhieuNhap>().AddRangeAsync(chiTietEntities);
                        await _context.SaveChangesAsync(); // persist CTPN rows so Lo can FK to them
                    }

                    // 3) Build LoThuocHSD list 1-to-1 from each ChiTiet (MaCTPN -> Lo)
                    var loList = new List<LoThuocHSD>();
                    foreach (var dto in phieuNhapDto.ChiTietPhieuNhaps)
                    {
                        var maCTPN = dto.MaCTPN ?? string.Empty;
                        var lo = new LoThuocHSD
                        {
                            MaLoThuocHSD = string.Empty, // will generate below
                            MaThuoc = dto.MaThuoc ?? string.Empty,
                            MaCTPN = maCTPN,
                            HanSuDung = dto.HanSuDung ?? phieuNhap.NgayNhap,
                            SoLuong = dto.SoLuong,
                            MaLoaiDonViNhap = dto.MaLoaiDonViNhap ?? string.Empty
                        };
                        loList.Add(lo);
                    }

                    // Validate MaLoaiDonViNhap existence
                    var maLoaiDonViUsed = loList.Where(l => !string.IsNullOrEmpty(l.MaLoaiDonViNhap)).Select(l => l.MaLoaiDonViNhap).Distinct().ToList();
                    if (maLoaiDonViUsed.Any())
                    {
                        var existing = await _context.Set<LoaiDonVi>()
                            .Where(ld => ld.MaLoaiDonVi != null && maLoaiDonViUsed.Contains(ld.MaLoaiDonVi!))
                            .Select(ld => ld.MaLoaiDonVi!)
                            .ToListAsync();
                        var missing = maLoaiDonViUsed.Except(existing).ToList();
                        if (missing.Any())
                        {
                            throw new Exception($"Missing MaLoaiDonVi entries: {string.Join(',', missing)}. Please provide valid MaLoaiDonViNhap values.");
                        }
                    }

                    // Generate MaLoThuocHSD values and add
                    // We'll keep per-CTPN counters to create unique LHS numbers
                    var lotSeqByCTPN = new Dictionary<string, int>();
                    foreach (var lo in loList)
                    {
                        var targetCTPN = lo.MaCTPN ?? string.Empty;
                        var existingLotCount = await _context.Set<LoThuocHSD>().Where(l => l.MaCTPN == targetCTPN).CountAsync();
                        int localSeq = 1;
                        if (lotSeqByCTPN.TryGetValue(targetCTPN, out var cur)) localSeq = cur + 1;
                        lotSeqByCTPN[targetCTPN] = localSeq;
                        var finalSeq = existingLotCount + localSeq;

                        // derive suffix from targetCTPN
                        string ctpnSuffix = "000";
                        try
                        {
                            var partsC = (targetCTPN ?? string.Empty).Split('/');
                            if (partsC.Length > 0)
                            {
                                var leftC = partsC[0];
                                if (leftC.StartsWith("CTPN")) leftC = leftC.Substring(4);
                                if (!string.IsNullOrEmpty(leftC))
                                {
                                    var digs = leftC.Length <= 3 ? leftC : leftC.Substring(leftC.Length - 3);
                                    ctpnSuffix = digs.PadLeft(3, '0');
                                }
                            }
                        }
                        catch { ctpnSuffix = "000"; }

                        string maLo = lo.MaLoThuocHSD;
                        if (string.IsNullOrEmpty(maLo))
                        {
                            var yy = phieuNhap.NgayNhap.Year % 100;
                            var mm = phieuNhap.NgayNhap.Month;
                            maLo = $"LHS{finalSeq.ToString("D3")}/{ctpnSuffix}/{maPNSuffix}/{yy:D2}-{mm:D2}";
                        }
                        lo.MaLoThuocHSD = maLo;
                    }

                    if (loList.Any())
                    {
                        await _context.Set<LoThuocHSD>().AddRangeAsync(loList);
                        await _context.SaveChangesAsync();
                    }

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
                        ctpn.ThanhTien
                    })
                    .ToListAsync();

                return new {
                    PhieuNhap = phieuNhap,
                    ChiTiet = chiTietPhieuNhaps
                };
            });

            return Ok(response);
        }
    }
}