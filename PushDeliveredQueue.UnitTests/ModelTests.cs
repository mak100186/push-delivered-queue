using FluentAssertions;

using Moq;

using PushDeliveredQueue.Core.Abstractions;
using PushDeliveredQueue.Core.Models;

using Xunit;

namespace PushDeliveredQueue.UnitTests;

public class ModelTests
{
    [Fact]
    public void MessageEnvelope_Constructor_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var payload = "test payload";

        // Act
        var envelope = new MessageEnvelope(id, timestamp, payload);

        // Assert
        envelope.Id.Should().Be(id);
        envelope.CreatedAt.Should().Be(timestamp);
        envelope.Payload.Should().Be(payload);
    }

    [Fact]
    public void MessageEnvelope_WithNullPayload_ShouldSetPayloadToNull()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        // Act
        var envelope = new MessageEnvelope(id, timestamp, null);

        // Assert
        envelope.Payload.Should().BeNull();
    }

    [Fact]
    public void DeliveryResult_Values_ShouldBeCorrect()
    {
        // Act & Assert
        DeliveryResult.Ack.Should().Be(DeliveryResult.Ack);
        DeliveryResult.Nack.Should().Be(DeliveryResult.Nack);
        DeliveryResult.Ack.Should().NotBe(DeliveryResult.Nack);
    }

    [Fact]
    public void CursorState_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var cursor = new CursorState()
        {
            Handler = null // Handler is required, setting to null for default test
        };

        // Assert
        cursor.Index.Should().Be(0);
        cursor.IsCommitted.Should().BeFalse();
        cursor.Handler.Should().BeNull();
        cursor.Cancellation.Should().NotBeNull();
    }

    [Fact]
    public void CursorState_WithCustomValues_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var handler = new Mock<IQueueEventHandler>();
        handler.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(DeliveryResult.Ack);
        handler.Setup(h => h.OnMessageFailedHandlerAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(PostMessageFailedBehavior.Commit);

        // Act
        var cursor = new CursorState
        {
            Index = 5,
            IsCommitted = true,
            Handler = handler.Object
        };

        // Assert
        cursor.Index.Should().Be(5);
        cursor.IsCommitted.Should().BeTrue();
        cursor.Handler.Should().Be(handler.Object);
        cursor.Cancellation.Should().NotBeNull();
    }

    [Fact]
    public void SubscriberState_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var state = new SubscriberState();

        // Assert
        state.CursorIndex.Should().Be(0);
        state.IsCommitted.Should().BeFalse();
        state.PendingCount.Should().Be(0);
    }

    [Fact]
    public void SubscriberState_WithCustomValues_ShouldSetPropertiesCorrectly()
    {
        // Act
        var state = new SubscriberState
        {
            CursorIndex = 10,
            IsCommitted = true,
            PendingCount = 25
        };

        // Assert
        state.CursorIndex.Should().Be(10);
        state.IsCommitted.Should().BeTrue();
        state.PendingCount.Should().Be(25);
    }

    [Fact]
    public void SubscribableQueueState_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var state = new SubscribableQueueState();

        // Assert
        state.Buffer.Should().NotBeNull();
        state.Buffer.Should().BeEmpty();
        state.Subscribers.Should().NotBeNull();
        state.Subscribers.Should().BeEmpty();
    }

    [Fact]
    public void SubscribableQueueState_WithCustomValues_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var buffer = new List<MessageEnvelope>
        {
            new(Guid.NewGuid(), DateTime.UtcNow, "message1"),
            new(Guid.NewGuid(), DateTime.UtcNow, "message2")
        };
        var subscribers = new Dictionary<Guid, SubscriberState>
        {
            { Guid.NewGuid(), new() { CursorIndex = 1, IsCommitted = true, PendingCount = 5 } }
        };

        // Act
        var state = new SubscribableQueueState
        {
            Buffer = buffer,
            Subscribers = subscribers
        };

        // Assert
        state.Buffer.Should().BeEquivalentTo(buffer);
        state.Subscribers.Should().BeEquivalentTo(subscribers);
    }

    [Fact]
    public async Task MessageHandler_Delegate_ShouldBeCallable()
    {
        // Arrange
        var envelope = new MessageEnvelope(Guid.NewGuid(), DateTime.UtcNow, "test");
        var handler = new Mock<IQueueEventHandler>();
        handler.Setup(h =>
            h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<MessageEnvelope, Guid, CancellationToken>((msg, id, ct) => msg.Should().Be(envelope))
            .ReturnsAsync(DeliveryResult.Ack);
        handler.Setup(h => h.OnMessageFailedHandlerAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(PostMessageFailedBehavior.Commit);

        // Act
        var result = await handler.Object.OnMessageReceiveAsync(envelope, Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().Be(DeliveryResult.Ack);
    }

    [Fact]
    public async Task MessageHandler_WithNackResult_ShouldReturnNack()
    {
        // Arrange
        var envelope = new MessageEnvelope(Guid.NewGuid(), DateTime.UtcNow, "test");
        var handler = new Mock<IQueueEventHandler>();
        handler.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(DeliveryResult.Nack);
        handler.Setup(h => h.OnMessageFailedHandlerAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(PostMessageFailedBehavior.Commit);

        // Act
        var result = await handler.Object.OnMessageReceiveAsync(envelope, Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().Be(DeliveryResult.Nack);
    }

    [Fact]
    public async Task MessageHandler_WithException_ShouldThrowException()
    {
        // Arrange
        var envelope = new MessageEnvelope(Guid.NewGuid(), DateTime.UtcNow, "test");
        var handler = new Mock<IQueueEventHandler>();
        handler.Setup(h => h.OnMessageReceiveAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
        .Callback<MessageEnvelope, Guid, CancellationToken>((msg, id, ct) => throw new InvalidOperationException("Test exception"))
        .ReturnsAsync(DeliveryResult.Ack);
        handler.Setup(h => h.OnMessageFailedHandlerAsync(It.IsAny<MessageEnvelope>(), It.IsAny<Guid>(), It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(PostMessageFailedBehavior.Commit);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await handler.Object.OnMessageReceiveAsync(envelope, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public void MessageEnvelope_Equality_ShouldWorkCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var payload = "test payload";

        var envelope1 = new MessageEnvelope(id, timestamp, payload);
        var envelope2 = new MessageEnvelope(id, timestamp, payload);
        var envelope3 = new MessageEnvelope(Guid.NewGuid(), timestamp, payload);

        // Act & Assert
        envelope1.Should().BeEquivalentTo(envelope2);
        envelope1.Should().NotBeEquivalentTo(envelope3);
    }

    [Fact]
    public void SubscriberState_Equality_ShouldWorkCorrectly()
    {
        // Arrange
        var state1 = new SubscriberState
        {
            CursorIndex = 5,
            IsCommitted = true,
            PendingCount = 10
        };

        var state2 = new SubscriberState
        {
            CursorIndex = 5,
            IsCommitted = true,
            PendingCount = 10
        };

        var state3 = new SubscriberState
        {
            CursorIndex = 6,
            IsCommitted = true,
            PendingCount = 10
        };

        // Act & Assert
        state1.Should().BeEquivalentTo(state2);
        state1.Should().NotBeEquivalentTo(state3);
    }

    [Fact]
    public void CursorState_Cancellation_ShouldBeUsable()
    {
        // Arrange
        var cursor = new CursorState() { Handler = null };

        // Act & Assert
        cursor.Cancellation.Should().NotBeNull();
        cursor.Cancellation.Token.IsCancellationRequested.Should().BeFalse();

        cursor.Cancellation.Cancel();
        cursor.Cancellation.Token.IsCancellationRequested.Should().BeTrue();
    }
}
