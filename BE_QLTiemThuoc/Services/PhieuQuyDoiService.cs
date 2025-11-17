using BE_QLTiemThuoc.Dto;
using BE_QLTiemThuoc.Model.Kho;
using BE_QLTiemThuoc.Repositories;
using BE_QLTiemThuoc.Data;
using Microsoft.EntityFrameworkCore;

namespace BE_QLTiemThuoc.Services
{
    public class PhieuQuyDoiService
    {
        private readonly PhieuNhapRepository _repo;
        private readonly AppDbContext _context;
        // constructor
        public PhieuQuyDoiService(PhieuNhapRepository repo, AppDbContext context)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        // in-memory queue for enqueue-by-ma requests
        private static string GenMaPhieuQD()
        {
            return "QD" + DateTime.UtcNow.ToString("yyMMddHHmmss") + new Random().Next(10, 99).ToString();
        }

        private static string GenMaLo()
        {
            return "LO" + DateTime.UtcNow.ToString("yyMMddHHmmss") + new Random().Next(10, 99).ToString();
        }

        /// <summary>
        /// Quick convert by MaThuoc (single package). unitsPerPackageOverride can be null to use THUOC.SoLuong.
        /// Returns MaPhieuQD and MaLoMoi.
        /// </summary>
        public async Task<(string MaPhieuQD, string MaLoMoi)> QuickConvertByMaAsync(string maThuoc, int? unitsPerPackageOverride = null, string? maLoaiDonViMoi = null, DateTime? hanSuDungMoi = null)
        {
            if (string.IsNullOrWhiteSpace(maThuoc)) throw new ArgumentException("maThuoc is required");

            var ctx = _repo.Context;
            var thuoc = await ctx.Set<Model.Thuoc.Thuoc>().AsNoTracking().FirstOrDefaultAsync(t => t.MaThuoc == maThuoc);
            if (thuoc == null) throw new KeyNotFoundException("Thuoc not found by code");

            int unitsPerPackage;
            if (unitsPerPackageOverride.HasValue && unitsPerPackageOverride.Value > 0)
            {
                unitsPerPackage = unitsPerPackageOverride.Value;
            }
            else
            {
                // Try to resolve units per package from GIATHUOC based on requested target unit
                if (!string.IsNullOrEmpty(maLoaiDonViMoi))
                {
                    var gia = await ctx.GiaThuocs.AsNoTracking().FirstOrDefaultAsync(g => g.MaThuoc == maThuoc && g.MaLoaiDonVi == maLoaiDonViMoi);
                    if (gia != null && gia.SoLuong > 0)
                    {
                        unitsPerPackage = gia.SoLuong;
                    }
                    else
                    {
                        throw new InvalidOperationException("Units per package must be provided or present in GiaThuoc for the given MaLoaiDonVi.");
                    }
                }
                else
                {
                    throw new InvalidOperationException("Units per package must be provided or present in GiaThuoc for the given MaLoaiDonVi.");
                }
            }

            // find nearest expiry sealed lot (TrangThaiSeal = 0 -> false)
            var candidateLot = await ctx.TonKhos.Where(t => t.MaThuoc == maThuoc && !t.TrangThaiSeal && t.SoLuongCon >= 1)
                .OrderBy(t => t.HanSuDung)
                .FirstOrDefaultAsync();
            if (candidateLot == null) throw new InvalidOperationException("No sealed lot with sufficient quantity found");

            // perform single-lot conversion using a transaction
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var ctx2 = _repo.Context;

                // lock the specific lot row to avoid race conditions
                var tk = await ctx2.TonKhos
                    .FromSqlRaw("SELECT * FROM TON_KHO WITH (UPDLOCK, ROWLOCK) WHERE MaLo = {0}", candidateLot.MaLo)
                    .AsTracking()
                    .FirstOrDefaultAsync();

                if (tk == null) throw new KeyNotFoundException($"TonKho '{candidateLot.MaLo}' not found");
                if (tk.TrangThaiSeal) throw new InvalidOperationException("Lot is already split (TrangThaiSeal = 1)");
                if (tk.SoLuongCon < 1) throw new InvalidOperationException("Not enough quantity in source lot");

                // consume one source package
                tk.SoLuongCon -= 1;

                var maPhieu = GenMaPhieuQD();
                var maLoMoi = GenMaLo();

                // determine MaNV to record on the phieu
                var anyNv = await ctx2.Set<Model.NhanVien>().AsNoTracking().FirstOrDefaultAsync();
                var maNVToRecord = anyNv?.MANV;
                if (string.IsNullOrEmpty(maNVToRecord)) throw new InvalidOperationException("No NhanVien found to attribute quick convert phieu.");

                var targetDonVi = string.IsNullOrEmpty(maLoaiDonViMoi) ? "Viên" : maLoaiDonViMoi;

                // determine which expiry date the new lot should have (explicit override or source lot's HSD)
                var targetHanSuDung = hanSuDungMoi ?? tk.HanSuDung;

                // check for existing aggregated lot (same MaThuoc, HanSuDung, MaLoaiDonViTinh)
                var existing = await ctx2.TonKhos
                    .Where(t => t.MaThuoc == tk.MaThuoc && t.HanSuDung == targetHanSuDung && t.MaLoaiDonViTinh == targetDonVi)
                    .FirstOrDefaultAsync();

                if (existing != null)
                {
                    existing.SoLuongNhap += unitsPerPackage;
                    existing.SoLuongCon += unitsPerPackage;
                    maLoMoi = existing.MaLo;
                }
                else
                {
                    var newLot = new TonKho
                    {
                        MaLo = maLoMoi,
                        MaThuoc = tk.MaThuoc,
                        HanSuDung = targetHanSuDung,
                        TrangThaiSeal = true,
                        MaLoaiDonViTinh = targetDonVi,
                        SoLuongNhap = unitsPerPackage,
                        SoLuongCon = unitsPerPackage,
                        GhiChu = $"Tách từ {tk.MaLo} (Phiếu {maPhieu})"
                    };
                    await ctx2.TonKhos.AddAsync(newLot);
                }

                // persist source lot update and possibly new TON_KHO so MaLoMoi exists
                await ctx2.SaveChangesAsync();

                // insert PHIEU_QUY_DOI
                await ctx2.Database.ExecuteSqlRawAsync(
                    "INSERT INTO PHIEU_QUY_DOI (MaPhieuQD, NgayQuyDoi, MaNV, GhiChu) VALUES ({0}, {1}, {2}, {3})",
                    maPhieu, DateTime.Now, maNVToRecord, $"Quick convert: {maThuoc}");

                // insert CT_PHIEU_QUY_DOI
                await ctx2.Database.ExecuteSqlRawAsync(
                    "INSERT INTO CT_PHIEU_QUY_DOI (MaPhieuQD, MaLoGoc, MaLoMoi, MaThuoc, SoLuongQuyDoi, TyLeQuyDoi, SoLuongGoc, MaLoaiDonViGoc, MaLoaiDonViMoi) VALUES ({0},{1},{2},{3},{4},{5},{6},{7},{8})",
                    maPhieu, tk.MaLo, maLoMoi, tk.MaThuoc, unitsPerPackage, unitsPerPackage, 1, tk.MaLoaiDonViTinh, targetDonVi);

                await tx.CommitAsync();
                return (maPhieu, maLoMoi);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        /// <summary>
        /// Create a PHIEU_QUY_DOI for a batch of items (multiple source lots). Returns MaPhieuQD and list of MaLoMoi created.
        /// </summary>
        public async Task<(string MaPhieuQD, List<string> MaLoMoi)> CreatePhieuQuyDoiBatchAsync(PhieuQuyDoiBatchCreateDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (dto.Items == null || !dto.Items.Any()) throw new ArgumentException("Items list is required and cannot be empty");
            // New implementation: items contain MaThuoc (medicine code). For each item, consume sealed lots (TrangThaiSeal = false)
            // in HSD order until SoLuongGoc is satisfied. This may produce multiple CT rows per item.
            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var ctx = _repo.Context;

                var ctRows = new List<(string MaLoGoc, string MaLoMoi, string MaThuoc, int SoLuongQuyDoi, int TyLeQuyDoi, int SoLuongGoc, string MaLoaiDonViGoc, string MaLoaiDonViMoi)>();
                var createdMaLo = new List<string>();

                foreach (var item in dto.Items)
                {
                    if (item == null) throw new ArgumentException("Item cannot be null");
                    if (string.IsNullOrEmpty(item.MaThuoc)) throw new ArgumentException("MaThuoc is required for every item");
                    if (item.SoLuongGoc <= 0) throw new ArgumentException("SoLuongGoc must be > 0 for every item");

                    // resolve units per package from THUOC when not provided
                    var thuoc = await ctx.Set<Model.Thuoc.Thuoc>().AsNoTracking().FirstOrDefaultAsync(t => t.MaThuoc == item.MaThuoc);
                    if (thuoc == null) throw new KeyNotFoundException($"Thuoc not found for MaThuoc {item.MaThuoc}");
                    int unitsPerPackage = item.TyLeQuyDoi.HasValue && item.TyLeQuyDoi.Value > 0 ? item.TyLeQuyDoi.Value : throw new InvalidOperationException("TyLeQuyDoi (units per package) is required when Thuoc.SoLuong is not available.");

                    // collect candidate lots for this MaThuoc (sealed packages still in sealed state i.e., TrangThaiSeal == false)
                    var candidateLots = await ctx.TonKhos
                        .Where(t => t.MaThuoc == item.MaThuoc && !t.TrangThaiSeal && t.SoLuongCon > 0)
                        .OrderBy(t => t.HanSuDung)
                        .ToListAsync();

                    var totalAvailable = candidateLots.Sum(l => l.SoLuongCon);
                    if (totalAvailable < item.SoLuongGoc) throw new InvalidOperationException($"Not enough quantity across all sealed lots to fulfill MaThuoc {item.MaThuoc}");

                    int remaining = item.SoLuongGoc;
                    foreach (var lot in candidateLots)
                    {
                        if (remaining <= 0) break;

                        var take = Math.Min(remaining, lot.SoLuongCon);

                        // re-lock the specific lot row before mutating
                        var tk = await ctx.TonKhos.FromSqlRaw("SELECT * FROM TON_KHO WITH (UPDLOCK, ROWLOCK) WHERE MaLo = {0}", lot.MaLo).AsTracking().FirstOrDefaultAsync();
                        if (tk == null) throw new KeyNotFoundException($"TonKho '{lot.MaLo}' not found during consumption");
                        if (tk.TrangThaiSeal) throw new InvalidOperationException($"Lot {lot.MaLo} is already split (TrangThaiSeal = 1)");
                        if (tk.SoLuongCon < take) throw new InvalidOperationException($"Concurrent update reduced availability for lot {lot.MaLo}");

                        // decrement source lot by number of packages
                        tk.SoLuongCon -= take;

                        // compute loose units
                        int soLuongQuyDoi;
                        try { checked { soLuongQuyDoi = take * unitsPerPackage; } }
                        catch (OverflowException ex) { throw new InvalidOperationException("Quantity overflow during conversion", ex); }

                        var targetDonVi = string.IsNullOrEmpty(item.MaLoaiDonViMoi) ? "Viên" : item.MaLoaiDonViMoi;

                        // decide expiry for the new lot (explicit override per item, otherwise use source lot HSD)
                        var targetHanSuDung = item.HanSuDungMoi ?? tk.HanSuDung;

                        // check for existing aggregated lot with same MaThuoc, HanSuDung, MaLoaiDonViTinh
                        var existing = await ctx.TonKhos
                            .Where(t => t.MaThuoc == tk.MaThuoc && t.HanSuDung == targetHanSuDung && t.MaLoaiDonViTinh == targetDonVi)
                            .FirstOrDefaultAsync();

                        string maLoMoi;
                        if (existing != null)
                        {
                            existing.SoLuongNhap += soLuongQuyDoi;
                            existing.SoLuongCon += soLuongQuyDoi;
                            maLoMoi = existing.MaLo;
                        }
                        else
                        {
                            maLoMoi = GenMaLo();
                            var newLot = new TonKho
                            {
                                MaLo = maLoMoi,
                                MaThuoc = tk.MaThuoc,
                                HanSuDung = targetHanSuDung,
                                TrangThaiSeal = true,
                                MaLoaiDonViTinh = targetDonVi,
                                SoLuongNhap = soLuongQuyDoi,
                                SoLuongCon = soLuongQuyDoi,
                                GhiChu = $"Tách từ {tk.MaLo} (Phiếu ... pending)"
                            };
                            await ctx.TonKhos.AddAsync(newLot);
                        }

                        // record CT row for this consumed piece (use MaLoaiDonViGoc/MaLoaiDonViMoi column semantics)
                        ctRows.Add((tk.MaLo, maLoMoi, tk.MaThuoc, soLuongQuyDoi, unitsPerPackage, take, tk.MaLoaiDonViTinh, targetDonVi));
                        if (!createdMaLo.Contains(maLoMoi)) createdMaLo.Add(maLoMoi);

                        remaining -= take;
                    }
                }

                // persist all changes (source lot decreases and new/aggregated TON_KHO rows)
                await ctx.SaveChangesAsync();

                // generate MaPhieu and insert PHIEU_QUY_DOI
                var maPhieu = GenMaPhieuQD();
                string? maNVToRecord = dto.MaNV;
                if (string.IsNullOrEmpty(maNVToRecord))
                {
                    var anyNv = await ctx.Set<Model.NhanVien>().AsNoTracking().FirstOrDefaultAsync();
                    maNVToRecord = anyNv?.MANV;
                }
                if (string.IsNullOrEmpty(maNVToRecord)) throw new InvalidOperationException("No MaNV provided and no NhanVien found in database to attribute the phieu.");

                await ctx.Database.ExecuteSqlRawAsync(
                    "INSERT INTO PHIEU_QUY_DOI (MaPhieuQD, NgayQuyDoi, MaNV, GhiChu) VALUES ({0}, {1}, {2}, {3})",
                    maPhieu, DateTime.Now, maNVToRecord, dto.GhiChu ?? string.Empty);

                // insert CT rows
                foreach (var r in ctRows)
                {
                    await ctx.Database.ExecuteSqlRawAsync(
                        "INSERT INTO CT_PHIEU_QUY_DOI (MaPhieuQD, MaLoGoc, MaLoMoi, MaThuoc, SoLuongQuyDoi, TyLeQuyDoi, SoLuongGoc, MaLoaiDonViGoc, MaLoaiDonViMoi) VALUES ({0},{1},{2},{3},{4},{5},{6},{7},{8})",
                        maPhieu, r.MaLoGoc, r.MaLoMoi, r.MaThuoc, r.SoLuongQuyDoi, r.TyLeQuyDoi, r.SoLuongGoc, r.MaLoaiDonViGoc, r.MaLoaiDonViMoi);
                }

                await tx.CommitAsync();
                return (maPhieu, createdMaLo);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
    }
}
