using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Surelance.Application.Common.Interfaces;
using Surelance.Application.Features.Events;
using Surelance.Domain.Common;
using System.Text.Json;

namespace Surelance.Infrastructure.Outbox;

public interface IOutboxProcessor
{
    Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken = default);
}

public class OutboxProcessor : IOutboxProcessor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var nowUtc = dateTimeProvider.UtcNow;
        var messages = await outboxRepo.GetUnprocessedMessagesAsync(nowUtc, batchSize: 20, cancellationToken);
        if (messages.Count == 0) return;

        _logger.LogInformation("Processing {Count} outbox messages...", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                var eventType = Type.GetType(message.Type)
                    ?? throw new InvalidOperationException($"Could not resolve type '{message.Type}' for outbox message {message.Id}.");

                var domainEvent = JsonSerializer.Deserialize(message.Content, eventType) as IDomainEvent
                    ?? throw new InvalidOperationException($"Deserialization produced null for outbox message {message.Id} of type '{message.Type}'.");

                var notificationType = typeof(DomainEventNotification<>).MakeGenericType(eventType);
                var notification = Activator.CreateInstance(notificationType, domainEvent)
                    ?? throw new InvalidOperationException($"Could not instantiate notification for outbox message {message.Id}.");

                if (notification is INotification mediatrNotification)
                {
                    await publisher.Publish(mediatrNotification, cancellationToken);
                }

                message.MarkProcessed(dateTimeProvider.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch outbox message {MessageId} of type {Type}", message.Id, message.Type);
                message.MarkFailed(dateTimeProvider.UtcNow, ex.Message);
                if (message.RetryCount >= 10)
                {
                    _logger.LogWarning("Outbox message {MessageId} has reached maximum retry count ({RetryCount}) and requires manual intervention.", message.Id, message.RetryCount);
                }
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
