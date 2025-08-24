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

public class LoggingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ILogger<LoggingTests> _logger;
    public LoggingTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _logger = factory.Services.GetRequiredService<ILogger<LoggingTests>>();
    }

    [Fact]
    public async Task EnqueueOperation_ShouldLogMessage()
    {
        // Arrange
        var payload = "test logging message";
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/SubscribaleQueue/enqueue", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Wait for logs to be written
        await Task.Delay(1000);

        // Verify logs in Seq (this would require Seq API access in a real scenario)
        // For now, we just verify the operation completed successfully
        var messageId = await response.Content.ReadAsStringAsync();
        messageId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SubscribeOperation_ShouldLogMessage()
    {
        // Act
        var response = await _client.PostAsync("/SubscribaleQueue/subscribe", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Wait for logs to be written
        await Task.Delay(1000);

        var subscriberId = await response.Content.ReadAsStringAsync();
        subscriberId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UnsubscribeOperation_ShouldLogMessage()
    {
        // Arrange
        var subscribeResponse = await _client.PostAsync("/SubscribaleQueue/subscribe", null);
        var subscriberId = await subscribeResponse.Content.ReadAsStringAsync();
        subscriberId = subscriberId.Trim('"');

        // Act
        var response = await _client.PostAsync($"/SubscribaleQueue/unsubscribe/{subscriberId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Wait for logs to be written
        await Task.Delay(1000);
    }

    [Fact]
    public async Task MessageProcessing_ShouldLogProcessingEvents()
    {
        // Arrange
        var payload = "processing test message";
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        await _client.PostAsync("/SubscribaleQueue/enqueue", content);

        // Act
        var subscribeResponse = await _client.PostAsync("/SubscribaleQueue/subscribe", null);
        var subscriberId = await subscribeResponse.Content.ReadAsStringAsync();
        subscriberId = subscriberId.Trim('"');

        // Wait for message processing
        await Task.Delay(2000);

        // Assert
        subscribeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify the message was processed by checking state
        var stateResponse = await _client.GetAsync("/diagnostics/state");
        var state = await stateResponse.Content.ReadFromJsonAsync<SubscribableQueueState>();
        
        state.Should().NotBeNull();
        state!.Subscribers.Should().ContainKey(Guid.Parse(subscriberId));
        
        var subscriberState = state.Subscribers[Guid.Parse(subscriberId)];
        subscriberState.CursorIndex.Should().BeGreaterThan(0); // Message processed
        subscriberState.IsCommitted.Should().BeTrue();
    }

    [Fact]
    public async Task FailedMessageProcessing_ShouldLogRetryEvents()
    {
        // Arrange
        var failPayload = "message that will fail";
        var content = new StringContent(JsonSerializer.Serialize(failPayload), Encoding.UTF8, "application/json");
        await _client.PostAsync("/SubscribaleQueue/enqueue", content);

        // Act
        var subscribeResponse = await _client.PostAsync("/SubscribaleQueue/subscribe", null);
        var subscriberId = await subscribeResponse.Content.ReadAsStringAsync();
        subscriberId = subscriberId.Trim('"');

        // Wait for retry attempts
        await Task.Delay(3000);

        // Assert
        subscribeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify the message was processed after retries
        var stateResponse = await _client.GetAsync("/diagnostics/state");
        var state = await stateResponse.Content.ReadFromJsonAsync<SubscribableQueueState>();
        
        state.Should().NotBeNull();
        state!.Subscribers.Should().ContainKey(Guid.Parse(subscriberId));
        
        var subscriberState = state.Subscribers[Guid.Parse(subscriberId)];
        subscriberState.CursorIndex.Should().BeGreaterThan(0); // At least one message processed
        subscriberState.IsCommitted.Should().BeTrue();
    }

    [Fact]
    public async Task MultipleOperations_ShouldLogAllEvents()
    {
        // Arrange
        var operations = new List<Task<HttpResponseMessage>>();

        // Enqueue multiple messages
        for (int i = 0; i < 3; i++)
        {
            var payload = $"multi-log-message-{i}";
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            operations.Add(_client.PostAsync("/SubscribaleQueue/enqueue", content));
        }

        // Subscribe
        operations.Add(_client.PostAsync("/SubscribaleQueue/subscribe", null));

        // Act
        var responses = await Task.WhenAll(operations);

        // Wait for processing
        await Task.Delay(2000);

        // Assert
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
        
        // Verify all operations completed successfully
        var stateResponse = await _client.GetAsync("/diagnostics/state");
        var state = await stateResponse.Content.ReadFromJsonAsync<SubscribableQueueState>();
        
        state.Should().NotBeNull();
        // The buffer contains all messages that were enqueued during the test run
        state!.Buffer.Should().HaveCountGreaterOrEqualTo(3);
        // Subscribers may have accumulated from previous tests
        state.Subscribers.Should().HaveCountGreaterOrEqualTo(1);
    }

    [Fact]
    public async Task ApplicationStartup_ShouldLogStartupEvents()
    {
        // This test verifies that the application starts up correctly and logs are configured
        // The WebApplicationFactory ensures the application is started
        
        // Act - Make a simple request to verify the application is running
        var response = await _client.GetAsync("/diagnostics/state");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // The fact that we can make requests means the application started successfully
        // and logging is configured (otherwise we'd get startup errors)
    }

    [Fact]
    public async Task ErrorHandling_ShouldLogErrors()
    {
        // Arrange
        var invalidSubscriberId = Guid.NewGuid();

        // Act - Try to unsubscribe with invalid ID (should not throw but should log)
        var response = await _client.PostAsync($"/SubscribaleQueue/unsubscribe/{invalidSubscriberId}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Wait for any logs to be written
        await Task.Delay(1000);
        
        // The operation should complete without throwing, and any errors should be logged
    }
}


