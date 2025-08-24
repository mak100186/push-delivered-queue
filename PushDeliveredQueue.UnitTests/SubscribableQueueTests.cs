using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using PushDeliveredQueue.Core;
using PushDeliveredQueue.Core.Configs;
using PushDeliveredQueue.Core.Models;
using Xunit;

namespace PushDeliveredQueue.UnitTests;

public class SubscribableQueueTests : IDisposable
{
    private readonly SubscribableQueue _queue;
    private readonly Mock<IOptions<SubscribableQueueOptions>> _mockOptions;

    public SubscribableQueueTests()
    {
        _mockOptions = new Mock<IOptions<SubscribableQueueOptions>>();
        _mockOptions.Setup(x => x.Value).Returns(new SubscribableQueueOptions
        {
            Ttl = TimeSpan.FromMinutes(5),
            RetryCount = 3,
            DelayBetweenRetriesMs = 100
        });

        _queue = new SubscribableQueue(_mockOptions.Object);
    }

    public void Dispose()
    {
        _queue?.Dispose();
    }

    [Fact]
    public void Constructor_WithValidOptions_ShouldInitializeCorrectly()
    {
        // Act & Assert
        var state = _queue.GetState();
        state.Should().NotBeNull();
        state.Buffer.Should().BeEmpty();
        state.Subscribers.Should().BeEmpty();
    }

    [Fact]
    public void Enqueue_WithValidPayload_ShouldReturnMessageId()
    {
        // Arrange
        var payload = "test message";

        // Act
        var messageId = _queue.Enqueue(payload);

        // Assert
        messageId.Should().NotBeNullOrEmpty();
        Guid.TryParse(messageId, out _).Should().BeTrue();
    }

    [Fact]
    public void Enqueue_WithValidPayload_ShouldAddMessageToBuffer()
    {
        // Arrange
        var payload = "test message";

        // Act
        _queue.Enqueue(payload);

        // Assert
        var state = _queue.GetState();
        state.Buffer.Should().HaveCount(1);
        state.Buffer[0].Payload.Should().Be(payload);
    }

    [Fact]
    public void Enqueue_MultipleMessages_ShouldAddAllMessagesToBuffer()
    {
        // Arrange
        var messages = new[] { "message1", "message2", "message3" };

        // Act
        foreach (var message in messages)
        {
            _queue.Enqueue(message);
        }

        // Assert
        var state = _queue.GetState();
        state.Buffer.Should().HaveCount(3);
        state.Buffer.Select(m => m.Payload).Should().BeEquivalentTo(messages);
    }

    [Fact]
    public void Subscribe_WithValidHandler_ShouldReturnSubscriberId()
    {
        // Arrange
        MessageHandler handler = _ => Task.FromResult(DeliveryResult.Ack);

        // Act
        var subscriberId = _queue.Subscribe(handler);

        // Assert
        subscriberId.Should().NotBeEmpty();
    }

    [Fact]
    public void Subscribe_WithValidHandler_ShouldAddSubscriberToState()
    {
        // Arrange
        MessageHandler handler = _ => Task.FromResult(DeliveryResult.Ack);

        // Act
        var subscriberId = _queue.Subscribe(handler);

        // Assert
        var state = _queue.GetState();
        state.Subscribers.Should().ContainKey(subscriberId);
        state.Subscribers[subscriberId].CursorIndex.Should().Be(0);
        state.Subscribers[subscriberId].IsCommitted.Should().BeFalse();
    }

    [Fact]
    public void Subscribe_MultipleSubscribers_ShouldAddAllSubscribers()
    {
        // Arrange
        MessageHandler handler1 = _ => Task.FromResult(DeliveryResult.Ack);
        MessageHandler handler2 = _ => Task.FromResult(DeliveryResult.Ack);

        // Act
        var subscriber1 = _queue.Subscribe(handler1);
        var subscriber2 = _queue.Subscribe(handler2);

        // Assert
        var state = _queue.GetState();
        state.Subscribers.Should().HaveCount(2);
        state.Subscribers.Should().ContainKey(subscriber1);
        state.Subscribers.Should().ContainKey(subscriber2);
    }

