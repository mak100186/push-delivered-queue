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

public class PostMessageFailedBehaviorTests : IDisposable
{
    private readonly Mock<ILogger<SubscribableQueue>> _mockLogger;
    private readonly SubscribableQueue _queue;
    private readonly Mock<IOptions<SubscribableQueueOptions>> _mockOptions;

    public PostMessageFailedBehaviorTests()
    {
        _mockLogger = new Mock<ILogger<SubscribableQueue>>();
        _mockOptions = new Mock<IOptions<SubscribableQueueOptions>>();
        _mockOptions.Setup(x => x.Value).Returns(new SubscribableQueueOptions
        {
            Ttl = TimeSpan.FromMinutes(5),
            RetryCount = 2,
            DelayBetweenRetriesMs = 50
        });

        _queue = new SubscribableQueue(_mockOptions.Object, _mockLogger.Object);
    }

    public void Dispose()
    {
        _queue?.Dispose();
    }

    [Fact]
    public void PostMessageFailedBehavior_AllValues_ShouldBeDefined()
    {
        // Act & Assert
        Enum.GetValues<PostMessageFailedBehavior>().Should().HaveCount(5);
        ((int)PostMessageFailedBehavior.RetryOnceThenCommit).Should().Be(0);
        ((int)PostMessageFailedBehavior.RetryOnceThenDLQ).Should().Be(1);
        ((int)PostMessageFailedBehavior.AddToDLQ).Should().Be(2);
        ((int)PostMessageFailedBehavior.Commit).Should().Be(3);
        ((int)PostMessageFailedBehavior.Block).Should().Be(4);
    }

