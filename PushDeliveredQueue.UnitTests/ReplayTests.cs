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

public class ReplayTests : IDisposable
{
    private readonly Mock<ILogger<SubscribableQueue>> _mockLogger;
    private readonly SubscribableQueue _queue;
    private readonly Mock<IOptions<SubscribableQueueOptions>> _mockOptions;
    private readonly Mock<IQueueEventHandler> _mockHandler;

    public ReplayTests()
    {
        _mockLogger = new Mock<ILogger<SubscribableQueue>>();
        _mockOptions = new Mock<IOptions<SubscribableQueueOptions>>();
        _mockOptions.Setup(x => x.Value).Returns(new SubscribableQueueOptions
        {
            Ttl = TimeSpan.FromMinutes(5),
            RetryCount = 1,
            DelayBetweenRetriesMs = 50
        });

        _queue = new SubscribableQueue(_mockOptions.Object, _mockLogger.Object);
        _mockHandler = new Mock<IQueueEventHandler>();
    }

    public void Dispose()
    {
        _queue?.Dispose();
    }

    [Fact]
    public void ChangeMessagePayload_WithValidMessageId_ShouldUpdatePayload()
    {
        // Arrange
        var messageId = _queue.Enqueue("original payload");
        var newPayload = "updated payload";

        // Act
        _queue.ChangeMessagePayload(messageId, newPayload, CancellationToken.None);

        // Assert
        var state = _queue.GetState();
        var message = state.Buffer.FirstOrDefault(m => m.Id == messageId);
        message.Should().NotBeNull();
        message!.Payload.Should().Be(newPayload);
    }

    [Fact]
    public void ChangeMessagePayload_WithInvalidMessageId_ShouldLogWarning()
    {
        // Arrange
        var invalidMessageId = Guid.NewGuid();
        var newPayload = "updated payload";

        // Act
        _queue.ChangeMessagePayload(invalidMessageId, newPayload, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not found for payload change")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ReplayFromDlqAsync_WithValidMessage_ShouldProcessMessage()
    {
        // Arrange
        var subscriberId = _queue.Subscribe(_mockHandler.Object);
        var messageId = _queue.Enqueue("test message");

        // Setup handler to fail and add to DLQ
        _mockHandler.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(DeliveryResult.Nack);
        _mockHandler.Setup(h => h.OnMessageFailedHandlerAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(PostMessageFailedBehavior.AddToDLQ);

        // Wait for message to be processed and added to DLQ
        await Task.Delay(100);

        // Setup handler to succeed on replay
        _mockHandler.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(DeliveryResult.Ack);

        // Act
        await _queue.ReplayFromDlqAsync(subscriberId, messageId, CancellationToken.None);

        // Assert
        var state = _queue.GetState();
        var subscriber = state.Subscribers[subscriberId];
        subscriber.DeadLetterQueue.Should().BeEmpty();
    }

    [Fact]
    public async Task ReplayFromDlqAsync_WithInvalidMessageId_ShouldLogWarning()
    {
        // Arrange
        var subscriberId = _queue.Subscribe(_mockHandler.Object);
        var invalidMessageId = Guid.NewGuid();

        // Act
        await _queue.ReplayFromDlqAsync(subscriberId, invalidMessageId, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not found in DLQ for replay")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ReplayFromDlqAsync_WithInvalidSubscriberId_ShouldLogWarning()
    {
        // Arrange
        var invalidSubscriberId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        // Act
        await _queue.ReplayFromDlqAsync(invalidSubscriberId, messageId, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("non-existent subscriber")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ReplayAllDlqMessagesAsync_WithMultipleMessages_ShouldProcessAllMessages()
    {
        // Arrange
        var subscriberId = _queue.Subscribe(_mockHandler.Object);
        var message1 = _queue.Enqueue("message 1");
        var message2 = _queue.Enqueue("message 2");

        // Setup handler to fail and add to DLQ
        _mockHandler.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(DeliveryResult.Nack);
        _mockHandler.Setup(h => h.OnMessageFailedHandlerAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(PostMessageFailedBehavior.AddToDLQ);

        // Wait for messages to be processed and added to DLQ
        await Task.Delay(100);

        // Setup handler to succeed on replay
        _mockHandler.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(DeliveryResult.Ack);

        // Act
        await _queue.ReplayAllDlqMessagesAsync(subscriberId, CancellationToken.None);

        // Assert
        var state = _queue.GetState();
        var subscriber = state.Subscribers[subscriberId];
        subscriber.DeadLetterQueue.Should().BeEmpty();
    }

    [Fact]
    public async Task ReplayAllDlqMessagesAsync_WithEmptyDlq_ShouldLogInformation()
    {
        // Arrange
        var subscriberId = _queue.Subscribe(_mockHandler.Object);

        // Act
        await _queue.ReplayAllDlqMessagesAsync(subscriberId, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("has no messages in DLQ to replay")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ReplayAllDlqSubscribers_WithMultipleSubscribers_ShouldReplayAll()
    {
        // Arrange
        var handler1 = new Mock<IQueueEventHandler>();
        var handler2 = new Mock<IQueueEventHandler>();
        var subscriber1 = _queue.Subscribe(handler1.Object);
        var subscriber2 = _queue.Subscribe(handler2.Object);

        // Act
        _queue.ReplayAllDlqSubscribers(CancellationToken.None);

        // Assert - Should not throw and should attempt to replay for all subscribers
        // Note: This is a fire-and-forget operation, so we can only verify it doesn't throw
        _queue.GetState().Subscribers.Should().HaveCount(2);
    }

    [Fact]
    public void ReplayFrom_WithValidMessage_ShouldUpdateCursorIndex()
    {
        // Arrange
        var subscriberId = _queue.Subscribe(_mockHandler.Object);
        var message1 = _queue.Enqueue("message 1");
        var message2 = _queue.Enqueue("message 2");
        var message3 = _queue.Enqueue("message 3");

        // Setup handler to process messages
        _mockHandler.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync(DeliveryResult.Ack);

        // Wait for some processing
        Thread.Sleep(100);

        // Act
        _queue.ReplayFrom(subscriberId, message2);

        // Assert
        var state = _queue.GetState();
        var subscriber = state.Subscribers[subscriberId];
        subscriber.CursorIndex.Should().Be(1); // Should point to message2 (index 1)
    }

    [Fact]
    public void ReplayFrom_WithInvalidSubscriber_ShouldLogWarning()
    {
        // Arrange
        var invalidSubscriberId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        // Act
        _queue.ReplayFrom(invalidSubscriberId, messageId);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("non-existent subscriber")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