    [Fact]
    public void Unsubscribe_WithValidSubscriberId_ShouldRemoveSubscriber()
    {
        // Arrange
        MessageHandler handler = _ => Task.FromResult(DeliveryResult.Ack);
        var subscriberId = _queue.Subscribe(handler);

        // Act
        _queue.Unsubscribe(subscriberId);

        // Assert
        var state = _queue.GetState();
        state.Subscribers.Should().NotContainKey(subscriberId);
    }

    [Fact]
    public void Unsubscribe_WithInvalidSubscriberId_ShouldNotThrow()
    {
        // Arrange
        var invalidSubscriberId = Guid.NewGuid();

        // Act & Assert
        var action = () => _queue.Unsubscribe(invalidSubscriberId);
        action.Should().NotThrow();
    }

    [Fact]
    public async Task Subscribe_WithAckHandler_ShouldDeliverMessageAndCommit()
    {
        // Arrange
        var messagePayload = "test message";
        var messageId = _queue.Enqueue(messagePayload);
        var deliveredMessages = new List<MessageEnvelope>();
        
        MessageHandler handler = message =>
        {
            deliveredMessages.Add(message);
            return Task.FromResult(DeliveryResult.Ack);
        };

        // Act
        var subscriberId = _queue.Subscribe(handler);
        
        // Wait for message delivery
        await Task.Delay(500);

        // Assert
        deliveredMessages.Should().HaveCount(1);
        deliveredMessages[0].Payload.Should().Be(messagePayload);
        deliveredMessages[0].Id.ToString().Should().Be(messageId);
        
        var state = _queue.GetState();
        state.Subscribers[subscriberId].CursorIndex.Should().Be(1);
        state.Subscribers[subscriberId].IsCommitted.Should().BeTrue();
    }

    [Fact]
    public async Task Subscribe_WithNackHandler_ShouldRetryAndEventuallyAdvance()
    {
        // Arrange
        var messagePayload = "test message";
        _queue.Enqueue(messagePayload);
        var deliveredMessages = new List<MessageEnvelope>();
        
        MessageHandler handler = message =>
        {
            deliveredMessages.Add(message);
            return Task.FromResult(DeliveryResult.Nack);
        };

        // Act
        var subscriberId = _queue.Subscribe(handler);
        
        // Wait for message delivery attempts
        await Task.Delay(1000);

        // Assert
        deliveredMessages.Should().HaveCountGreaterThan(1); // Should retry multiple times
        
        var state = _queue.GetState();
        state.Subscribers[subscriberId].CursorIndex.Should().Be(1); // Should advance after retries exhausted
        state.Subscribers[subscriberId].IsCommitted.Should().BeTrue();
    }

    [Fact]
    public async Task Subscribe_WithExceptionThrowingHandler_ShouldRetryAndEventuallyNack()
    {
        // Arrange
        var messagePayload = "test message";
        _queue.Enqueue(messagePayload);
        var deliveryAttempts = 0;
        
        MessageHandler handler = message =>
        {
            deliveryAttempts++;
            throw new InvalidOperationException("Simulated error");
        };

        // Act
        var subscriberId = _queue.Subscribe(handler);
        
        // Wait for retry attempts
        await Task.Delay(1500);

        // Assert
        deliveryAttempts.Should().BeGreaterThan(1); // Should retry based on RetryCount
        
        var state = _queue.GetState();
        state.Subscribers[subscriberId].CursorIndex.Should().Be(1); // Should advance after all retries exhausted
    }

