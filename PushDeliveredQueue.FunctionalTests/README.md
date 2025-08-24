# PushDeliveredQueue.FunctionalTests

This project contains comprehensive functional tests for the PushDeliveredQueue sample application using TestContainers and WebApplicationFactory. The tests verify the end-to-end functionality of the queue system through HTTP API calls.

## Test Architecture

### TestWebApplicationFactory
- **Custom WebApplicationFactory**: Extends `WebApplicationFactory<Program>` to set up the test environment
- **TestContainers Integration**: Uses Seq container for structured logging during tests
- **Configuration Management**: Loads test-specific configuration from `appsettings.test.json`
- **Logging Setup**: Configures Serilog with console and Seq sinks for comprehensive logging

### Test Categories

#### 1. QueueApiTests.cs
Core API functionality tests:
- **Message Enqueueing**: Validates message enqueueing with various payloads
- **Subscriber Management**: Tests subscribe/unsubscribe operations
- **State Inspection**: Verifies queue state retrieval
- **Message Lifecycle**: End-to-end message processing workflows
- **Error Handling**: Tests retry mechanisms and failure scenarios
- **API Endpoints**: Validates Swagger and OpenAPI accessibility

#### 2. ConcurrencyTests.cs
Concurrent operation testing:
- **Concurrent Enqueueing**: Multiple simultaneous message enqueueing
- **Concurrent Subscribing**: Multiple subscriber creation
- **Mixed Operations**: Concurrent enqueue and subscribe operations
- **Rapid Operations**: Fast subscribe/unsubscribe cycles
- **Bulk Processing**: Large message volumes with multiple subscribers
- **State Consistency**: Concurrent state inspection validation
- **Stress Testing**: High-load scenarios

#### 3. LoggingTests.cs
Logging and observability tests:
- **Operation Logging**: Verifies logging of enqueue, subscribe, unsubscribe operations
- **Processing Events**: Tests logging during message processing
- **Retry Logging**: Validates logging during retry attempts
- **Error Logging**: Tests error condition logging
- **Startup Logging**: Application startup and configuration logging

## Test Configuration

### appsettings.test.json
Optimized configuration for functional testing:
```json
{
  "SubscribableQueue": {
    "Ttl": "00:01:00",
    "RetryCount": 2,
    "DelayBetweenRetriesMs": 100
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": ["Console"]
  }
}
```

### TestContainers Setup
- **Seq Container**: Structured logging server for test observability
- **Port Mapping**: Dynamic port allocation for container isolation
- **Lifecycle Management**: Automatic container startup/shutdown

## Running Tests

### Prerequisites
- .NET 9.0 SDK
- Docker Desktop (for TestContainers)
- All project dependencies restored

### Command Line
```bash
# Run all functional tests
dotnet test PushDeliveredQueue.FunctionalTests

# Run with verbose output
dotnet test PushDeliveredQueue.FunctionalTests --verbosity normal

# Run specific test class
dotnet test PushDeliveredQueue.FunctionalTests --filter "FullyQualifiedName~QueueApiTests"

# Run with test output
dotnet test PushDeliveredQueue.FunctionalTests --logger "console;verbosity=detailed"
```

### Visual Studio
1. Open the solution in Visual Studio
2. Build the solution
3. Open Test Explorer (Test > Test Explorer)
4. Run functional tests or specific test methods

## Test Scenarios

### Basic Functionality
- ✅ Message enqueueing with various payloads
- ✅ Subscriber creation and management
- ✅ Queue state inspection
- ✅ Complete message lifecycle

### Advanced Scenarios
- ✅ Multiple concurrent subscribers
- ✅ Message retry mechanisms
- ✅ Failure handling and recovery
- ✅ Rapid subscribe/unsubscribe cycles
- ✅ High-load stress testing

### Integration Testing
- ✅ HTTP API contract validation
- ✅ Response format verification
- ✅ Error response handling
- ✅ Swagger/OpenAPI accessibility

## Test Patterns

