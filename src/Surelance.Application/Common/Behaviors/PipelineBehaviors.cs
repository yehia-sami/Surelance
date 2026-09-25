using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Surelance.Application.Common.Interfaces;
using Surelance.Application.Common.Models;
using Surelance.Domain.Entities;
using System.Reflection;
using System.Text.Json;

namespace Surelance.Application.Common.Behaviors;

public interface IAuditableCommand
{
    string EntityName { get; }
    string GetEntityId();
}

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .Select(f => f.ErrorMessage)
            .Distinct()
            .ToList();

        if (failures.Count != 0)
        {
            if (typeof(Result).IsAssignableFrom(typeof(TResponse)))
            {
                if (typeof(TResponse) == typeof(Result))
                {
                    return (TResponse)(object)Result.Failure(failures);
                }

                if (typeof(TResponse).IsGenericType && 
                    typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
                {
                    var failureMethod = typeof(TResponse).GetMethod("Failure", 
                        BindingFlags.Public | BindingFlags.Static, 
                        new[] { typeof(IEnumerable<string>) });

                    if (failureMethod != null)
                    {
                        var result = failureMethod.Invoke(null, new object[] { failures });
                        return (TResponse)result!;
                    }
                }
            }

            throw new ValidationException(validationResults.SelectMany(r => r.Errors));
        }

        return await next();
    }
}

// Opt-in via IAuditableCommand keeps sensitive requests (like auth and passwords) out of the audit trail
public class AuditLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<AuditLoggingBehavior<TRequest, TResponse>> _logger;

    public AuditLoggingBehavior(
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        ILogger<AuditLoggingBehavior<TRequest, TResponse>> logger)
    {
        _auditLogRepository = auditLogRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IAuditableCommand auditable)
        {
            return await next();
        }

        var response = await next();
        var succeeded = response is not Result result || result.IsSuccess;

        try
        {
            var auditLog = new AuditLog(
                id: Guid.NewGuid(),
                userId: _currentUserService.UserId,
                userEmail: _currentUserService.Email,
                action: typeof(TRequest).Name,
                entityName: auditable.EntityName,
                entityId: auditable.GetEntityId(),
                details: JsonSerializer.Serialize(request, typeof(TRequest)),
                succeeded: succeeded,
                timestampUtc: _dateTimeProvider.UtcNow);

            await _auditLogRepository.AddAsync(auditLog, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Audit: {Action} on {EntityName} {EntityId} by {UserId} succeeded={Succeeded}",
                auditLog.Action, auditLog.EntityName, auditLog.EntityId, auditLog.UserId, succeeded);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist audit log for {Command}", typeof(TRequest).Name);
        }

        return response;
    }
}
