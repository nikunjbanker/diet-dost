namespace Nutrition.Application.Common.Models;

/// <summary>
/// Universal Result envelope for application operations, decoupling business handlers from HTTP semantics.
/// </summary>
/// <typeparam name="T">The type of the encapsulated payload.</typeparam>
public class Result<T>
{
    public bool Succeeded { get; }
    public T? Data { get; }
    public string? Error { get; }
    public string? ErrorCode { get; }
    public int StatusCode { get; }

    protected Result(bool succeeded, T? data, string? error, string? errorCode, int statusCode)
    {
        Succeeded = succeeded;
        Data = data;
        Error = error;
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    public static Result<T> Success(T data, int statusCode = 200) =>
        new(true, data, null, null, statusCode);

    public static Result<T> Failure(string error, string errorCode = "BadRequest", int statusCode = 400) =>
        new(false, default, error, errorCode, statusCode);

    public static Result<T> NotFound(string message = "Entity not found.") =>
        new(false, default, message, "NotFound", 404);

    public static Result<T> Unauthorized(string message = "Unauthorized.") =>
        new(false, default, message, "Unauthorized", 401);

    public static Result<T> Forbidden(string message = "Forbidden.") =>
        new(false, default, message, "Forbidden", 403);
}

/// <summary>
/// Non-generic Result envelope for operations without return data.
/// </summary>
public class Result : Result<bool>
{
    private Result(bool succeeded, string? error, string? errorCode, int statusCode)
        : base(succeeded, succeeded, error, errorCode, statusCode)
    {
    }

    public static Result Ok(int statusCode = 200) =>
        new(true, null, null, statusCode);

    public static new Result Failure(string error, string errorCode = "BadRequest", int statusCode = 400) =>
        new(false, error, errorCode, statusCode);

    public static new Result NotFound(string message = "Entity not found.") =>
        new(false, message, "NotFound", 404);

    public static new Result Unauthorized(string message = "Unauthorized.") =>
        new(false, message, "Unauthorized", 401);

    public static new Result Forbidden(string message = "Forbidden.") =>
        new(false, message, "Forbidden", 403);
}
