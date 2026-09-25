using System.Text.Json;
using FluentValidation;
using SmartLab.API.Extensions;
using SmartLab.API.Filters;
using SmartLab.BLL.Common;
using SmartLab.BLL.Common.Exceptions;

namespace SmartLab.API.Middleware;

/// <summary>Converts exceptions into ApiResponse JSON with the matching HTTP status code.</summary>
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        var (status, response) = ex switch
        {
            BadRequestException e => (StatusCodes.Status400BadRequest, ApiResponse.Fail(e.Message, e.Errors)),
            ValidationException e => (StatusCodes.Status400BadRequest,
                ApiResponse.Fail("Dữ liệu không hợp lệ", e.Errors.ToErrorDictionary())),
            UnauthorizedException e => (StatusCodes.Status401Unauthorized, ApiResponse.Fail(e.Message)),
            ForbiddenException e => (StatusCodes.Status403Forbidden, ApiResponse.Fail(e.Message)),
            NotFoundException e => (StatusCodes.Status404NotFound, ApiResponse.Fail(e.Message)),
            ConflictException e => (StatusCodes.Status409Conflict, ApiResponse.Fail(e.Message, e.Errors)),
            _ => (StatusCodes.Status500InternalServerError, ApiResponse.Fail("Đã xảy ra lỗi hệ thống")),
        };

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);

        if (context.Response.HasStarted)
            return;

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonDefaults.Options));
    }
}
