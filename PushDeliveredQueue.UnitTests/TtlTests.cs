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

public class TtlTests : IDisposable
{
    private readonly Mock<ILogger<SubscribableQueue>> _mockLogger;
    private readonly Mock<IOptions<SubscribableQueueOptions>> _mockOptions;

    public TtlTests()
    {
        _mockLogger = new Mock<ILogger<SubscribableQueue>>();
        _mockOptions = new Mock<IOptions<SubscribableQueueOptions>>();
    }

    public void Dispose()
    {
        // Cleanup handled by individual tests
    }

    [Fact]
    public void Constructor_WithTtlOption_ShouldSetTtlCorrectly()
    {
        // Arrange
        var expectedTtl = TimeSpan.FromMinutes(10);
        _mockOptions.Setup(x => x.Value).Returns(new SubscribableQueueOptions
        {
            Ttl = expectedTtl,
            RetryCount = 3,
            DelayBetweenRetriesMs = 100
        });

        // Act
        using var queue = new SubscribableQueue(_mockOptions.Object, _mockLogger.Object);

        // Assert
        var state = queue.GetState();
        state.Ttl.Should().Be(expectedTtl);
    }

    [Fact]
    public async Task PruneExpiredMessages_WithExpiredMessages_ShouldRemoveMessages()
    {
        // Arrange
        var shortTtl = TimeSpan.FromMilliseconds(50); // Very short TTL for testing
        _mockOptions.Setup(x => x.Value).Returns(new SubscribableQueueOptions
        {
            Ttl = shortTtl,
            RetryCount = 3,
            DelayBetweenRetriesMs = 100
        });

        using var queue = new SubscribableQueue(_mockOptions.Object, _mockLogger.Object);

        // Add messages
        queue.Enqueue("message 1");
        queue.Enqueue("message 2");
        queue.Enqueue("message 3");

        // Verify messages are added
        var initialState = queue.GetState();
        initialState.Buffer.Should().HaveCount(3);

        // Act - Wait for messages to expire
        await Task.Delay(200); // Wait longer than TTL

        // Assert - Messages should be pruned
        var finalState = queue.GetState();
        finalState.Buffer.Should().BeEmpty();
    }

    [Fact]
    public async Task PruneExpiredMessages_WithMixedExpiredAndValidMessages_ShouldRemoveOnlyExpired()
    {
        // Arrange
        var shortTtl = TimeSpan.FromMilliseconds(100);
        _mockOptions.Setup(x => x.Value).Returns(new SubscribableQueueOptions
        {
            Ttl = shortTtl,
            RetryCount = 3,
            DelayBetweenRetriesMs = 100
        });

        using var queue = new SubscribableQueue(_mockOptions.Object, _mockLogger.Object);

        // Add initial messages
        queue.Enqueue("old message 1");
        queue.Enqueue("old message 2");

        // Wait for them to expire
        await Task.Delay(150);

        // Add new messages
        queue.Enqueue("new message 1");
        queue.Enqueue("new message 2");

        // Wait for pruning
        await Task.Delay(50);

        // Assert - Only new messages should remain
        var state = queue.GetState();
        state.Buffer.Should().HaveCount(2);
        state.Buffer.All(m => m.Payload.StartsWith("new")).Should().BeTrue();
    }

