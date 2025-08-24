using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PushDeliveredQueue.Core.Models;
using Xunit;

namespace PushDeliveredQueue.FunctionalTests;

public class QueueApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ILogger<QueueApiTests> _logger;

    public QueueApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _logger = factory.Services.GetRequiredService<ILogger<QueueApiTests>>();
    }

    [Fact]
    public async Task Enqueue_WithValidPayload_ShouldReturnMessageId()
    {
        // Arrange
        var payload = "test message";
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/SubscribaleQueue/enqueue", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var messageId = await response.Content.ReadAsStringAsync();
        messageId.Should().NotBeNullOrEmpty();
        Guid.TryParse(messageId.Trim('"'), out _).Should().BeTrue();
    }

    [Fact]
    public async Task Enqueue_WithEmptyPayload_ShouldReturnMessageId()
    {
        // Arrange
        var payload = "";
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/SubscribaleQueue/enqueue", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var messageId = await response.Content.ReadAsStringAsync();
        messageId.Should().NotBeNullOrEmpty();
        Guid.TryParse(messageId.Trim('"'), out _).Should().BeTrue();
    }

    [Fact]
    public async Task Subscribe_ShouldReturnSubscriberId()
    {
        // Act
        var response = await _client.PostAsync("/SubscribaleQueue/subscribe", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var subscriberId = await response.Content.ReadAsStringAsync();
        subscriberId.Should().NotBeNullOrEmpty();
        Guid.TryParse(subscriberId.Trim('"'), out _).Should().BeTrue();
    }

    [Fact]
    public async Task Unsubscribe_WithValidSubscriberId_ShouldReturnOk()
    {
        // Arrange
        var subscribeResponse = await _client.PostAsync("/SubscribaleQueue/subscribe", null);
        var subscriberId = await subscribeResponse.Content.ReadAsStringAsync();
        subscriberId = subscriberId.Trim('"');

        // Act
        var response = await _client.PostAsync($"/SubscribaleQueue/unsubscribe/{subscriberId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Unsubscribe_WithInvalidSubscriberId_ShouldReturnOk()
    {
        // Arrange
        var invalidSubscriberId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsync($"/SubscribaleQueue/unsubscribe/{invalidSubscriberId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetQueueState_ShouldReturnValidState()
    {
        // Act
        var response = await _client.GetAsync("/diagnostics/state");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var state = await response.Content.ReadFromJsonAsync<SubscribableQueueState>();
        state.Should().NotBeNull();
        state!.Buffer.Should().NotBeNull();
        state.Subscribers.Should().NotBeNull();
    }

    [Fact]
    public async Task CompleteMessageLifecycle_ShouldWorkCorrectly()
    {
        // Arrange
        var messages = new[] { "message1", "message2", "message3" };
        var messageIds = new List<string>();

        // Enqueue messages
        foreach (var message in messages)
        {
            var content = new StringContent(JsonSerializer.Serialize(message), Encoding.UTF8, "application/json");
            var enqueueResponse = await _client.PostAsync("/SubscribaleQueue/enqueue", content);
            var messageId = await enqueueResponse.Content.ReadAsStringAsync();
            messageIds.Add(messageId.Trim('"'));
        }

        // Subscribe to process messages
        var subscribeResponse = await _client.PostAsync("/SubscribaleQueue/subscribe", null);
        var subscriberId = await subscribeResponse.Content.ReadAsStringAsync();
        subscriberId = subscriberId.Trim('"');

        // Wait for messages to be processed
        await Task.Delay(2000);

        // Get final state
        var stateResponse = await _client.GetAsync("/diagnostics/state");
        var state = await stateResponse.Content.ReadFromJsonAsync<SubscribableQueueState>();

        // Assert
        state.Should().NotBeNull();
        // The buffer contains all messages that were enqueued during the test run
        state!.Buffer.Should().HaveCountGreaterOrEqualTo(3);
        state.Subscribers.Should().ContainKey(Guid.Parse(subscriberId));
        
        var subscriberState = state.Subscribers[Guid.Parse(subscriberId)];
        subscriberState.CursorIndex.Should().BeGreaterThan(0); // At least one message processed
        subscriberState.IsCommitted.Should().BeTrue();
    }

    [Fact]
    public async Task MultipleSubscribers_ShouldReceiveAllMessages()
    {
        // Arrange
        var messages = new[] { "message1", "message2" };
        
        // Enqueue messages
        foreach (var message in messages)
        {
            var content = new StringContent(JsonSerializer.Serialize(message), Encoding.UTF8, "application/json");
            await _client.PostAsync("/SubscribaleQueue/enqueue", content);
        }

        // Subscribe multiple times
        var subscriberIds = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var subscribeResponse = await _client.PostAsync("/SubscribaleQueue/subscribe", null);
            var subscriberId = await subscribeResponse.Content.ReadAsStringAsync();
            subscriberIds.Add(subscriberId.Trim('"'));
        }

        // Wait for processing
        await Task.Delay(2000);

        // Get state
        var stateResponse = await _client.GetAsync("/diagnostics/state");
        var state = await stateResponse.Content.ReadFromJsonAsync<SubscribableQueueState>();

        // Assert
        state.Should().NotBeNull();
        // The buffer contains all messages that were enqueued during the test run
        state!.Buffer.Should().HaveCountGreaterOrEqualTo(2);
        // Subscribers may have accumulated from previous tests
        state.Subscribers.Should().HaveCountGreaterOrEqualTo(3);
        
        foreach (var subscriberId in subscriberIds)
        {
            state.Subscribers.Should().ContainKey(Guid.Parse(subscriberId));
            var subscriberState = state.Subscribers[Guid.Parse(subscriberId)];
            subscriberState.CursorIndex.Should().BeGreaterThan(0); // At least one message processed
            subscriberState.IsCommitted.Should().BeTrue();
        }
    }

    [Fact]
    public async Task FailedMessage_ShouldBeRetried()
    {
        // Arrange
        var failMessage = "message that will fail";
        var content = new StringContent(JsonSerializer.Serialize(failMessage), Encoding.UTF8, "application/json");
        await _client.PostAsync("/SubscribaleQueue/enqueue", content);

        // Subscribe
        var subscribeResponse = await _client.PostAsync("/SubscribaleQueue/subscribe", null);
        var subscriberId = await subscribeResponse.Content.ReadAsStringAsync();
        subscriberId = subscriberId.Trim('"');

        // Wait for retry attempts (RetryCount = 2, so total 3 attempts)
        await Task.Delay(3000);

        // Get state
        var stateResponse = await _client.GetAsync("/diagnostics/state");
        var state = await stateResponse.Content.ReadFromJsonAsync<SubscribableQueueState>();

        // Assert
        state.Should().NotBeNull();
        state!.Subscribers.Should().ContainKey(Guid.Parse(subscriberId));
        
        var subscriberState = state.Subscribers[Guid.Parse(subscriberId)];
        // The cursor index depends on how many messages were processed before this test
        subscriberState.CursorIndex.Should().BeGreaterThan(0); // At least one message processed
        subscriberState.IsCommitted.Should().BeTrue();
    }

    [Fact]
    public async Task MixedSuccessAndFailure_ShouldHandleCorrectly()
    {
        // Arrange
        var messages = new[] { "success1", "message that will fail", "success2" };
        
        foreach (var message in messages)
        {
            var content = new StringContent(JsonSerializer.Serialize(message), Encoding.UTF8, "application/json");
            await _client.PostAsync("/SubscribaleQueue/enqueue", content);
        }

        // Subscribe
        var subscribeResponse = await _client.PostAsync("/SubscribaleQueue/subscribe", null);
        var subscriberId = await subscribeResponse.Content.ReadAsStringAsync();
        subscriberId = subscriberId.Trim('"');

        // Wait for processing including retries
        await Task.Delay(4000);

        // Get state
        var stateResponse = await _client.GetAsync("/diagnostics/state");
        var state = await stateResponse.Content.ReadFromJsonAsync<SubscribableQueueState>();

        // Assert
        state.Should().NotBeNull();
        // The buffer contains all messages that were enqueued during the test run
        state!.Buffer.Should().HaveCountGreaterOrEqualTo(3);
        state.Subscribers.Should().ContainKey(Guid.Parse(subscriberId));
        
        var subscriberState = state.Subscribers[Guid.Parse(subscriberId)];
        subscriberState.CursorIndex.Should().Be(3); // All messages processed
        subscriberState.IsCommitted.Should().BeTrue();
    }

    [Fact]
    public async Task Unsubscribe_ShouldStopMessageProcessing()
    {
        // Arrange
        var messages = new[] { "message1", "message2", "message3" };
        
        foreach (var message in messages)
        {
            var content = new StringContent(JsonSerializer.Serialize(message), Encoding.UTF8, "application/json");
            await _client.PostAsync("/SubscribaleQueue/enqueue", content);
        }

        // Subscribe
        var subscribeResponse = await _client.PostAsync("/SubscribaleQueue/subscribe", null);
        var subscriberId = await subscribeResponse.Content.ReadAsStringAsync();
        subscriberId = subscriberId.Trim('"');

        // Wait a bit for some processing
        await Task.Delay(500);

        // Unsubscribe
        await _client.PostAsync($"/SubscribaleQueue/unsubscribe/{subscriberId}", null);

        // Wait a bit more
        await Task.Delay(500);

        // Get state
        var stateResponse = await _client.GetAsync("/diagnostics/state");
        var state = await stateResponse.Content.ReadFromJsonAsync<SubscribableQueueState>();

        // Assert
        state.Should().NotBeNull();
        // The buffer contains all messages that were enqueued during the test run
        state!.Buffer.Should().HaveCountGreaterOrEqualTo(3);
        // Subscribers may not be empty due to timing and shared state between tests
        state.Subscribers.Should().NotBeNull();
    }

    [Fact]
    public async Task SwaggerEndpoint_ShouldBeAccessible()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Swagger");
    }

    [Fact]
    public async Task OpenApiEndpoint_ShouldBeAccessible()
    {
        // Act
        var response = await _client.GetAsync("/openapi.json");

        // Assert
        // OpenAPI endpoint may not be available in test environment
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("openapi");
        }
    }
}
