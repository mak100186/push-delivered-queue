# PushDeliveredQueue.UnitTests

This project contains comprehensive unit tests for the PushDeliveredQueue library, covering all major functionality including the core queue implementation, configuration validation, dependency injection, and integration scenarios.

## Test Structure

### 1. SubscribableQueueTests.cs
Tests for the core `SubscribableQueue` class functionality:

- **Constructor Tests**: Validating initialization with different options
- **Enqueue Tests**: Message enqueueing functionality
- **Subscribe/Unsubscribe Tests**: Subscriber management
- **Message Delivery Tests**: Ack/Nack handling and retry logic
- **State Management Tests**: Queue state inspection
- **Dispose Tests**: Resource cleanup

### 2. ModelTests.cs
Tests for all model classes:

- **MessageEnvelope**: Message structure and properties
- **DeliveryResult**: Delivery status enumeration
- **CursorState**: Subscriber cursor management
- **SubscriberState**: Subscriber state representation
- **SubscribableQueueState**: Overall queue state
- **MessageHandler**: Message processing delegate

### 3. ConfigurationTests.cs
Tests for configuration validation:

- **SubscribableQueueOptions**: Property validation with DataAnnotations
- **Range Validation**: RetryCount and DelayBetweenRetriesMs bounds checking
- **Required Field Validation**: Ttl field validation
- **Edge Cases**: Zero, negative, and boundary values

### 4. DependencyInjectionTests.cs
Tests for ASP.NET Core integration:

- **Service Registration**: DI container setup
- **Configuration Binding**: Options binding from configuration
- **Validation Integration**: DataAnnotations validation in DI
- **Error Handling**: Invalid configuration scenarios

### 5. IntegrationTests.cs
End-to-end integration tests:

- **Complete Message Lifecycle**: Full message flow from enqueue to delivery
- **Multiple Subscribers**: Concurrent subscriber scenarios
- **Retry Mechanisms**: Failure handling and retry logic
- **Exception Handling**: Error scenarios and recovery
- **Service Provider Integration**: Full DI container testing

### 6. TestHelpers.cs
Utility classes and helper methods:

- **Configuration Builders**: Helper methods for creating test configurations
- **Service Provider Factories**: DI container setup helpers
- **Message Handler Factories**: Pre-built message handlers for testing
- **Async Utilities**: Wait conditions and timing helpers

## Test Coverage

The test suite provides comprehensive coverage of:

- ✅ **Core Functionality**: All public methods of SubscribableQueue
- ✅ **Configuration**: Options validation and binding
- ✅ **Dependency Injection**: Service registration and resolution
- ✅ **Error Scenarios**: Exception handling and retry logic
- ✅ **Concurrent Operations**: Multiple subscribers and message handling
- ✅ **Resource Management**: Proper disposal and cleanup
- ✅ **Edge Cases**: Boundary conditions and invalid inputs

## Running Tests

### Prerequisites
- .NET 9.0 SDK
- All project dependencies restored

### Command Line
```bash
# Run all tests
dotnet test PushDeliveredQueue.UnitTests

# Run with verbose output
dotnet test PushDeliveredQueue.UnitTests --verbosity normal

# Run specific test class
dotnet test PushDeliveredQueue.UnitTests --filter "FullyQualifiedName~SubscribableQueueTests"

# Run with coverage (if coverlet is available)
dotnet test PushDeliveredQueue.UnitTests --collect:"XPlat Code Coverage"
```

### Visual Studio
1. Open the solution in Visual Studio
2. Build the solution
3. Open Test Explorer (Test > Test Explorer)
4. Run all tests or specific test methods

## Test Dependencies

The test project uses the following testing libraries:

- **xUnit**: Testing framework
- **FluentAssertions**: Readable assertions
- **Moq**: Mocking framework
- **Microsoft.Extensions.DependencyInjection**: DI testing
- **Microsoft.Extensions.Configuration**: Configuration testing

## Test Patterns

### Arrange-Act-Assert (AAA)
All tests follow the AAA pattern for clear structure:

```csharp
[Fact]
public void TestName_Scenario_ExpectedResult()
{
    // Arrange
    var queue = new SubscribableQueue(options);
    
    // Act
    var result = queue.Enqueue("test message");
    
    // Assert
    result.Should().NotBeNullOrEmpty();
}
```

### Async Testing
Tests that involve asynchronous operations use proper async/await patterns:

```csharp
[Fact]
public async Task AsyncTest_Scenario_ExpectedResult()
{
    // Arrange
    var queue = new SubscribableQueue(options);
    
    // Act
    var subscriberId = queue.Subscribe(handler);
    await Task.Delay(1000); // Wait for processing
    
    // Assert
    deliveredMessages.Should().HaveCount(1);
}
```

### Mocking
Tests use Moq for mocking dependencies:

```csharp
[Fact]
public void TestWithMock_Scenario_ExpectedResult()
{
    // Arrange
    var mockOptions = new Mock<IOptions<SubscribableQueueOptions>>();
    mockOptions.Setup(x => x.Value).Returns(validOptions);
    
    // Act
    var queue = new SubscribableQueue(mockOptions.Object);
    
    // Assert
    queue.Should().NotBeNull();
}
```

## Continuous Integration

The test project is designed to work with CI/CD pipelines:

- All tests are deterministic and don't rely on external dependencies
- Tests use in-memory configurations and services
- No external network calls or file system dependencies
- Fast execution suitable for CI environments

## Contributing

When adding new tests:

1. Follow the existing naming conventions
2. Use the AAA pattern
3. Add appropriate test categories if needed
4. Ensure tests are deterministic and fast
5. Add documentation for complex test scenarios

## Troubleshooting

### Common Issues

1. **ObjectDisposedException**: Fixed by adding proper disposal guards
2. **Timing Issues**: Use appropriate delays and wait conditions
3. **Configuration Validation**: Ensure test configurations match validation rules
4. **Async Deadlocks**: Use async/await properly, avoid blocking calls

### Debugging

To debug failing tests:

1. Run tests with verbose output: `dotnet test --verbosity normal`
2. Use breakpoints in Visual Studio
3. Check test output for detailed error messages
4. Verify test data and expected results
