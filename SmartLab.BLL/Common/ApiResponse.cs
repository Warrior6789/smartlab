namespace SmartLab.BLL.Common;

/// <summary>Standard envelope for every API response: { success, message, data, errors }.</summary>
public class ApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public IDictionary<string, string[]>? Errors { get; init; }

    public static ApiResponse<T> Ok(T? data, string message = "Thành công")
        => new() { Success = true, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message, IDictionary<string, string[]>? errors = null)
        => new() { Success = false, Message = message, Errors = errors };
}

/// <summary>Non-generic shortcut for responses without data.</summary>
public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Ok(string message = "Thành công")
        => new() { Success = true, Message = message };

    public new static ApiResponse Fail(string message, IDictionary<string, string[]>? errors = null)
        => new() { Success = false, Message = message, Errors = errors };
}
