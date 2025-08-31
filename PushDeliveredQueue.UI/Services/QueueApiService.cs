using PushDeliveredQueue.API.Dtos;

namespace PushDeliveredQueue.UI.Services;

public class QueueApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<QueueApiService> _logger;

    public QueueApiService(HttpClient httpClient, ILogger<QueueApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SubscribableQueueStateDto?> GetQueueStateAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<SubscribableQueueStateDto>("diagnostics/state");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get queue state");
            return null;
        }
    }

    public async Task<Guid?> EnqueueMessageAsync(string payload)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("SubscribaleQueue/enqueueSingle", payload);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<Guid>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue message");
        }
        return null;
    }

    public async Task<List<Guid>?> EnqueueMultipleMessagesAsync(int count)
    {
        try
        {
            var response = await _httpClient.PostAsync($"SubscribaleQueue/enqueueMultiple?count={count}", null);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<Guid>>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue multiple messages");
        }
        return null;
    }

    public async Task<Guid?> SubscribeAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("SubscribaleQueue/subscribe", null);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<Guid>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe");
        }
        return null;
    }

    public async Task<bool> UnsubscribeAsync(Guid subscriberId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"SubscribaleQueue/unsubscribe/{subscriberId}", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe");
            return false;
        }
    }

    public async Task<bool> ChangeMessagePayloadAsync(Guid messageId, string newPayload)
    {
        try
        {
            var response = await _httpClient.PostAsync($"diagnostics/changePayload?messageId={messageId}&payload={Uri.EscapeDataString(newPayload)}", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to change message payload");
            return false;
        }
    }

    public async Task<bool> ReplayFromAsync(Guid subscriberId, Guid messageId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"ReplayQueue/replayFrom?subscriberId={subscriberId}&messageId={messageId}", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to replay from message");
            return false;
        }
    }

    public async Task<bool> ReplayAllDlqAsync(Guid subscriberId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"ReplayQueue/replayAllDlq?subscriberId={subscriberId}", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to replay all DLQ messages");
            return false;
        }
    }

    public async Task<bool> ReplayAllDlqSubscribersAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("ReplayQueue/replayAllSubscribers", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to replay all DLQ subscribers");
            return false;
        }
    }
}
