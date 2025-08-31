using System.Timers;
using PushDeliveredQueue.Sample.Dtos;

namespace PushDeliveredQueue.UI.Services;

public class QueueMonitoringService : IDisposable
{
    private readonly QueueApiService _apiService;
    private readonly ILogger<QueueMonitoringService> _logger;
    private readonly System.Timers.Timer _refreshTimer;
    private readonly List<Action<SubscribableQueueStateDto?>> _stateChangeCallbacks = new();

    public SubscribableQueueStateDto? CurrentState { get; private set; }
    public bool IsMonitoring { get; private set; }
    public DateTime LastRefresh { get; private set; }

    public event Action<SubscribableQueueStateDto?>? StateChanged;

    public QueueMonitoringService(QueueApiService apiService, ILogger<QueueMonitoringService> logger)
    {
        _apiService = apiService;
        _logger = logger;
        _refreshTimer = new System.Timers.Timer(2000); // Refresh every 2 seconds
        _refreshTimer.Elapsed += async (sender, e) => await RefreshStateAsync();
    }

    public async Task StartMonitoringAsync()
    {
        if (IsMonitoring) return;

        IsMonitoring = true;
        _refreshTimer.Start();
        await RefreshStateAsync();
        _logger.LogInformation("Queue monitoring started");
    }

    public void StopMonitoring()
    {
        if (!IsMonitoring) return;

        IsMonitoring = false;
        _refreshTimer.Stop();
        _logger.LogInformation("Queue monitoring stopped");
    }

    public async Task RefreshStateAsync()
    {
        try
        {
            var newState = await _apiService.GetQueueStateAsync();
            if (newState != null)
            {
                CurrentState = newState;
                LastRefresh = DateTime.UtcNow;
                StateChanged?.Invoke(newState);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh queue state");
        }
    }

    public void RegisterStateChangeCallback(Action<SubscribableQueueStateDto?> callback)
    {
        _stateChangeCallbacks.Add(callback);
        StateChanged += callback;
    }

    public void UnregisterStateChangeCallback(Action<SubscribableQueueStateDto?> callback)
    {
        _stateChangeCallbacks.Remove(callback);
        StateChanged -= callback;
    }

    public void Dispose()
    {
        StopMonitoring();
        _refreshTimer.Dispose();
        
        // Clean up all callbacks
        foreach (var callback in _stateChangeCallbacks.ToList())
        {
            StateChanged -= callback;
        }
        _stateChangeCallbacks.Clear();
        
        GC.SuppressFinalize(this);
    }
}
