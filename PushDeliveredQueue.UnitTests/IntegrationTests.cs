using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PushDeliveredQueue.AspNetCore.DependencyInjection;
using PushDeliveredQueue.Core;
using PushDeliveredQueue.Core.Configs;
using PushDeliveredQueue.Core.Models;
using Xunit;

namespace PushDeliveredQueue.UnitTests;

public class IntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly SubscribableQueue _queue;

    public IntegrationTests()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SubscribableQueue:Ttl"] = "00:05:00",
                ["SubscribableQueue:RetryCount"] = "2",
                ["SubscribableQueue:DelayBetweenRetriesMs"] = "50"
            })
            .Build();

        services.AddSubscribableQueueWithOptions(configuration);
        _serviceProvider = services.BuildServiceProvider();
        _queue = _serviceProvider.GetRequiredService<SubscribableQueue>();
    }

    public void Dispose()
    {
        _queue?.Dispose();
        _serviceProvider?.Dispose();
    }

    [Fact]
    public async Task CompleteMessageLifecycle_ShouldWorkCorrectly()
    {
        // Arrange
        var messages = new[] { "message1", "message2", "message3" };
        var deliveredMessages = new List<string>();
        var messageIds = new List<string>();

        // Enqueue messages
        foreach (var message in messages)
        {
            var messageId = _queue.Enqueue(message);
            messageIds.Add(messageId);
        }

        // Subscribe with handler
        MessageHandler handler = message =>
        {
            deliveredMessages.Add(message.Payload);
            return Task.FromResult(DeliveryResult.Ack);
        };

        var subscriberId = _queue.Subscribe(handler);

        // Act - Wait for all messages to be processed
        await Task.Delay(1000);

        // Assert
        deliveredMessages.Should().HaveCount(3);
        deliveredMessages.Should().BeEquivalentTo(messages, options => options.WithStrictOrdering());

        var state = _queue.GetState();
        state.Subscribers[subscriberId].CursorIndex.Should().Be(3);
        state.Subscribers[subscriberId].IsCommitted.Should().BeTrue();
        state.Subscribers[subscriberId].PendingCount.Should().Be(-1); // 3 messages - (3 + 1) = -1
    }

    [Fact]
    public async Task MultipleSubscribers_ShouldReceiveAllMessages()
    {
        // Arrange
        var messages = new[] { "message1", "message2" };
        var subscriber1Messages = new List<string>();
        var subscriber2Messages = new List<string>();

        // Enqueue messages
        foreach (var message in messages)
        {
            _queue.Enqueue(message);
        }

        // Subscribe two handlers
        MessageHandler handler1 = message =>
        {
            subscriber1Messages.Add(message.Payload);
            return Task.FromResult(DeliveryResult.Ack);
        };

        MessageHandler handler2 = message =>
        {
            subscriber2Messages.Add(message.Payload);
            return Task.FromResult(DeliveryResult.Ack);
        };

        var subscriber1 = _queue.Subscribe(handler1);
        var subscriber2 = _queue.Subscribe(handler2);

        // Act - Wait for processing
        await Task.Delay(1000);

        // Assert
        subscriber1Messages.Should().HaveCount(2);
        subscriber2Messages.Should().HaveCount(2);
        subscriber1Messages.Should().BeEquivalentTo(messages);
        subscriber2Messages.Should().BeEquivalentTo(messages);

        var state = _queue.GetState();
        state.Subscribers.Should().HaveCount(2);
        state.Subscribers[subscriber1].CursorIndex.Should().Be(2);
        state.Subscribers[subscriber2].CursorIndex.Should().Be(2);
    }

    [Fact]
    public async Task SubscriberUnsubscribe_ShouldStopReceivingMessages()
    {
        // Arrange
        var deliveredMessages = new List<string>();
        var messageCount = 0;

        MessageHandler handler = message =>
        {
            deliveredMessages.Add(message.Payload);
            messageCount++;
            
            // Unsubscribe after receiving first message
            if (messageCount == 1)
            {
                _queue.Unsubscribe(_queue.GetState().Subscribers.Keys.First());
            }
            
            return Task.FromResult(DeliveryResult.Ack);
        };

        var subscriberId = _queue.Subscribe(handler);

        // Enqueue multiple messages
        _queue.Enqueue("message1");
        _queue.Enqueue("message2");
        _queue.Enqueue("message3");

        // Act - Wait for processing
        await Task.Delay(1000);

        // Assert
        deliveredMessages.Should().HaveCount(1);
        deliveredMessages[0].Should().Be("message1");

        var state = _queue.GetState();
        state.Subscribers.Should().BeEmpty(); // Should be removed after unsubscribe
    }

    [Fact]
    public async Task RetryMechanism_ShouldRetryFailedMessages()
    {
        // Arrange
        var deliveryAttempts = 0;
        var messagePayload = "retry-test-message";
        _queue.Enqueue(messagePayload);

        MessageHandler handler = message =>
        {
            deliveryAttempts++;
            // Fail first two attempts, succeed on third
            if (deliveryAttempts < 3)
            {
                return Task.FromResult(DeliveryResult.Nack);
            }
            return Task.FromResult(DeliveryResult.Ack);
        };

        var subscriberId = _queue.Subscribe(handler);

        // Act - Wait for retry attempts
        await Task.Delay(2000);

        // Assert
        deliveryAttempts.Should().Be(3); // Should retry 2 times + 1 success

        var state = _queue.GetState();
        state.Subscribers[subscriberId].CursorIndex.Should().Be(1); // Should advance after success
    }

    [Fact]
    public async Task ExceptionHandling_ShouldRetryAndEventuallyFail()
    {
        // Arrange
        var deliveryAttempts = 0;
        var messagePayload = "exception-test-message";
        _queue.Enqueue(messagePayload);

        MessageHandler handler = message =>
        {
            deliveryAttempts++;
            throw new InvalidOperationException($"Attempt {deliveryAttempts}");
        };

        var subscriberId = _queue.Subscribe(handler);

        // Act - Wait for retry attempts
        await Task.Delay(2000);

        // Assert
        deliveryAttempts.Should().Be(3); // Should retry based on RetryCount (2) + 1 initial attempt

        var state = _queue.GetState();
        state.Subscribers[subscriberId].CursorIndex.Should().Be(1); // Should advance after all retries exhausted
    }

    [Fact]
    public async Task MixedSuccessAndFailure_ShouldHandleCorrectly()
    {
        // Arrange
        var messages = new[] { "success1", "failure", "success2" };
        var deliveredMessages = new List<string>();
        var attemptCount = 0;

        foreach (var message in messages)
        {
            _queue.Enqueue(message);
        }

        MessageHandler handler = message =>
        {
            attemptCount++;
            deliveredMessages.Add(message.Payload);
            
            // Fail only the "failure" message
            if (message.Payload == "failure")
            {
                return Task.FromResult(DeliveryResult.Nack);
            }
            
            return Task.FromResult(DeliveryResult.Ack);
        };

        var subscriberId = _queue.Subscribe(handler);

        // Act - Wait for processing
        await Task.Delay(3000);

        // Assert
        deliveredMessages.Should().Contain("success1");
        deliveredMessages.Should().Contain("success2");
        deliveredMessages.Should().Contain("failure");
        
        // Should have multiple attempts for the failure message
        attemptCount.Should().BeGreaterThan(3);

        var state = _queue.GetState();
        state.Subscribers[subscriberId].CursorIndex.Should().Be(3); // Should advance past all messages
    }

    [Fact]
    public async Task QueueState_ShouldReflectCurrentState()
    {
        // Arrange
        var messages = new[] { "message1", "message2", "message3" };
        foreach (var message in messages)
        {
            _queue.Enqueue(message);
        }

        // Act - Get initial state
        var initialState = _queue.GetState();

        // Subscribe and process
        var deliveredMessages = new List<string>();
        MessageHandler handler = message =>
        {
            deliveredMessages.Add(message.Payload);
            return Task.FromResult(DeliveryResult.Ack);
        };

        var subscriberId = _queue.Subscribe(handler);

        // Wait for partial processing
        await Task.Delay(500);
        var partialState = _queue.GetState();

        // Wait for complete processing
        await Task.Delay(1000);
        var finalState = _queue.GetState();

        // Assert
        initialState.Buffer.Should().HaveCount(3);
        initialState.Subscribers.Should().BeEmpty();

        partialState.Buffer.Should().HaveCount(3);
        partialState.Subscribers.Should().HaveCount(1);
        partialState.Subscribers[subscriberId].CursorIndex.Should().BeGreaterThan(0);

        finalState.Buffer.Should().HaveCount(3);
        finalState.Subscribers.Should().HaveCount(1);
        finalState.Subscribers[subscriberId].CursorIndex.Should().Be(3);
        finalState.Subscribers[subscriberId].PendingCount.Should().Be(-1); // 3 messages - (3 + 1) = -1
    }

    [Fact]
    public async Task ConcurrentSubscribers_ShouldWorkCorrectly()
    {
        // Arrange
        var messages = new[] { "message1", "message2", "message3", "message4", "message5" };
        var subscriberResults = new Dictionary<int, List<string>>();
        var subscriberCount = 3;

        // Enqueue messages
        foreach (var message in messages)
        {
            _queue.Enqueue(message);
        }

        // Create multiple subscribers
        var subscriberIds = new List<Guid>();
        for (int i = 0; i < subscriberCount; i++)
        {
            var subscriberIndex = i;
            subscriberResults[subscriberIndex] = new List<string>();

            MessageHandler handler = message =>
            {
                subscriberResults[subscriberIndex].Add(message.Payload);
                return Task.FromResult(DeliveryResult.Ack);
            };

            var subscriberId = _queue.Subscribe(handler);
            subscriberIds.Add(subscriberId);
        }

        // Act - Wait for processing
        await Task.Delay(2000);

        // Assert
        for (int i = 0; i < subscriberCount; i++)
        {
            subscriberResults[i].Should().HaveCount(5);
            subscriberResults[i].Should().BeEquivalentTo(messages);
        }

        var state = _queue.GetState();
        state.Subscribers.Should().HaveCount(3);
        foreach (var subscriberId in subscriberIds)
        {
            state.Subscribers[subscriberId].CursorIndex.Should().Be(5);
            state.Subscribers[subscriberId].PendingCount.Should().Be(-1); // 5 messages - (5 + 1) = -1
        }
    }

    [Fact]
    public async Task ServiceProviderIntegration_ShouldWorkCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SubscribableQueue:Ttl"] = "00:05:00",
                ["SubscribableQueue:RetryCount"] = "1",
                ["SubscribableQueue:DelayBetweenRetriesMs"] = "100"
            })
            .Build();

        services.AddSubscribableQueueWithOptions(configuration);
        using var serviceProvider = services.BuildServiceProvider();

        // Act
        var queue = serviceProvider.GetRequiredService<SubscribableQueue>();
        var options = serviceProvider.GetRequiredService<IOptions<SubscribableQueueOptions>>();

        // Assert
        queue.Should().NotBeNull();
        options.Should().NotBeNull();
        options.Value.Ttl.Should().Be(TimeSpan.FromMinutes(5));
        options.Value.RetryCount.Should().Be(1);
        options.Value.DelayBetweenRetriesMs.Should().Be(100);

        // Test basic functionality
        var messageId = queue.Enqueue("test message");
        messageId.Should().NotBeNullOrEmpty();

        var deliveredMessages = new List<string>();
        MessageHandler handler = message =>
        {
            deliveredMessages.Add(message.Payload);
            return Task.FromResult(DeliveryResult.Ack);
        };

        var subscriberId = queue.Subscribe(handler);
        await Task.Delay(500);

        deliveredMessages.Should().HaveCount(1);
        deliveredMessages[0].Should().Be("test message");
    }
}
