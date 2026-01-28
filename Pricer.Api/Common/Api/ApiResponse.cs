namespace Pricer.Api.Common.Api;

public sealed record ApiError(string code, string message);

public sealed record ApiResponse<T>(bool ok, T? data, ApiError? error)
{
    public static ApiResponse<T> Success(T data) => new(true, data, null);
    public static ApiResponse<T> Failure(string code, string message) => new(false, default, new ApiError(code, message));
}
