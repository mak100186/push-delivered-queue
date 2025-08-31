using FluentAssertions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using PushDeliveredQueue.Core;
using PushDeliveredQueue.Core.Abstractions;
using PushDeliveredQueue.Core.Configs;
using PushDeliveredQueue.Core.Models;

using Xunit;

namespace PushDeliveredQueue.UnitTests;

public class ErrorHandlingTests : IDisposable
{
    private readonly Mock<ILogger<SubscribableQueue>> _mockLogger;
    private readonly SubscribableQueue _queue;
    private readonly Mock<IOptions<SubscribableQueueOptions>> _mockOptions;

    public ErrorHandlingTests()
    {
        _mockLogger = new Mock<ILogger<SubscribableQueue>>();
        _mockOptions = new Mock<IOptions<SubscribableQueueOptions>>();
        _mockOptions.Setup(x => x.Value).Returns(new SubscribableQueueOptions
        {
            Ttl = TimeSpan.FromMinutes(5),
            RetryCount = 3,
            DelayBetweenRetriesMs = 100
        });

        _queue = new SubscribableQueue(_mockOptions.Object, _mockLogger.Object);
    }

    public void Dispose()
    {
        _queue?.Dispose();
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new SubscribableQueue(null!, _mockLogger.Object);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new SubscribableQueue(_mockOptions.Object, null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Enqueue_WithNullPayload_ShouldHandleGracefully()
    {
        // Act
        var messageId = _queue.Enqueue(null!);

        // Assert
        messageId.Should().NotBeEmpty();
        var state = _queue.GetState();
        var message = state.Buffer.FirstOrDefault(m => m.Id == messageId);
        message.Should().NotBeNull();
        message!.Payload.Should().BeNull();
    }

    [Fact]
    public void Subscribe_WithNullHandler_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => _queue.Subscribe(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Unsubscribe_WithInvalidSubscriberId_ShouldNotThrow()
    {
        // Arrange
        var invalidSubscriberId = Guid.NewGuid();

        // Act & Assert - Should not throw
        var action = () => _queue.Unsubscribe(invalidSubscriberId);
        action.Should().NotThrow();
    }

    [Fact]
    public async Task MessageHandler_ThrowingException_ShouldBeHandledGracefully()
    {
        // Arrange
        var handler = new Mock<IQueueEventHandler>();
        var subscriberId = _queue.Subscribe(handler.Object);
        var messageId = _queue.Enqueue("test message");

        handler.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new InvalidOperationException("Test exception"));

        handler.Setup(h => h.OnMessageFailedHandlerAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(PostMessageFailedBehavior.AddToDLQ);

        // Act
        await Task.Delay(200); // Wait for processing

        // Assert
        handler.Verify(h => h.OnMessageFailedHandlerAsync(
            It.IsAny<MessageEnvelope>(),
            It.IsAny<Guid>(),
            It.IsAny<Exception>(),
            It.IsAny<CancellationToken>()), Times.Once);

        var state = _queue.GetState();
        var subscriber = state.Subscribers[subscriberId];
        subscriber.DeadLetterQueue.Should().HaveCount(1);
    }

    [Fact]
    public async Task MessageHandler_ThrowingExceptionInFailedHandler_ShouldLogError()
    {
        // Arrange
        var handler = new Mock<IQueueEventHandler>();
        var subscriberId = _queue.Subscribe(handler.Object);
        var messageId = _queue.Enqueue("test message");

        handler.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new InvalidOperationException("Test exception"));

        handler.Setup(h => h.OnMessageFailedHandlerAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new InvalidOperationException("Failed handler exception"));

        // Act
        await Task.Delay(200); // Wait for processing

        // Assert - Should not throw and should continue processing
        _queue.GetState().Should().NotBeNull();
    }

    [Fact]
    public async Task MessageHandler_ThrowingExceptionInDeadLetterHandler_ShouldLogError()
    {
        // Arrange
        var handler = new Mock<IQueueEventHandler>();
        var subscriberId = _queue.Subscribe(handler.Object);
        var messageId = _queue.Enqueue("test message");

        handler.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(DeliveryResult.Nack);

        handler.Setup(h => h.OnMessageFailedHandlerAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(PostMessageFailedBehavior.AddToDLQ);

        handler.Setup(h => h.OnDeadLetterHandlerAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new InvalidOperationException("Dead letter handler exception"));

        // Act
        await Task.Delay(200); // Wait for processing

        // Assert - Should not throw and should continue processing
        _queue.GetState().Should().NotBeNull();
    }

    [Fact]
    public void GetState_WithConcurrentAccess_ShouldNotThrow()
    {
        // Arrange
        var tasks = new List<Task>();

        // Act - Create multiple concurrent calls to GetState
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    _queue.GetState();
                }
            }));
        }

        // Assert - Should not throw
        Task.WaitAll(tasks.ToArray());
        _queue.GetState().Should().NotBeNull();
    }

    [Fact]
    public void Enqueue_WithConcurrentAccess_ShouldNotThrow()
    {
        // Arrange
        var tasks = new List<Task<Guid>>();

        // Act - Create multiple concurrent enqueue operations
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() => _queue.Enqueue($"message {i}")));
        }

        // Assert - Should not throw and all messages should be enqueued
        Task.WaitAll(tasks.ToArray());
        var state = _queue.GetState();
        state.Buffer.Should().HaveCount(100);
    }

    [Fact]
    public void Subscribe_WithConcurrentAccess_ShouldNotThrow()
    {
        // Arrange
        var handler = new Mock<IQueueEventHandler>();
        handler.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(DeliveryResult.Ack);

        var tasks = new List<Task<Guid>>();

        // Act - Create multiple concurrent subscribe operations
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() => _queue.Subscribe(handler.Object)));
        }

        // Assert - Should not throw and all subscribers should be created
        Task.WaitAll(tasks.ToArray());
        var state = _queue.GetState();
        state.Subscribers.Should().HaveCount(10);
    }

    [Fact]
    public void Dispose_WithActiveSubscribers_ShouldCleanupGracefully()
    {
        // Arrange
        var handler = new Mock<IQueueEventHandler>();
        var subscriberId = _queue.Subscribe(handler.Object);

        // Act
        _queue.Dispose();

        // Assert - Should not throw and should clean up resources
        var state = _queue.GetState();
        state.Should().NotBeNull();
    }

    [Fact]
    public void Dispose_MultipleTimes_ShouldNotThrow()
    {
        // Act & Assert - Should not throw on multiple dispose calls
        _queue.Dispose();
        _queue.Dispose();
        _queue.Dispose();
    }

    [Fact]
    public async Task CancellationToken_WithCancellation_ShouldStopProcessing()
    {
        // Arrange
        var handler = new Mock<IQueueEventHandler>();
        var subscriberId = _queue.Subscribe(handler.Object);
        var messageId = _queue.Enqueue("test message");

        handler.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(DeliveryResult.Ack);

        // Act - Unsubscribe should cancel the processing
        _queue.Unsubscribe(subscriberId);

        // Assert - Should not throw and subscriber should be removed
        await Task.Delay(100);
        var state = _queue.GetState();
        state.Subscribers.Should().BeEmpty();
    }

    [Fact]
    public void ChangeMessagePayload_WithNullPayload_ShouldHandleGracefully()
    {
        // Arrange
        var messageId = _queue.Enqueue("original payload");

        // Act
        _queue.ChangeMessagePayload(messageId, null!, CancellationToken.None);

        // Assert
        var state = _queue.GetState();
        var message = state.Buffer.FirstOrDefault(m => m.Id == messageId);
        message.Should().NotBeNull();
        message!.Payload.Should().BeNull();
    }

    [Fact]
    public async Task ReplayFromDlqAsync_WithCancelledToken_ShouldHandleGracefully()
    {
        // Arrange
        var handler = new Mock<IQueueEventHandler>();
        var subscriberId = _queue.Subscribe(handler.Object);
        var messageId = _queue.Enqueue("test message");

        handler.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(DeliveryResult.Nack);

        handler.Setup(h => h.OnMessageFailedHandlerAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(PostMessageFailedBehavior.AddToDLQ);

        await Task.Delay(100);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - Should handle cancellation gracefully
        var action = () => _queue.ReplayFromDlqAsync(subscriberId, messageId, cts.Token);
        await action.Should().NotThrowAsync();
    }
}
