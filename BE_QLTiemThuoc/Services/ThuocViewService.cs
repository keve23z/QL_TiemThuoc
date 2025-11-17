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

        public async Task<List<dynamic>> GetChuaTachLeAsync()
        {
            return await _repo.GetChuaTachLeAsync();
        }

        public async Task<List<dynamic>> GetDaTachLeAsync()
        {
            return await _repo.GetDaTachLeAsync();
        }

        public async Task<List<dynamic>> GetTongSoLuongConAsync()
        {
            return await _repo.GetTongSoLuongConAsync();
        }
    }
}