namespace BE_QLTiemThuoc.Services
{
    public class ApiResponse<T>
    {
        public int Status { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }
    public static class ApiResponseHelper
    {
        public static ApiResponse<T> ExecuteSafety<T>(Func<T> func)
        {
            try
            {
                var result = func();
                return new ApiResponse<T>
                {
                    Status = 1,
                    Message = null,
                    Data = result
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<T>
                {
                    Status = -1,
                    Message = ex.Message,
                    Data = default
                };
            }
        }

        public static async Task<ApiResponse<T>> ExecuteSafetyAsync<T>(Func<Task<T>> func)
        {
            try
            {
                var result = await func();
                return new ApiResponse<T>
                {
                    Status = 1,
                    Message = null,
                    Data = result
                };
            }
            catch (Exception ex)
            {
                return new ApiResponse<T>
                {
                    Status = -1,
                    Message = ex.Message,
                    Data = default
                };
            }
        }
    }

}
