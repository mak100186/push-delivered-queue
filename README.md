# PushDeliveredQueue

A reactive push-based message queue system built for .NET 9.0, designed for real-time systems, event-driven architectures, and notification pipelines. This library provides a lightweight, in-memory message queue with automatic retry mechanisms, subscriber management, and configurable message TTL.

## 🚀 Features

- **Push-Based Delivery**: Messages are actively pushed to subscribers rather than pulled
- **Multiple Subscribers**: Support for multiple concurrent subscribers with independent cursors
- **Automatic Retry**: Configurable retry policies with exponential backoff using Polly
- **Message TTL**: Automatic cleanup of expired messages to prevent memory leaks
- **Ack/Nack Support**: Explicit message acknowledgment for reliable delivery
- **ASP.NET Core Integration**: Seamless integration with dependency injection
- **Comprehensive Testing**: Full unit and functional test coverage
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
│  └─────────────┘  └─────────────┘  └─────────────┘          │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────────────────────────────────────────────────┐│
│  │              Message Buffer (In-Memory)                 ││
│  │  [Msg1] → [Msg2] → [Msg3] → ... → [MsgN]                ││
│  └─────────────────────────────────────────────────────────┘│
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐          │
│  │   Retry     │  │     TTL     │  │   Pruning   │          │
│  │   Policy    │  │  Cleanup    │  │   Service   │          │
│  └─────────────┘  └─────────────┘  └─────────────┘          │
└─────────────────────────────────────────────────────────────┘
```

### Project Structure

- **`PushDeliveredQueue.Core`**: Core queue implementation and models
- **`PushDeliveredQueue.AspNetCore`**: ASP.NET Core integration and DI extensions
- **`PushDeliveredQueue.Sample`**: Example web application with API endpoints
- **`PushDeliveredQueue.UnitTests`**: Comprehensive unit test suite
- **`PushDeliveredQueue.FunctionalTests`**: End-to-end functional tests

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
public class MyMessageHandler
{
    public async Task<DeliveryResult> HandleMessageAsync(MessageEnvelope message)
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
        var subscriberId = _queue.Subscribe(_handler.HandleMessageAsync);
        return Ok(subscriberId);
    }
}
```

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
public record MessageEnvelope(Guid Id, DateTime Timestamp, string Payload);

// Message processing delegate
public delegate Task<DeliveryResult> MessageHandler(MessageEnvelope message);

// Delivery acknowledgment result
public enum DeliveryResult
{
    Ack,   // Acknowledge successful processing
    Nack   // Negative acknowledgment (retry)
}
```

### State Management

```csharp
// Complete queue state
public class SubscribableQueueState
{
    public List<MessageEnvelope> Buffer { get; set; } = [];
    public Dictionary<Guid, SubscriberState> Subscribers { get; set; } = [];
}

// Individual subscriber state
public class SubscriberState
{
    public int CursorIndex { get; set; }
    public bool IsCommitted { get; set; }
    public int PendingCount { get; set; }
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
var subscriberId = queue.Subscribe(async (message) =>
{
    Console.WriteLine($"Processing: {message.Payload}");
    await ProcessMessage(message.Payload);
    return DeliveryResult.Ack;
});

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
var emailSubscriber = queue.Subscribe(async (message) =>
{
    if (message.Payload.Contains("email"))
    {
        await SendEmail(message.Payload);
        return DeliveryResult.Ack;
    }
    return DeliveryResult.Nack; // Skip non-email messages
});

var smsSubscriber = queue.Subscribe(async (message) =>
{
    if (message.Payload.Contains("sms"))
    {
        await SendSms(message.Payload);
        return DeliveryResult.Ack;
    }
    return DeliveryResult.Nack; // Skip non-sms messages
});

// Enqueue messages for different channels
queue.Enqueue("email:user@example.com:Welcome!");
queue.Enqueue("sms:+1234567890:Your code is 123456");
```

### Error Handling and Retry

```csharp
var subscriberId = queue.Subscribe(async (message) =>
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
});
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

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Test Categories

- **Unit Tests**: Core functionality, configuration, DI integration
- **Functional Tests**: End-to-end API testing with TestContainers
- **Concurrency Tests**: Multi-threaded scenarios and stress testing
- **Integration Tests**: Complete message lifecycle testing

### Sample Application

The `PushDeliveredQueue.Sample` project provides a complete working example:

```bash
# Run the sample application
cd PushDeliveredQueue.Sample
dotnet run

# Access Swagger UI
open http://localhost:5000
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

**Built with ❤️ for .NET developers who need reliable, real-time message processing.**
