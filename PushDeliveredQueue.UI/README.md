# PushDeliveredQueue UI

A Blazor Server web application for monitoring and managing the PushDeliveredQueue system.

## Features

- **Real-time Queue Monitoring**: Live updates of queue state, subscribers, and message processing
- **Message Management**: Enqueue messages, view buffer contents, and modify message payloads
- **Subscriber Management**: Subscribe/unsubscribe to the queue and monitor subscriber status
- **Dead Letter Queue (DLQ) Management**: View and replay failed messages
- **Interactive Dashboard**: Modern, responsive UI with real-time statistics

## Prerequisites

- .NET 9.0 SDK
- The PushDeliveredQueue.Sample API must be running on `https://localhost:7246`

## Getting Started

1. **Start the Sample API**:
   ```bash
   cd PushDeliveredQueue.Sample
   dotnet run
   ```

2. **Start the UI**:
   ```bash
   cd PushDeliveredQueue.UI
   dotnet run
   ```

3. **Access the UI**: Navigate to `https://localhost:7274` (or the port shown in the console)

## Usage

### Queue Monitor Dashboard

The main dashboard (`/queue-monitor`) provides:

- **Monitoring Controls**: Start/stop real-time monitoring and manual refresh
- **Quick Actions**: Subscribe to the queue, replay DLQ messages, and enqueue new messages
- **Statistics Cards**: Real-time counts of buffer messages, subscribers, DLQ messages, and pending messages
- **Message Buffer**: View all messages in the queue with options to edit payloads
- **Subscribers**: Monitor active subscribers, their status, and manage subscriptions

### Key Features

1. **Real-time Updates**: The UI automatically refreshes every 2 seconds when monitoring is active
2. **Message Operations**: 
   - Enqueue single messages or bulk messages
   - Edit message payloads inline
   - View message expiration times
3. **Subscriber Management**:
   - Subscribe to the queue
   - Unsubscribe active subscribers
   - Monitor subscriber status (Active/Blocked)
   - Replay DLQ messages for specific subscribers
4. **DLQ Management**:
   - View failed messages in each subscriber's DLQ
   - Replay all DLQ messages for a subscriber
   - Replay all DLQ messages across all subscribers

## Configuration

The UI connects to the sample API using the configuration in `appsettings.json`:

```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:7246/"
  }
}
```

## Architecture

- **Blazor Server**: Real-time UI with server-side rendering
- **HTTP Client**: Communicates with the queue API
- **Monitoring Service**: Manages real-time updates and state synchronization
- **Modal Components**: Interactive dialogs for editing and confirmation

## Development

The UI project includes:

- `QueueApiService`: HTTP client wrapper for API communication
- `QueueMonitoringService`: Real-time monitoring with timer-based updates
- `QueueMonitor.razor`: Main dashboard component
- `MessageEditModal.razor`: Modal for editing message payloads

## Troubleshooting

- **Connection Issues**: Ensure the sample API is running on the correct port
- **No Data**: Check that the queue has messages and subscribers
- **Real-time Updates**: Verify monitoring is started and the API is accessible
