using BE_QLTiemThuoc.Repositories;

namespace BE_QLTiemThuoc.Services
{
    public class ThuocViewService
    {
        private readonly ThuocViewRepository _repo;

        public ThuocViewService(ThuocViewRepository repo)
        {
            _repo = repo;
        }

        public async Task<List<dynamic>> GetChuaTachLeAsync(int? status)
        {
            return await _repo.GetChuaTachLeAsync(status);
        }

        public async Task<List<dynamic>> GetDaTachLeAsync(int? status)
        {
            return await _repo.GetDaTachLeAsync(status);
        }

        public async Task<List<dynamic>> GetTongSoLuongConAsync()
        {
            return await _repo.GetTongSoLuongConAsync();
        }

        public async Task<List<dynamic>> GetSapHetHanAsync(int? days, int? months, int? years, DateTime? fromDate)
        {
            return await _repo.GetSapHetHanAsync(days, months, years, fromDate);
        }

        public async Task<List<dynamic>> GetLichSuLoAsync(string maLo)
        {
            return await _repo.GetLichSuLoAsync(maLo);
        }
    }
}