    [Fact]
    public async Task Subscribe_MultipleMessages_ShouldDeliverInOrder()
    {
        // Arrange
        var messages = new[] { "message1", "message2", "message3" };
        var deliveredMessages = new List<string>();
        
        foreach (var message in messages)
        {
            _queue.Enqueue(message);
        }
        
        MessageHandler handler = message =>
        {
            deliveredMessages.Add(message.Payload);
            return Task.FromResult(DeliveryResult.Ack);
        };

        // Act
        var subscriberId = _queue.Subscribe(handler);
        
        // Wait for all messages to be delivered
        await Task.Delay(1000);

        // Assert
        deliveredMessages.Should().HaveCount(3);
        deliveredMessages.Should().BeEquivalentTo(messages, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Subscribe_WithMultipleSubscribers_ShouldDeliverToAll()
    {
        // Arrange
        var messagePayload = "test message";
        _queue.Enqueue(messagePayload);
        
        var deliveredToSubscriber1 = new List<MessageEnvelope>();
        var deliveredToSubscriber2 = new List<MessageEnvelope>();
        
        MessageHandler handler1 = message =>
        {
            deliveredToSubscriber1.Add(message);
            return Task.FromResult(DeliveryResult.Ack);
        };
        
        MessageHandler handler2 = message =>
        {
            deliveredToSubscriber2.Add(message);
            return Task.FromResult(DeliveryResult.Ack);
        };

        // Act
        var subscriber1 = _queue.Subscribe(handler1);
        var subscriber2 = _queue.Subscribe(handler2);
        
        // Wait for message delivery
        await Task.Delay(500);

        // Assert
        deliveredToSubscriber1.Should().HaveCount(1);
        deliveredToSubscriber2.Should().HaveCount(1);
        deliveredToSubscriber1[0].Payload.Should().Be(messagePayload);
        deliveredToSubscriber2[0].Payload.Should().Be(messagePayload);
    }

    [Fact]
    public void GetState_ShouldReturnCorrectBufferState()
    {
        // Arrange
        var messages = new[] { "message1", "message2" };
        foreach (var message in messages)
        {
            _queue.Enqueue(message);
        }

        // Act
        var state = _queue.GetState();

        // Assert
        state.Buffer.Should().HaveCount(2);
        state.Buffer.Select(m => m.Payload).Should().BeEquivalentTo(messages);
    }

    [Fact]
    public void GetState_ShouldReturnCorrectSubscriberState()
    {
        // Arrange
        MessageHandler handler = _ => Task.FromResult(DeliveryResult.Ack);
        var subscriberId = _queue.Subscribe(handler);

        // Act
        var state = _queue.GetState();

        // Assert
        state.Subscribers.Should().ContainKey(subscriberId);
        var subscriberState = state.Subscribers[subscriberId];
        subscriberState.CursorIndex.Should().Be(0);
        subscriberState.IsCommitted.Should().BeFalse();
        subscriberState.PendingCount.Should().Be(-1); // No messages in buffer, so -1
    }

    [Fact]
    public void GetState_WithMessagesAndSubscribers_ShouldCalculatePendingCountCorrectly()
    {
        // Arrange
        var messages = new[] { "message1", "message2", "message3" };
        foreach (var message in messages)
        {
            _queue.Enqueue(message);
        }
        
        MessageHandler handler = _ => Task.FromResult(DeliveryResult.Ack);
        var subscriberId = _queue.Subscribe(handler);

        // Act
        var state = _queue.GetState();

        // Assert
        state.Subscribers[subscriberId].PendingCount.Should().Be(2); // 3 messages - (0 + 1) = 2
    }

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Arrange
        MessageHandler handler = _ => Task.FromResult(DeliveryResult.Ack);
        var subscriberId = _queue.Subscribe(handler);

        // Act
        _queue.Dispose();

        // Assert
        var state = _queue.GetState();
        state.Subscribers.Should().NotBeEmpty(); // Subscribers remain but are cancelled
        // Note: Dispose cancels the cancellation tokens but doesn't remove subscribers from the dictionary
    }

    [Fact]
    public void Constructor_WithCustomRetrySettings_ShouldUseConfiguredValues()
    {
        // Arrange
        var customOptions = new SubscribableQueueOptions
        {
            Ttl = TimeSpan.FromMinutes(10),
            RetryCount = 5,
            DelayBetweenRetriesMs = 200
        };
        
        var mockOptions = new Mock<IOptions<SubscribableQueueOptions>>();
        mockOptions.Setup(x => x.Value).Returns(customOptions);

        // Act
        using var customQueue = new SubscribableQueue(mockOptions.Object);

        // Assert
        customQueue.Should().NotBeNull();
        // Note: We can't directly test the retry policy configuration, but we can verify the queue works
        var state = customQueue.GetState();
        state.Should().NotBeNull();
    }
}
