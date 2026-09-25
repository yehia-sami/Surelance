namespace Surelance.Application.Common.Models;

// Error categories mapped to HTTP status codes by the API layer (400, 404, 403, 409)
public enum ErrorType
{
    None = 0,
    Validation,
    NotFound,
    Forbidden,
    Conflict
}

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public ErrorType ErrorType { get; }
    public string? ErrorMessage { get; }
    public IReadOnlyList<string> Errors { get; }

    protected Result(bool isSuccess, ErrorType errorType, string? error, IEnumerable<string>? errors = null)
    {
        IsSuccess = isSuccess;
        ErrorType = errorType;
        ErrorMessage = error;
        Errors = (errors ?? (string.IsNullOrWhiteSpace(error) ? [] : [error])).ToList().AsReadOnly();
    }

    public static Result Success() => new(true, ErrorType.None, null);

    public static Result Failure(string error, ErrorType errorType = ErrorType.Validation) => new(false, errorType, error);

    public static Result Failure(IEnumerable<string> errors)
    {
        var list = errors.ToList();
        return new(false, ErrorType.Validation, list.FirstOrDefault(), list);
    }

    public static Result NotFound(string error) => Failure(error, ErrorType.NotFound);
    public static Result Forbidden(string error) => Failure(error, ErrorType.Forbidden);
    public static Result Conflict(string error) => Failure(error, ErrorType.Conflict);
}

public class Result<T> : Result
{
    private readonly T? _value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access Value of failed result: {ErrorMessage}");

    protected Result(bool isSuccess, T? value, ErrorType errorType, string? error, IEnumerable<string>? errors = null)
        : base(isSuccess, errorType, error, errors)
    {
        _value = value;
    }

    public static Result<T> Success(T value) => new(true, value, ErrorType.None, null);

    public new static Result<T> Failure(string error, ErrorType errorType = ErrorType.Validation) => new(false, default, errorType, error);

    public new static Result<T> Failure(IEnumerable<string> errors)
    {
        var list = errors.ToList();
        return new(false, default, ErrorType.Validation, list.FirstOrDefault(), list);
    }

    public new static Result<T> NotFound(string error) => Failure(error, ErrorType.NotFound);
    public new static Result<T> Forbidden(string error) => Failure(error, ErrorType.Forbidden);
    public new static Result<T> Conflict(string error) => Failure(error, ErrorType.Conflict);

    public static implicit operator Result<T>(T value) => Success(value);
}
