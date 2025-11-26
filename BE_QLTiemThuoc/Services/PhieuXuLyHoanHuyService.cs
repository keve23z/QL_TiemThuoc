using BE_QLTiemThuoc.Model.Kho;
using BE_QLTiemThuoc.Repositories;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BE_QLTiemThuoc.Data;
using System;
using System.Linq;

namespace BE_QLTiemThuoc.Services
{
    public class PhieuXuLyHoanHuyService
    {
        private readonly PhieuXuLyHoanHuyRepository _repo;
        private readonly AppDbContext _ctx;

        public PhieuXuLyHoanHuyService(PhieuXuLyHoanHuyRepository repo, AppDbContext ctx)
        {
            _repo = repo;
            _ctx = ctx;
        }

        public async Task<List<dynamic>> GetAllAsync(System.DateTime? start = null, System.DateTime? end = null, string? maNV_Tao = null, bool? loaiNguon = null, int? trangThai = null)
        {
            var list = await _repo.GetAllAsync(start, end, maNV_Tao, loaiNguon, trangThai);
            var outList = new List<dynamic>();

            // collect maNV codes to fetch names (cast to string to avoid dynamic LINQ overload issues)
            var maNvs = list.Select(p => (string?)p.MaNV_Tao).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var maNvsDuyet = list.Select(p => (string?)p.MaNV_Duyet).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var allMa = maNvs.Concat(maNvsDuyet).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var nvMap = new Dictionary<string, string?>();
            if (allMa.Any())
            {
                nvMap = _ctx.NhanViens.AsNoTracking()
                    .Where(n => allMa.Contains(n.MaNV))
                    .ToDictionary(n => n.MaNV, n => n.HoTen);
            }

            foreach (var p in list)
            {
                dynamic item = new System.Dynamic.ExpandoObject();
                var d = (IDictionary<string, object>)item;
                d["MaPXH"] = p.MaPXH;
                d["NgayTao"] = p.NgayTao;
                d["MaNV_Tao"] = p.MaNV_Tao;
                d["MaNV_Duyet"] = p.MaNV_Duyet;
                d["NhanVienTaoName"] = (p.MaNV_Tao != null && nvMap.ContainsKey(p.MaNV_Tao)) ? nvMap[p.MaNV_Tao] : null;
                d["NhanVienDuyetName"] = (p.MaNV_Duyet != null && nvMap.ContainsKey(p.MaNV_Duyet)) ? nvMap[p.MaNV_Duyet] : null;
                d["MaHD"] = p.MaHD;
                d["LoaiNguon"] = p.LoaiNguon;
                d["TrangThai"] = p.TrangThai;
                d["LyDo"] = p.LyDo;
                outList.Add(item);
            }

            return outList;
        }

        public async Task<dynamic?> GetDetailsAsync(string maPXH)
        {
            return await _repo.GetDetailsByMaAsync(maPXH);
        }

        public async Task<dynamic> CreateQuickRequestAsync(Dto.PhieuXuLyHoanHuyQuickCreateDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            if (string.IsNullOrWhiteSpace(dto.MaNV)) throw new ArgumentException("MaNV is required");
            if (string.IsNullOrWhiteSpace(dto.MaThuoc)) throw new ArgumentException("MaThuoc is required");
            if (string.IsNullOrWhiteSpace(dto.MaLoaiDonVi)) throw new ArgumentException("MaLoaiDonVi is required");
            if (dto.SoLuong <= 0) throw new ArgumentException("SoLuong must be > 0");

            // find candidate lots ordered by nearest expiry (earliest HanSuDung first)
            var candidateLots = await _ctx.TonKhos
                .Where(t => t.MaThuoc == dto.MaThuoc && t.MaLoaiDonViTinh == dto.MaLoaiDonVi && t.SoLuongCon > 0)
                .OrderBy(t => t.HanSuDung)
                .ToListAsync();

            var totalAvailable = candidateLots.Sum(t => t.SoLuongCon);
            if (totalAvailable < dto.SoLuong) throw new InvalidOperationException("Insufficient total stock to satisfy the request");

            // create PXH header
            var maPXH = "PXH" + DateTime.UtcNow.ToString("yyMMddHHmmss") + new Random().Next(10, 99).ToString();
            var phieu = new PhieuXuLyHoanHuy
            {
                MaPXH = maPXH,
                NgayTao = DateTime.Now,
                MaNV_Tao = dto.MaNV,
                LoaiNguon = false, // from kho
                TrangThai = 0, // chờ duyệt
                MaHD = null,
            };

            // Set LyDo or fallback GhiChu via reflection if the model has that property
            void SetLyDoIfPossible(object target, string? value)
            {
                var pi = target.GetType().GetProperty("LyDo");
                if (pi == null)
                {
                    pi = target.GetType().GetProperty("GhiChu");
                }
                if (pi != null && pi.CanWrite)
                {
                    pi.SetValue(target, value);
                }
            }

            var remaining = dto.SoLuong;
            var createdCTs = new List<dynamic>();

            await using var tx = await _ctx.Database.BeginTransactionAsync();
            try
            {
                await _ctx.PhieuXuLyHoanHuys.AddAsync(phieu);
                // assign LyDo/GhiChu on the tracked entity
                SetLyDoIfPossible(phieu, dto.LyDo);
                // Save header first so the FK constraint for ChiTietPhieuXuLy is satisfied
                await _ctx.SaveChangesAsync();

                foreach (var lotItem in candidateLots)
                {
                    if (remaining <= 0) break;
                    var take = Math.Min(remaining, lotItem.SoLuongCon);
                    if (take <= 0) continue;

                    var maCT = "CTPXH" + DateTime.UtcNow.ToString("yyMMddHHmmss") + new Random().Next(10, 99).ToString();
                    var ct = new ChiTietPhieuXuLy
                    {
                        MaCTPXH = maCT,
                        MaPXH = maPXH,
                        MaLo = lotItem.MaLo,
                        SoLuong = take,
                        LoaiXuLy = false
                    };

                    await _ctx.Set<ChiTietPhieuXuLy>().AddAsync(ct);

                    lotItem.SoLuongCon -= take;
                    if (lotItem.SoLuongCon < 0) throw new InvalidOperationException("Inventory went negative unexpectedly");
                    _ctx.TonKhos.Update(lotItem);

                    createdCTs.Add(new { MaCT = maCT, MaLo = lotItem.MaLo, SoLuong = take, SoLuongConRemaining = lotItem.SoLuongCon });

                    remaining -= take;
                }

                if (remaining > 0) throw new InvalidOperationException("Unable to allocate required quantity from lots");

                await _ctx.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            dynamic outObj = new System.Dynamic.ExpandoObject();
            var d = (IDictionary<string, object>)outObj;
            d["MaPXH"] = maPXH;
            d["CTs"] = createdCTs;
            return outObj;
        }
    }
}
