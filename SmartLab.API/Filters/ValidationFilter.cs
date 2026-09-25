using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SmartLab.API.Filters;

/// <summary>
/// Runs the FluentValidation validator (if one is registered) for every action argument.
/// Failures are thrown as ValidationException and turned into a 400 ApiResponse by ExceptionMiddleware.
/// </summary>
public class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
                continue;

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
                continue;

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            if (!result.IsValid)
                throw new ValidationException(result.Errors);
        }

        await next();
    }
}

public static class ValidationErrorExtensions
{
    /// <summary>Groups failures by camelCase property name: { "email": ["...", "..."] }.</summary>
    public static IDictionary<string, string[]> ToErrorDictionary(this IEnumerable<ValidationFailure> failures)
        => failures
            .GroupBy(f => ToCamelCase(f.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray());

    private static string ToCamelCase(string name)
        => string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
