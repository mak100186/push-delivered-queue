using PushDeliveredQueue.API.Dtos;

namespace PushDeliveredQueue.UI.Services;

public class QueueMonitoringService : IDisposable
{
    private readonly QueueApiService _apiService;
    private readonly ILogger<QueueMonitoringService> _logger;
    private readonly Timer _refreshTimer;

    public SubscribableQueueStateDto? CurrentState { get; private set; }
    public bool IsMonitoring { get; private set; }
    public DateTime LastRefresh { get; private set; }

    public event Action<SubscribableQueueStateDto?>? StateChanged;

    public QueueMonitoringService(QueueApiService apiService, ILogger<QueueMonitoringService> logger)
    {
        _apiService = apiService;
        _logger = logger;
        _refreshTimer = new Timer(async _ => await RefreshStateAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(2)); // Refresh every 2 seconds
    }

    public async Task StartMonitoringAsync()
    {
        if (IsMonitoring)
        {
            return;
        }

        IsMonitoring = true;
        await RefreshStateAsync();
        _logger.LogInformation("Queue monitoring started");
    }

    public void StopMonitoring()
    {
        if (!IsMonitoring)
        {
            return;
        }

        IsMonitoring = false;
        _logger.LogInformation("Queue monitoring stopped");
    }

    public async Task RefreshStateAsync()
    {
        if (!IsMonitoring)
        {
            return;
        }

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
    
    public void Dispose()
    {
        StopMonitoring();
        _refreshTimer.Dispose();
        
        GC.SuppressFinalize(this);
    }
}