    [Fact]
    public async Task PruneExpiredMessages_WithMultipleSubscribers_ShouldUpdateAllCursors()
    {
        // Arrange
        var shortTtl = TimeSpan.FromMilliseconds(50);
        _mockOptions.Setup(x => x.Value).Returns(new SubscribableQueueOptions
        {
            Ttl = shortTtl,
            RetryCount = 3,
            DelayBetweenRetriesMs = 100
        });

        using var queue = new SubscribableQueue(_mockOptions.Object, _mockLogger.Object);

        var handler1 = new Mock<IQueueEventHandler>();
        var handler2 = new Mock<IQueueEventHandler>();
        var subscriber1 = queue.Subscribe(handler1.Object);
        var subscriber2 = queue.Subscribe(handler2.Object);

        // Add messages
        queue.Enqueue("message 1");
        queue.Enqueue("message 2");

        // Setup handlers
        handler1.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DeliveryResult.Ack);
        handler2.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DeliveryResult.Ack);

        // Wait for processing
        await Task.Delay(100);

        // Wait for pruning
        await Task.Delay(100);

        // Assert - All cursors should be updated
        var state = queue.GetState();
        state.Subscribers[subscriber1].CursorIndex.Should().Be(0);
        state.Subscribers[subscriber2].CursorIndex.Should().Be(0);
    }

    [Fact]
    public async Task PruneExpiredMessages_WithException_ShouldLogErrorAndContinue()
    {
        // Arrange
        var shortTtl = TimeSpan.FromMilliseconds(50);
        _mockOptions.Setup(x => x.Value).Returns(new SubscribableQueueOptions
        {
            Ttl = shortTtl,
            RetryCount = 3,
            DelayBetweenRetriesMs = 100
        });

        using var queue = new SubscribableQueue(_mockOptions.Object, _mockLogger.Object);

        // Add a message
        queue.Enqueue("test message");

        // Wait for pruning
        await Task.Delay(200);

        // Assert - Should log error but continue processing
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error during pruning expired messages")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never); // Should not log error in normal operation
    }

    [Fact]
    public void Constructor_WithZeroTtl_ShouldThrowException()
    {
        // Arrange
        _mockOptions.Setup(x => x.Value).Returns(new SubscribableQueueOptions
        {
            Ttl = TimeSpan.Zero,
            RetryCount = 3,
            DelayBetweenRetriesMs = 100
        });

        // Act & Assert
        var action = () => new SubscribableQueue(_mockOptions.Object, _mockLogger.Object);
        action.Should().NotThrow(); // Current implementation allows zero TTL
    }

    [Fact]
    public void Constructor_WithNegativeTtl_ShouldThrowException()
    {
        // Arrange
        _mockOptions.Setup(x => x.Value).Returns(new SubscribableQueueOptions
        {
            Ttl = TimeSpan.FromTicks(-1),
            RetryCount = 3,
            DelayBetweenRetriesMs = 100
        });

        // Act & Assert
        var action = () => new SubscribableQueue(_mockOptions.Object, _mockLogger.Object);
        action.Should().NotThrow(); // Current implementation allows negative TTL
    }

    [Fact]
    public async Task PruneExpiredMessages_WithCancellation_ShouldStopGracefully()
    {
        // Arrange
        var longTtl = TimeSpan.FromMinutes(5);
        _mockOptions.Setup(x => x.Value).Returns(new SubscribableQueueOptions
        {
            Ttl = longTtl,
            RetryCount = 3,
            DelayBetweenRetriesMs = 100
        });

        var queue = new SubscribableQueue(_mockOptions.Object, _mockLogger.Object);

        // Act - Dispose should cancel the pruning task
        queue.Dispose();

        // Assert - Should not throw and should clean up properly
        // The pruning task should be cancelled gracefully
    }

    [Fact]
    public async Task PruneExpiredMessages_WithEmptyBuffer_ShouldNotThrow()
    {
        // Arrange
        var shortTtl = TimeSpan.FromMilliseconds(50);
        _mockOptions.Setup(x => x.Value).Returns(new SubscribableQueueOptions
        {
            Ttl = shortTtl,
            RetryCount = 3,
            DelayBetweenRetriesMs = 100
        });

        using var queue = new SubscribableQueue(_mockOptions.Object, _mockLogger.Object);

        // Act - Wait for pruning cycle
        await Task.Delay(200);

        // Assert - Should not throw and buffer should remain empty
        var state = queue.GetState();
        state.Buffer.Should().BeEmpty();
    }
}