### WebApplicationFactory Pattern
```csharp
public class QueueApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    
    public QueueApiTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }
}
```

### Async Testing Pattern
```csharp
[Fact]
public async Task TestName_Scenario_ExpectedResult()
{
    // Arrange
    var content = new StringContent(JsonSerializer.Serialize(payload));
    
    // Act
    var response = await _client.PostAsync("/endpoint", content);
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

### Concurrent Testing Pattern
```csharp
[Fact]
public async Task ConcurrentOperations_ShouldWorkCorrectly()
{
    // Arrange
    var tasks = new List<Task<HttpResponseMessage>>();
    
    // Act
    for (int i = 0; i < count; i++)
    {
        tasks.Add(_client.PostAsync("/endpoint", content));
    }
    var responses = await Task.WhenAll(tasks);
    
    // Assert
    responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
}
```

## Test Dependencies

### Core Dependencies
- **Microsoft.AspNetCore.Mvc.Testing**: WebApplicationFactory support
- **Testcontainers**: Container management for testing
- **Testcontainers.Seq**: Seq logging container
- **FluentAssertions**: Readable assertions
- **xUnit**: Testing framework

### Logging Dependencies
- **Serilog.AspNetCore**: Structured logging
- **Serilog.Sinks.Console**: Console logging
- **Serilog.Sinks.Seq**: Seq logging sink

## Continuous Integration

The functional tests are designed for CI/CD environments:

### Docker Requirements
- Docker Desktop or Docker Engine
- Container registry access for Seq image

### CI Configuration
```yaml
# Example GitHub Actions step
- name: Run Functional Tests
  run: |
    dotnet test PushDeliveredQueue.FunctionalTests --verbosity normal
    --logger "trx;LogFileName=functional-tests.trx"
```

### Test Isolation
- Each test class gets a fresh WebApplicationFactory instance
- TestContainers provide isolated environments
- No shared state between tests

## Troubleshooting

### Common Issues

1. **Docker Not Running**
   - Ensure Docker Desktop is started
   - Verify Docker daemon is accessible

2. **Container Startup Failures**
   - Check available ports (default: 5341)
   - Verify network connectivity
   - Check Docker resource limits

3. **Test Timeouts**
   - Increase timeout values for slow environments
   - Check system resources
   - Verify container health

4. **Logging Issues**
   - Verify Seq container is running
   - Check port mappings
   - Review Serilog configuration

### Debugging

1. **Enable Verbose Logging**
   ```bash
   dotnet test --verbosity normal --logger "console;verbosity=detailed"
   ```

2. **Container Inspection**
   ```bash
   docker ps  # List running containers
   docker logs <container-id>  # View container logs
   ```

3. **Test Output**
   - Check test results in Test Explorer
   - Review console output for errors
   - Examine Seq logs for application events

## Performance Considerations

### Test Execution Time
- **Individual Tests**: 1-5 seconds each
- **Full Suite**: 2-5 minutes
- **Container Startup**: 10-30 seconds (first run)

### Resource Usage
- **Memory**: ~200MB per test class
- **CPU**: Minimal during idle, spikes during processing
- **Network**: Local container communication only

### Optimization Tips
- Use `IClassFixture` for shared factory instances
- Minimize container startup/shutdown
- Use appropriate wait times for async operations
- Clean up resources in test teardown

## Contributing

When adding new functional tests:

1. **Follow Naming Conventions**
   - Test class: `{Feature}Tests`
   - Test method: `{Scenario}_{Condition}_{ExpectedResult}`

2. **Use Appropriate Patterns**
   - WebApplicationFactory for HTTP testing
   - TestContainers for external dependencies
   - FluentAssertions for readable assertions

3. **Consider Test Categories**
   - API functionality tests
   - Concurrency and stress tests
   - Logging and observability tests

4. **Maintain Test Isolation**
   - No shared state between tests
   - Proper cleanup in test teardown
   - Independent test execution

5. **Document Complex Scenarios**
   - Add comments for complex test logic
   - Explain timing considerations
   - Document expected behaviors
