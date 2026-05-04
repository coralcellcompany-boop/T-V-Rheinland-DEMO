using Microsoft.AspNetCore.Mvc;

namespace TuvInspection.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _log;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> log, IHostEnvironment env)
    {
        _next = next;
        _log = log;
        _env = env;
    }

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (UnauthorizedAccessException ex)
        {
            await Write(ctx, StatusCodes.Status403Forbidden, "Forbidden", ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            await Write(ctx, StatusCodes.Status404NotFound, "Not found", ex.Message);
        }
        catch (FluentValidation.ValidationException ex)
        {
            var detail = string.Join("; ", ex.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
            await Write(ctx, StatusCodes.Status400BadRequest, "Validation failed", detail);
        }
        catch (InvalidOperationException ex)
        {
            // State-machine guard rejections, illegal mutations, etc.
            await Write(ctx, StatusCodes.Status409Conflict, "Invalid state", ex.Message);
        }
        catch (ArgumentException ex)
        {
            await Write(ctx, StatusCodes.Status400BadRequest, "Bad request", ex.Message);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled exception on {Path}", ctx.Request.Path);
            var detail = _env.IsDevelopment() ? ex.ToString() : "An unexpected error occurred.";
            await Write(ctx, StatusCodes.Status500InternalServerError, "Internal Server Error", detail);
        }
    }

    private static Task Write(HttpContext ctx, int status, string title, string detail)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail, Instance = ctx.Request.Path };
        return ctx.Response.WriteAsJsonAsync(problem);
    }
}
