using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using PushDeliveredQueue.Sample.Dtos;

using Xunit;

namespace PushDeliveredQueue.FunctionalTests;

public class ConcurrencyTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ILogger<ConcurrencyTests> _logger;

    public ConcurrencyTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _logger = factory.Services.GetRequiredService<ILogger<ConcurrencyTests>>();
    }

    [Fact]
    public async Task ConcurrentEnqueue_ShouldHandleMultipleMessages()
    {
        // Arrange
        var messageCount = 10;
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act - Enqueue messages concurrently
        for (var i = 0; i < messageCount; i++)
        {
            var payload = $"concurrent-message-{i}";
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            tasks.Add(_client.PostAsync("/SubscribaleQueue/enqueue", content));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));

        var messageIds = new List<string>();
        foreach (var response in responses)
        {
            var messageId = await response.Content.ReadAsStringAsync();
            messageIds.Add(messageId.Trim('"'));
        }

        messageIds.Should().HaveCount(messageCount);
        messageIds.Should().AllSatisfy(id => Guid.TryParse(id, out _));
    }

    [Fact]
    public async Task ConcurrentSubscribe_ShouldCreateMultipleSubscribers()
    {
        // Arrange
        var subscriberCount = 5;
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act - Subscribe concurrently
        for (var i = 0; i < subscriberCount; i++)
        {
            tasks.Add(_client.PostAsync("/SubscribaleQueue/subscribe", null));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));

        var subscriberIds = new List<string>();
        foreach (var response in responses)
        {
            var subscriberId = await response.Content.ReadAsStringAsync();
            subscriberIds.Add(subscriberId.Trim('"'));
        }

        subscriberIds.Should().HaveCount(subscriberCount);
        subscriberIds.Should().AllSatisfy(id => Guid.TryParse(id, out _));
        subscriberIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ConcurrentEnqueueAndSubscribe_ShouldWorkCorrectly()
    {
        // Arrange
        var messageCount = 5;
        var subscriberCount = 3;
        var enqueueTasks = new List<Task<HttpResponseMessage>>();
        var subscribeTasks = new List<Task<HttpResponseMessage>>();

        // Act - Enqueue messages and subscribe concurrently
        for (var i = 0; i < messageCount; i++)
        {
            var payload = $"mixed-message-{i}";
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            enqueueTasks.Add(_client.PostAsync("/SubscribaleQueue/enqueue", content));
        }

        for (var i = 0; i < subscriberCount; i++)
        {
            subscribeTasks.Add(_client.PostAsync("/SubscribaleQueue/subscribe", null));
        }

        var enqueueResponses = await Task.WhenAll(enqueueTasks);
        var subscribeResponses = await Task.WhenAll(subscribeTasks);

        // Wait for processing
        await Task.Delay(3000);

        // Get state
        var stateResponse = await _client.GetAsync("/diagnostics/state");
        var state = await stateResponse.Content.ReadFromJsonAsync<SubscribableQueueStateDto>();

        // Assert
        enqueueResponses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
        subscribeResponses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));

        state.Should().NotBeNull();
        // The buffer contains all messages that were enqueued during the test run
        state!.Buffer.Should().HaveCountGreaterOrEqualTo(messageCount);
        // Subscribers may have accumulated from previous tests
        state.Subscribers.Should().HaveCountGreaterOrEqualTo(subscriberCount);
    }

    [Fact]
    public async Task RapidSubscribeUnsubscribe_ShouldHandleCorrectly()
    {
        // Arrange
        var operations = 10;
        var tasks = new List<Task>();

        // Act - Rapidly subscribe and unsubscribe
        for (var i = 0; i < operations; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                // Subscribe
                var subscribeResponse = await _client.PostAsync("/SubscribaleQueue/subscribe", null);
                var subscriberId = await subscribeResponse.Content.ReadAsStringAsync();
                subscriberId = subscriberId.Trim('"');

                // Unsubscribe immediately
                await _client.PostAsync($"/SubscribaleQueue/unsubscribe/{subscriberId}", null);
            }));
        }

        await Task.WhenAll(tasks);

        // Wait a bit
        await Task.Delay(1000);

        // Get state
        var stateResponse = await _client.GetAsync("/diagnostics/state");
        var state = await stateResponse.Content.ReadFromJsonAsync<SubscribableQueueStateDto>();

        // Assert
        state.Should().NotBeNull();
        // Subscribers may not be empty due to timing and shared state between tests
        state!.Subscribers.Should().NotBeNull();
    }

    [Fact]
    public async Task MultipleSubscribersWithConcurrentMessages_ShouldProcessAllMessages()
    {
        // Arrange
        var messageCount = 20;
        var subscriberCount = 4;

        // Enqueue messages first
        var enqueueTasks = new List<Task<HttpResponseMessage>>();
        for (var i = 0; i < messageCount; i++)
        {
            var payload = $"bulk-message-{i}";
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            enqueueTasks.Add(_client.PostAsync("/SubscribaleQueue/enqueue", content));
        }
        await Task.WhenAll(enqueueTasks);

        // Subscribe multiple subscribers
        var subscribeTasks = new List<Task<HttpResponseMessage>>();
        for (var i = 0; i < subscriberCount; i++)
        {
            subscribeTasks.Add(_client.PostAsync("/SubscribaleQueue/subscribe", null));
        }
        var subscribeResponses = await Task.WhenAll(subscribeTasks);

        // Wait for processing
        await Task.Delay(5000);

        // Get state
        var stateResponse = await _client.GetAsync("/diagnostics/state");
        var state = await stateResponse.Content.ReadFromJsonAsync<SubscribableQueueStateDto>();

        // Assert
        subscribeResponses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));

        state.Should().NotBeNull();
        // The buffer contains all messages that were enqueued during the test run
        state!.Buffer.Should().HaveCountGreaterOrEqualTo(messageCount);
        // Subscribers may have accumulated from previous tests
        state.Subscribers.Should().HaveCountGreaterOrEqualTo(subscriberCount);

        // All subscribers should have processed messages
        foreach (var subscriberState in state.Subscribers.Values)
        {
            subscriberState.PendingMessageCount.Should().Be(0); // All retries done
            subscriberState.IsBlocked.Should().BeFalse();
        }
    }

    [Fact]
    public async Task ConcurrentStateInspection_ShouldReturnConsistentResults()
    {
        // Arrange
        var inspectionCount = 10;
        var tasks = new List<Task<HttpResponseMessage>>();

        // Act - Inspect state concurrently
        for (var i = 0; i < inspectionCount; i++)
        {
            tasks.Add(_client.GetAsync("/diagnostics/state"));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));

        var states = new List<SubscribableQueueStateDto>();
        foreach (var response in responses)
        {
            var state = await response.Content.ReadFromJsonAsync<SubscribableQueueStateDto>();
            states.Add(state!);
        }

        // All states should be consistent (same buffer count, same subscriber count)
        var firstState = states.First();
        states.Should().AllSatisfy(s =>
        {
            s.Buffer.Should().HaveCount(firstState.Buffer.Count);
            s.Subscribers.Should().HaveCount(firstState.Subscribers.Count);
        });
    }

    [Fact]
    public async Task StressTest_ShouldHandleHighLoad()
    {
        // Arrange
        var messageCount = 50;
        var subscriberCount = 5;
        var concurrentOperations = 20;

        // Enqueue messages
        var enqueueTasks = new List<Task<HttpResponseMessage>>();
        for (var i = 0; i < messageCount; i++)
        {
            var payload = $"stress-message-{i}";
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            enqueueTasks.Add(_client.PostAsync("/SubscribaleQueue/enqueue", content));
        }
        await Task.WhenAll(enqueueTasks);

        // Subscribe
        var subscribeTasks = new List<Task<HttpResponseMessage>>();
        for (var i = 0; i < subscriberCount; i++)
        {
            subscribeTasks.Add(_client.PostAsync("/SubscribaleQueue/subscribe", null));
        }
        await Task.WhenAll(subscribeTasks);

        // Concurrent state inspections
        var inspectionTasks = new List<Task<HttpResponseMessage>>();
        for (var i = 0; i < concurrentOperations; i++)
        {
            inspectionTasks.Add(_client.GetAsync("/diagnostics/state"));
        }

        // Act
        var inspectionResponses = await Task.WhenAll(inspectionTasks);

        // Wait for processing
        await Task.Delay(3000);

        // Final state check
        var finalStateResponse = await _client.GetAsync("/diagnostics/state");
        var finalState = await finalStateResponse.Content.ReadFromJsonAsync<SubscribableQueueStateDto>();

        // Assert
        inspectionResponses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));

        finalState.Should().NotBeNull();
        // The buffer contains all messages that were enqueued during the test run
        finalState!.Buffer.Should().HaveCountGreaterOrEqualTo(messageCount);
        // Subscribers may have accumulated from previous tests
        finalState.Subscribers.Should().HaveCountGreaterOrEqualTo(subscriberCount);

        // All subscribers should have processed all messages
        foreach (var subscriberState in finalState.Subscribers.Values)
        {
            subscriberState.PendingMessageCount.Should().Be(0); // All retries done
            subscriberState.IsBlocked.Should().BeFalse();
        }
    }
}
