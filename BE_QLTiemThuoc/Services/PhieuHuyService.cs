using BE_QLTiemThuoc.Dto;
using BE_QLTiemThuoc.Model.Kho;
using BE_QLTiemThuoc.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BE_QLTiemThuoc.Services
{
    public class PhieuHuyService
    {
        private readonly PhieuHuyRepository _repo;
        private readonly BE_QLTiemThuoc.Data.AppDbContext _context;

        public PhieuHuyService(PhieuHuyRepository repo, BE_QLTiemThuoc.Data.AppDbContext context)
        {
            _repo = repo;
            _context = context;
        }

        
        /// <summary>
        /// Given a MaLo (lot code), trace and return the originating PhieuNhap(s).
        /// Algorithm:
        /// 1) If there is a ChiTietPhieuNhap with MaLo == maLo, return its MaPN.
        /// 2) Otherwise, look up CT_PHIEU_QUY_DOI rows where MaLoMoi == maLo to find MaLoGoc and the MaPhieuQD that produced it.
        ///    Repeat step 1 for each MaLoGoc. If MaLoGoc itself is not found in ChiTietPhieuNhap, repeat the CT_PHIEU_QUY_DOI lookup
        ///    up to a configurable depth (default 2 conversion levels) to find the original MaPN.
        /// Returns a list of trace results (may be multiple if lot originated from multiple source lots).
        /// </summary>
        public async Task<List<object>> FindOriginalPhieuNhapsByMaLoAsync(string maLo, int maxConversionDepth = 0)
        {
            if (string.IsNullOrWhiteSpace(maLo)) throw new ArgumentException("maLo is required");

            var ctx = _context;
            // Defensive check: ensure the underlying DbConnection has a connection string.
            // If this is empty it usually means configuration/environment wasn't loaded
            // before registering DbContext (or the launch profile overridden it with an empty value).
            var runtimeConn = ctx.Database.GetDbConnection();
            if (runtimeConn == null || string.IsNullOrWhiteSpace(runtimeConn.ConnectionString))
            {
                throw new InvalidOperationException("Database connection is not configured. Ensure 'DefaultConnection' is set in appsettings or provided via environment (.env) and Env.Load() runs before DbContext registration.");
            }
            var results = new List<object>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Normalize input
            string Normalize(string s) => s?.Trim() ?? string.Empty;

            // BFS queue: tuple of (currentMaLo, viaPhieuQD)
            var queue = new Queue<(string MaLo, string? ViaPhieuQD)>();
            queue.Enqueue((Normalize(maLo), null));

            using (var conn = ctx.Database.GetDbConnection())
            {
                await conn.OpenAsync();

                while (queue.Any())
                {
                    var (currentLoRaw, viaPhieuQD) = queue.Dequeue();
                    var currentLo = Normalize(currentLoRaw);
                    if (string.IsNullOrEmpty(currentLo) || visited.Contains(currentLo)) continue;
                    visited.Add(currentLo);

                    // 1) Check ChiTietPhieuNhap (case-insensitive, trimmed)
                    var ct = await ctx.ChiTietPhieuNhaps.AsNoTracking()
                        .FirstOrDefaultAsync(c => c.MaLo != null && c.MaLo.Trim().ToLower() == currentLo.ToLower());

                    if (ct != null)
                    {
                        results.Add(new
                        {
                            MaLo = currentLo,
                            FoundIn = "ChiTietPhieuNhap",
                            MaCTPN = ct.MaCTPN,
                            MaPN = ct.MaPN,
                            ViaPhieuQD = viaPhieuQD
                        });
                        // once found, do not traverse further from this branch
                        continue;
                    }

                    // 2) Not found in ChiTietPhieuNhap -> look up CT_PHIEU_QUY_DOI where MaLoMoi = currentLo
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT MaPhieuQD, MaLoGoc FROM CT_PHIEU_QUY_DOI WHERE LTRIM(RTRIM(MaLoMoi)) = @maLo";
                        var p = cmd.CreateParameter();
                        p.ParameterName = "@maLo";
                        p.Value = currentLo;
                        cmd.Parameters.Add(p);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            var foundAny = false;
                            while (await reader.ReadAsync())
                            {
                                foundAny = true;
                                var maPhieuQD = reader["MaPhieuQD"] == DBNull.Value ? null : reader["MaPhieuQD"].ToString();
                                var maLoGoc = reader["MaLoGoc"] == DBNull.Value ? null : reader["MaLoGoc"].ToString();

                                results.Add(new
                                {
                                    MaLo = currentLo,
                                    FoundIn = "CT_PHIEU_QUY_DOI",
                                    MaPhieuQD = maPhieuQD,
                                    MaLoGoc = maLoGoc?.Trim(),
                                    ViaPhieuQD = viaPhieuQD
                                });

                                // Enqueue source lot for further tracing
                                if (!string.IsNullOrWhiteSpace(maLoGoc))
                                {
                                    queue.Enqueue((Normalize(maLoGoc), maPhieuQD));
                                }
                            }

                            if (!foundAny)
                            {
                                // nothing produced this lot (no CT_PHIEU_QUY_DOI with MaLoMoi)
                                results.Add(new { MaLo = currentLo, FoundIn = "Unknown", ViaPhieuQD = viaPhieuQD });
                            }
                        }
                    }
                }
            }

            return results;
        }
        private string GenerateMaPhieuHuy()
        {
            return "PH" + DateTime.Now.ToString("yyyyMMddHHmmss");
        }

        private string GenerateMaChiTietPhieuHuy()
        {
            return "CTPH" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
        }
    }
}