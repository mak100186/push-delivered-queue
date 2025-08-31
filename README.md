# PushDeliveredQueue

A reactive push-based message queue system built for .NET 9.0, designed for real-time systems, event-driven architectures, and notification pipelines. This library provides a lightweight, in-memory message queue with automatic retry mechanisms, subscriber management, and configurable message TTL.

## 🚀 Features

- **Push-Based Delivery**: Messages are actively pushed to subscribers rather than pulled
- **Multiple Subscribers**: Support for multiple concurrent subscribers with independent cursors
- **Dead Letter Queue (DLQ)**: Automatic handling of failed messages with configurable failure behaviors
- **Message Replay**: Replay messages from DLQ or specific buffer positions
- **Message Payload Modification**: Change message payloads at runtime
- **Automatic Retry**: Configurable retry policies with exponential backoff using Polly
- **Message TTL**: Automatic cleanup of expired messages to prevent memory leaks
- **Ack/Nack Support**: Explicit message acknowledgment for reliable delivery
- **ASP.NET Core Integration**: Seamless integration with dependency injection
- **Blazor UI**: Web-based management interface for queue operations
- **Comprehensive Testing**: Full unit, functional, and UI test coverage
- **Real-time Processing**: Low-latency message delivery with background processing

## 📋 Table of Contents