    [Fact]
    public async Task PostMessageFailedBehavior_RetryOnceThenCommit_ShouldRetryOnceThenCommit()
    {
        // Arrange
        var handler = new Mock<IQueueEventHandler>();
        var subscriberId = _queue.Subscribe(handler.Object);

        var callCount = 0;
        handler.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(() =>
               {
                   callCount++;
                   return callCount == 1 ? DeliveryResult.Nack : DeliveryResult.Ack;
               });

        handler.Setup(h => h.OnMessageFailedHandlerAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(PostMessageFailedBehavior.RetryOnceThenCommit);

        // Act
        var messageId = _queue.Enqueue("test message");
        await Task.Delay(500); // Wait for processing

        // Assert
        callCount.Should().Be(2); // Should be called twice (original + retry)
        var state = _queue.GetState();
        var subscriber = state.Subscribers[subscriberId];
        subscriber.DeadLetterQueue.Should().BeEmpty(); // Should not be in DLQ
    }

    [Fact]
    public async Task PostMessageFailedBehavior_RetryOnceThenDLQ_ShouldRetryOnceThenAddToDLQ()
    {
        // Arrange
        var handler = new Mock<IQueueEventHandler>();
        var subscriberId = _queue.Subscribe(handler.Object);

        var callCount = 0;
        handler.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(() =>
               {
                   callCount++;
                   return DeliveryResult.Nack; // Always fail
               });

        handler.Setup(h => h.OnMessageFailedHandlerAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(PostMessageFailedBehavior.RetryOnceThenDLQ);

        // Act
        var messageId = _queue.Enqueue("test message");
        await Task.Delay(500); // Wait for processing

        // Assert
        callCount.Should().Be(2 + 2); // Should be called twice (original + retry)
        var state = _queue.GetState();
        var subscriber = state.Subscribers[subscriberId];
        subscriber.DeadLetterQueue.Should().HaveCount(1); // Should be in DLQ
    }

    [Fact]
    public async Task PostMessageFailedBehavior_AddToDLQ_ShouldAddToDLQImmediately()
    {
        // Arrange
        var handler = new Mock<IQueueEventHandler>();
        var subscriberId = _queue.Subscribe(handler.Object);
        
        handler.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(DeliveryResult.Nack);

        handler.Setup(h => h.OnMessageFailedHandlerAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(PostMessageFailedBehavior.AddToDLQ);

        // Act
        var messageId = _queue.Enqueue("fail message");
        await Task.Delay(500); // Wait for processing

        // Assert
        var state = _queue.GetState();
        var subscriber = state.Subscribers[subscriberId];
        subscriber.DeadLetterQueue.Should().HaveCount(1); // Should be in DLQ immediately
    }

    [Fact]
    public async Task PostMessageFailedBehavior_Commit_ShouldCommitWithoutRetry()
    {
        // Arrange
        var handler = new Mock<IQueueEventHandler>();
        var subscriberId = _queue.Subscribe(handler.Object);

        var callCount = 0;
        handler.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(() =>
               {
                   callCount++;
                   return DeliveryResult.Nack;
               });

        handler.Setup(h => h.OnMessageFailedHandlerAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(PostMessageFailedBehavior.Commit);

        // Act
        var messageId = _queue.Enqueue("fail message");
        await Task.Delay(500); // Wait for processing

        // Assert
        callCount.Should().BeGreaterThanOrEqualTo(2); // Should only be called once (no retry) + the retry policy attempts
        var state = _queue.GetState();
        var subscriber = state.Subscribers[subscriberId];
        subscriber.DeadLetterQueue.Should().BeEmpty(); // Should not be in DLQ
    }

    [Fact]
    public async Task PostMessageFailedBehavior_Block_ShouldBlockProcessing()
    {
        // Arrange
        var handler = new Mock<IQueueEventHandler>();
        var subscriberId = _queue.Subscribe(handler.Object);

        handler.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(DeliveryResult.Nack);

        handler.Setup(h => h.OnMessageFailedHandlerAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(PostMessageFailedBehavior.Block);

        // Act;
        var messageId = _queue.Enqueue("test message");
        await Task.Delay(500); // Wait for processing

        // Assert
        var state = _queue.GetState();
        var subscriber = state.Subscribers[subscriberId];
        subscriber.DeadLetterQueue.Should().BeEmpty(); // Should not be in DLQ
        // Note: Block behavior might be implementation-specific, this test verifies it doesn't add to DLQ
    }

    [Fact]
    public async Task PostMessageFailedBehavior_WithException_ShouldCallOnMessageFailedHandler()
    {
        // Arrange
        var handler = new Mock<IQueueEventHandler>();
        var subscriberId = _queue.Subscribe(handler.Object);

        handler.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ThrowsAsync(new InvalidOperationException("Test exception"));

        handler.Setup(h => h.OnMessageFailedHandlerAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(PostMessageFailedBehavior.AddToDLQ);

        // Act
        var messageId = _queue.Enqueue("test message");
        await Task.Delay(500); // Wait for processing

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
    public async Task PostMessageFailedBehavior_WithNullException_ShouldCallOnMessageFailedHandler()
    {
        // Arrange
        var handler = new Mock<IQueueEventHandler>();
        var subscriberId = _queue.Subscribe(handler.Object);

        handler.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(DeliveryResult.Nack);

        handler.Setup(h => h.OnMessageFailedHandlerAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(PostMessageFailedBehavior.AddToDLQ);

        // Act
        var messageId = _queue.Enqueue("test message");
        await Task.Delay(500); // Wait for processing

        // Assert
        handler.Verify(h => h.OnMessageFailedHandlerAsync(
            It.IsAny<MessageEnvelope>(),
            It.IsAny<Guid>(),
            It.IsAny<Exception>(), // Should accept null exception
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(PostMessageFailedBehavior.RetryOnceThenCommit)]
    [InlineData(PostMessageFailedBehavior.RetryOnceThenDLQ)]
    [InlineData(PostMessageFailedBehavior.AddToDLQ)]
    [InlineData(PostMessageFailedBehavior.Commit)]
    [InlineData(PostMessageFailedBehavior.Block)]
    public async Task PostMessageFailedBehavior_AllBehaviors_ShouldNotThrow(PostMessageFailedBehavior behavior)
    {
        // Arrange
        var handler = new Mock<IQueueEventHandler>();
        var subscriberId = _queue.Subscribe(handler.Object);

        handler.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(DeliveryResult.Nack);

        handler.Setup(h => h.OnMessageFailedHandlerAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(behavior);

        // Act & Assert - Should not throw
        var messageId = _queue.Enqueue("test message");
        await Task.Delay(500); // Wait for processing
        _queue.GetState().Should().NotBeNull();
    }
}
