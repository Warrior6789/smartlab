namespace SmartLab.BLL.Common.Exceptions;

/// <summary>Base class for expected business errors. ExceptionMiddleware maps each subclass to an HTTP status.</summary>
public abstract class AppException : Exception
{
    protected AppException(string message, IDictionary<string, string[]>? errors = null) : base(message)
    {
        Errors = errors;
    }

    public IDictionary<string, string[]>? Errors { get; }
}

/// <summary>400</summary>
public class BadRequestException : AppException
{
    public BadRequestException(string message, IDictionary<string, string[]>? errors = null) : base(message, errors) { }
}

/// <summary>401</summary>
public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Chưa đăng nhập hoặc phiên đăng nhập không hợp lệ") : base(message) { }
}

/// <summary>403</summary>
public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Bạn không có quyền thực hiện thao tác này") : base(message) { }
}

/// <summary>404</summary>
public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string entityName, object key) : base($"Không tìm thấy {entityName} với khóa '{key}'") { }
}

/// <summary>409</summary>
public class ConflictException : AppException
{
    public ConflictException(string message, IDictionary<string, string[]>? errors = null) : base(message, errors) { }
}