- [Architecture](#architecture)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [API Reference](#api-reference)
- [Examples](#examples)
- [Testing](#testing)
- [Contributing](#contributing)

## 🏗️ Architecture

### Core Components

```
┌─────────────────────────────────────────────────────────────┐
│                    SubscribableQueue                        │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐          │
│  │ Subscriber1 │  │ Subscriber2 │  │ SubscriberN │          │
│  │ (Cursor)    │  │ (Cursor)    │  │ (Cursor)    │          │
│  │ + DLQ       │  │ + DLQ       │  │ + DLQ       │          │
│  └─────────────┘  └─────────────┘  └─────────────┘          │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────────────────────────────────────────────────┐│
│  │              Message Buffer (In-Memory)                 ││
│  │  [Msg1] → [Msg2] → [Msg3] → ... → [MsgN]                ││
│  └─────────────────────────────────────────────────────────┘│
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐          │
│  │   Retry     │  │     TTL     │  │   Replay    │          │
│  │   Policy    │  │  Cleanup    │  │   Service   │          │
│  └─────────────┘  └─────────────┘  └─────────────┘          │
└─────────────────────────────────────────────────────────────┘
```

### Project Structure

- **`PushDeliveredQueue.Core`**: Core queue implementation and models
- **`PushDeliveredQueue.AspNetCore`**: ASP.NET Core integration and DI extensions
- **`PushDeliveredQueue.Sample`**: Example web application with API endpoints
- **`PushDeliveredQueue.UI`**: Blazor Server UI for queue management
- **`PushDeliveredQueue.UnitTests`**: Comprehensive unit test suite
- **`PushDeliveredQueue.FunctionalTests`**: End-to-end functional tests
- **`PushDeliveredQueue.UI.Tests`**: UI component tests using bUnit

## 🚀 Quick Start

### 1. Install the Package

```bash
dotnet add package PushDeliveredQueue.AspNetCore
```

### 2. Configure Services

```csharp
// Program.cs
using PushDeliveredQueue.AspNetCore.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add the queue with configuration
builder.Services.AddSubscribableQueueWithOptions(builder.Configuration);
```

### 3. Configure Options

```json
// appsettings.json
{
  "SubscribableQueue": {
    "Ttl": "00:05:00",
    "RetryCount": 3,
    "DelayBetweenRetriesMs": 100
  }
}
```

### 4. Create a Message Handler

```csharp
public class MyMessageHandler : IQueueEventHandler
{
    public async Task<DeliveryResult> OnMessageReceiveAsync(MessageEnvelope message, Guid subscriberId, CancellationToken cancellationToken)
    {
        try
        {
            // Process the message
            await ProcessMessage(message.Payload);
            return DeliveryResult.Ack; // Acknowledge successful processing
        }
        catch (Exception)
        {
            return DeliveryResult.Nack; // Negative acknowledgment for retry
        }
    }

    public async Task<PostMessageFailedBehavior> OnMessageFailedHandlerAsync(MessageEnvelope message, Guid subscriberId, Exception? exception, CancellationToken cancellationToken)
    {
        // Handle failed message processing
        return PostMessageFailedBehavior.AddToDLQ; // Add to dead letter queue
    }

    public async Task<DeliveryResult> OnDeadLetterHandlerAsync(MessageEnvelope message, Guid subscriberId, CancellationToken cancellationToken)
    {
        // Handle dead letter queue messages
        return DeliveryResult.Ack; // Acknowledge to remove from DLQ
    }
}
```

### 5. Use the Queue

```csharp
public class MessageController : ControllerBase
{
    private readonly SubscribableQueue _queue;
    private readonly MyMessageHandler _handler;

    public MessageController(SubscribableQueue queue, MyMessageHandler handler)
    {
        _queue = queue;
        _handler = handler;
    }

    [HttpPost("enqueue")]
    public IActionResult Enqueue([FromBody] string payload)
    {
        var messageId = _queue.Enqueue(payload);
        return Ok(messageId);
    }

    [HttpPost("subscribe")]
    public IActionResult Subscribe()
    {
        var subscriberId = _queue.Subscribe(_handler);
        return Ok(subscriberId);
    }
}
```

## 🚀 Running the Complete Application

### Option A: Using the Launcher (Recommended)

```bash
# Launch both API and UI simultaneously
dotnet run --project PushDeliveredQueue.Launcher
```

### Option B: Using Scripts

**PowerShell:**
```powershell
.\launch-both.ps1
```

**Batch (Windows):**
```cmd
launch-both.bat
```

### Option C: Manual Launch

```bash
# Terminal 1: Start the API
cd PushDeliveredQueue.Sample
dotnet run

# Terminal 2: Start the UI
cd PushDeliveredQueue.UI
dotnet run
```

### Access the Applications

- **API**: https://localhost:7246 (with Swagger documentation)
- **UI**: https://localhost:7274 (Queue monitoring dashboard)

## ⚙️ Configuration

### SubscribableQueueOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Ttl` | `TimeSpan` | Required | Time-to-live for messages in the queue |
| `RetryCount` | `int` | 5 | Number of retry attempts for failed messages |
| `DelayBetweenRetriesMs` | `int` | 200 | Delay between retry attempts in milliseconds |

### Configuration Validation

The library includes built-in validation using Data Annotations:

- `Ttl`: Required field
- `RetryCount`: Range 1-100
- `DelayBetweenRetriesMs`: Range 10-1000ms

## 📚 API Reference

### SubscribableQueue

#### Core Methods

```csharp
public class SubscribableQueue
{
    // Enqueue a message
    public string Enqueue(string payload);
    
    // Subscribe to messages
    public Guid Subscribe(MessageHandler handler);
    
    // Unsubscribe from messages
    public void Unsubscribe(Guid subscriberId);
    
    // Get current queue state
    public SubscribableQueueState GetState();
    
    // Dispose resources
    public void Dispose();
}
```

#### Models

```csharp
// Message envelope containing payload and metadata
public class MessageEnvelope(Guid id, DateTime createdAt, string payload)
{
    public Guid Id { get; init; } = id;
    public DateTime CreatedAt { get; init; } = createdAt;
    public string Payload { get; set; } = payload;
}

// Queue event handler interface
public interface IQueueEventHandler
{
    Task<DeliveryResult> OnMessageReceiveAsync(MessageEnvelope message, Guid subscriberId, CancellationToken cancellationToken);
    Task<PostMessageFailedBehavior> OnMessageFailedHandlerAsync(MessageEnvelope message, Guid subscriberId, Exception? exception, CancellationToken cancellationToken);
    Task<DeliveryResult> OnDeadLetterHandlerAsync(MessageEnvelope message, Guid subscriberId, CancellationToken cancellationToken);
}

// Delivery acknowledgment result
public enum DeliveryResult
{
    Ack,   // Acknowledge successful processing
    Nack   // Negative acknowledgment (retry)
}

// Post-message failure behavior
public enum PostMessageFailedBehavior
{
    RetryOnceThenCommit, // Will retry once immediately and then commit the message
    RetryOnceThenDLQ,    // Will retry once immediately and then add to DLQ
    AddToDLQ,            // Will add to DLQ directly
    Commit,              // Will commit and move on
    Block                // Will block processing
}
```

### State Management

```csharp
// Complete queue state
public class SubscribableQueueState
{
    public List<MessageEnvelope> Buffer { get; set; } = [];
    public Dictionary<Guid, SubscriberState> Subscribers { get; set; } = [];
    public TimeSpan Ttl { get; set; }
}

// Individual subscriber state
public class SubscriberState
{
    public int CursorIndex { get; set; }
    public bool IsCommitted { get; set; }
    public int PendingCount { get; set; }
    public List<MessageEnvelope> DeadLetterQueue { get; set; } = [];
}
```

## 💡 Examples

### Basic Usage

```csharp
// Create queue with options
var options = new SubscribableQueueOptions
{
    Ttl = TimeSpan.FromMinutes(5),
    RetryCount = 3,
    DelayBetweenRetriesMs = 100
};

var queue = new SubscribableQueue(Options.Create(options));

// Subscribe to messages
var handler = new MyMessageHandler();
var subscriberId = queue.Subscribe(handler);

// Enqueue messages
var messageId = queue.Enqueue("Hello, World!");

// Get queue state
var state = queue.GetState();
Console.WriteLine($"Buffer size: {state.Buffer.Count}");
Console.WriteLine($"Active subscribers: {state.Subscribers.Count}");
```

### Advanced Usage with Multiple Subscribers

```csharp
// Create multiple subscribers for different processing needs
var emailHandler = new EmailMessageHandler();
var smsHandler = new SmsMessageHandler();

var emailSubscriber = queue.Subscribe(emailHandler);
var smsSubscriber = queue.Subscribe(smsHandler);

// Enqueue messages for different channels
queue.Enqueue("email:user@example.com:Welcome!");
queue.Enqueue("sms:+1234567890:Your code is 123456");
```

### Replay and Dead Letter Queue Management

```csharp
// Replay a specific message from DLQ
await queue.ReplayFromDlqAsync(subscriberId, messageId, CancellationToken.None);

// Replay all messages in DLQ for a subscriber
await queue.ReplayAllDlqMessagesAsync(subscriberId, CancellationToken.None);

// Replay all DLQ messages for all subscribers
queue.ReplayAllDlqSubscribers(CancellationToken.None);

// Replay from a specific message in the buffer
queue.ReplayFrom(subscriberId, messageId);

// Change message payload
queue.ChangeMessagePayload(messageId, "updated payload", CancellationToken.None);
```

### Error Handling and Retry

```csharp
public class RobustMessageHandler : IQueueEventHandler
{
    private readonly ILogger<RobustMessageHandler> _logger;

    public RobustMessageHandler(ILogger<RobustMessageHandler> logger)
    {
        _logger = logger;
    }

    public async Task<DeliveryResult> OnMessageReceiveAsync(MessageEnvelope message, Guid subscriberId, CancellationToken cancellationToken)
    {
        try
        {
            // Attempt to process the message
            await ProcessWithExternalService(message.Payload);
            return DeliveryResult.Ack;
        }
        catch (TemporaryException ex)
        {
            // Log the error for retry
            _logger.LogWarning(ex, "Temporary failure, will retry");
            return DeliveryResult.Nack; // Will be retried
        }
        catch (PermanentException ex)
        {
            // Log the error and don't retry
            _logger.LogError(ex, "Permanent failure, skipping");
            return DeliveryResult.Ack; // Acknowledge to prevent retries
        }
    }

    public async Task<PostMessageFailedBehavior> OnMessageFailedHandlerAsync(MessageEnvelope message, Guid subscriberId, Exception? exception, CancellationToken cancellationToken)
    {
        if (exception is TemporaryException)
        {
            return PostMessageFailedBehavior.RetryOnceThenDLQ;
        }
        return PostMessageFailedBehavior.AddToDLQ;
    }

    public async Task<DeliveryResult> OnDeadLetterHandlerAsync(MessageEnvelope message, Guid subscriberId, CancellationToken cancellationToken)
    {
        // Handle dead letter queue messages
        _logger.LogWarning("Message {MessageId} in DLQ for subscriber {SubscriberId}", message.Id, subscriberId);
        return DeliveryResult.Ack; // Remove from DLQ
    }
}
```

## 🧪 Testing

### Running Tests

```bash
# Run all tests
dotnet test

# Run unit tests only
dotnet test PushDeliveredQueue.UnitTests

# Run functional tests only
dotnet test PushDeliveredQueue.FunctionalTests

# Run UI tests only
dotnet test PushDeliveredQueue.UI.Tests

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Test Categories

- **Unit Tests**: Core functionality, configuration, DI integration, replay functionality, TTL/pruning, error handling
- **Functional Tests**: End-to-end API testing with TestContainers
- **UI Tests**: Blazor component testing using bUnit
- **Concurrency Tests**: Multi-threaded scenarios and stress testing
- **Integration Tests**: Complete message lifecycle testing

### Sample Applications

The project provides multiple sample applications:

```bash
# Run the API sample application
cd PushDeliveredQueue.Sample
dotnet run

# Access Swagger UI
open http://localhost:7246

# Run the Blazor UI application
cd PushDeliveredQueue.UI
dotnet run

# Access Blazor UI
open http://localhost:7274
```

## 🔧 Performance Considerations

### Memory Management

- Messages are automatically pruned based on TTL
- Each subscriber maintains an independent cursor
- Background cleanup prevents memory leaks

### Concurrency

- Thread-safe operations using locks where necessary
- Concurrent subscriber management
- Background processing for message delivery

### Scalability

- In-memory design for low-latency scenarios
- Suitable for single-instance deployments
- Consider external message brokers for distributed scenarios

## 🤝 Planned future enhancements


## ⚙️ Delivery & Retry Intelligence
- Per-subscriber acknowledgment modes (receipt-time vs post-process ack)  
- Configurable delivery guarantees: at-most-once, at-least-once, exactly-once (with idempotency tokens)  
- Exponential backoff with jitter and custom back-off policies  
- Dead-letter routing: quarantine messages after max retries for manual inspection  
- Replay from offset or timestamp for cache rebuilds and debugging  
- Subscriber health checks: auto-pause or disable misbehaving consumers after N failures  

---

## 🛡 Consistency & Ordering
- Partitioned topics to preserve ordering for related keys without blocking the entire queue  
- Idempotency guards to prevent state corruption from duplicate deliveries  
- Versioned payload contracts to support rolling upgrades and schema evolution  

---

## 📈 Scalability & Throughput
- Batch enqueue and dispatch to boost end-to-end throughput  
- Parallel dispatch per subscriber (configurable concurrency level)  
- Rate limiting and back-pressure controls to throttle producers or slow subscribers  

---

## 📊 Observability & Metrics
- Diagnostic endpoints exposing queue length, consumer lag, retry counts, last processed offsets  
- Prometheus/OpenTelemetry integration (counters, histograms, gauges) for throughput, latency, error rates  
- Structured events on enqueue, delivery start/end, and pruning for audit trails  
- Trace instrumentation / correlation IDs for end-to-end request tracking  

---

## 🔌 Subscription Management
- Dynamic subscriber registration/discovery at runtime (no redeploys)  
- Filter expressions so subscribers receive only relevant events  
- Back-pressure signaling from slow consumers  
- Pause/Resume individual subscribers or entire queues via API  

---

## 🧠 Self-Healing & Operational Aids
- Heartbeat & liveness pings to detect and recover from dead subscribers  
- Automatic catch-up via DB snapshot when a subscriber falls too far behind  
- Schema-drift detectors warning on unexpected event fields  

---

## 💾 Durability & Dead-Letter Handling
- Pluggable persistent store (PostgreSQL, Redis, etc.) to survive restarts  
- Per-message TTL overrides for fine-grained retention  
- Dead-Letter Queue (DLQ) for messages that exhaust retries  
- Retention and cleanup policies for both main buffer and DLQ  

---

## 🌐 Multi-Queue & Namespacing
- Support multiple named queues in one process  
- Per-queue configuration (TTL, retry, back-off, concurrency)  
- Dynamic creation and disposal of queues at runtime  

---

## 🔧 Admin & Management APIs
- Requeue or purge messages in buffer and DLQ via HTTP/API  
- Inspect subscriber lag and enforce SLAs or trigger alerts  
- Expose management endpoints for operational controls (pause, resume, cleanup)  

---

# In-Depth Code Review for .NET 9 & C# 13

## 1. Lock Usage with `System.Threading.Lock`

In .NET 9, `System.Threading.Lock` is a value‐type lock optimized for `using` patterns and avoids the boxing overhead of `lock(object)`.  
  
• Replace `lock (_lock)` with  
```csharp
using var releaser = _lock.Enter();
// critical section
```  
• Ensure the `Lock` instance comes from `using System.Threading;` so it’s the built-in struct, not a custom type.  
• Scope of the lock is clear, and exceptions automatically release the lock.

## 2. Async Flow & `AsyncLock`

Mixing synchronous `Lock` with `await` still blocks a thread. In C# 13 create an `AsyncLock` implementation or use libraries like [Nito.AsyncEx]. Example:

```csharp
private readonly AsyncLock _asyncLock = new();
…
using (await _asyncLock.LockAsync(cancellationToken))
{
    await ProcessMessageAsync(...).ConfigureAwait(false);
}
```

This guarantees true asynchronous mutual exclusion without thread‐pool blocking.

## 3. Subscriber DLQ Thread‐Safety

`CursorState.DeadLetterQueue` is currently a plain `List<T>`. Under high concurrency:

• Switch to `ConcurrentQueue<MessageEnvelope>` if ordering suffices.  
• Or wrap all DLQ mutations in a dedicated `AsyncLock` (or the same `Lock`) to avoid races between `AddToDeadLetterQueue` and replay logic.

## 4. Background Work via `IHostedService`

Instead of fire‐and‐forget `Task.Run` for pruning and replay, register dedicated `BackgroundService` instances. This provides:

1. Automatic graceful shutdown on application stop  
2. Built-in exception propagation to the host’s logs  
3. Centralized lifetime management  

Example skeleton:

```csharp
public class PruneService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await queue.PruneExpiredAsync(stoppingToken);
    }
}
```

## 5. Efficient Buffer Pruning

Repeated `RemoveAt(0)` on a `List<T>` is O(n²) on large datasets. Consider:

- A `LinkedList<T>` for O(1) head removals  
- A ring buffer (`Memory<T>` or `ArraySegment<T>`) that advances a start index  
- Periodic “compact” passes to avoid unbounded growth  

## 6. Retry + Fallback Policy Order

Current wrap `Policy.WrapAsync(fallback, retry)` invokes fallback first. Reverse for “retry first, then fallback”:

```csharp
_retryPolicy = Policy.WrapAsync(retryPolicy, fallbackPolicy);
```

Verify behavior by writing a small console test to confirm exceptions hit retry loops before fallback.

## 7. Cancellation & Disposal Patterns

- The linked `CancellationTokenSource` created in `Subscribe` isn’t stored or disposed. Store it in `CursorState` so `Dispose()` can clean up every CTS.  
- Since `CancellationTokenSource` now implements `IAsyncDisposable`, consider `await using` in .NET 9 when tearing down long‐running services.

## 8. Structured Logging & Metrics

Enable `System.Diagnostics.Metrics` (new in .NET 6) to:

- Emit counters for delivered, failed, DLQ‐added messages  
- Record histograms for delivery latency  

Example:

```csharp
static readonly Meter _meter = new("PushDeliveredQueue");
static readonly Counter<long> _dlqCounter = _meter.CreateCounter<long>("dlq_messages");
…
_dlqCounter.Add(1, new("subscriber", subscriberId.ToString()));
```

Use `OpenTelemetry` exporters for unified observability.

---

# Future Enhancements & Extensions

1. **Clustered Instances & Persistence**  
   - Plug in Redis or SQL-backed storage to share buffer state across multiple nodes.  
   - Use `IHostedService` to elect a leader for prune loops.

2. **Admin & HTTP APIs**  
   - Add filtered DLQ endpoints (`/dlq/{subscriberId}?status=failed`) with paging.  
   - Expose health checks for each background service.

3. **Batch & Windowed Delivery**  
   - Allow subscribers to request N messages at once via `IAsyncEnumerable<MessageEnvelope>`.  
   - Add flow‐control so fast producers don’t overwhelm slow consumers.

4. **Dynamic Configuration**  
   - Integrate with `IOptionsMonitor<SubscribableQueueOptions>` to adjust TTL, retry count, delay at runtime.  
   - Push config changes via a control channel or admin UI.

5. **Pluggable Handlers**  
   - Support middleware pipeline for handlers, allowing crosscutting concerns like correlation ID propagation, enrichment, or custom DLQ policies.

6. **Chaos Testing & Resiliency**  
   - Introduce failure injection hooks (e.g., random Nacks or exceptions) for systematic resilience verification.  
   - Provide a “chaos” mode via config to automatically add jitter and faults.

7. **Granular Metrics & Alerts**  
   - Surface per-subscriber backpressure metrics (pending count vs. throughput).  
   - Bind alerts when DLQ size for any subscriber exceeds a threshold.

By embracing these .NET 9/C# 13 idioms—`Lock` structs, `AsyncLock`, `BackgroundService`, `System.Diagnostics.Metrics`—and layering in clustering, admin APIs, dynamic tuning, and chaos testing, the queue will evolve from a single‐node in‐memory demo into a resilient, observable, and production‐grade messaging service.


**Built with ❤️ for .NET developers who need reliable, real-time message processing.**
