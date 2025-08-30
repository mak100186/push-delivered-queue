using FluentAssertions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PushDeliveredQueue.AspNetCore.DependencyInjection;
using PushDeliveredQueue.Core;
using PushDeliveredQueue.Core.Configs;

using Xunit;

using static PushDeliveredQueue.UnitTests.TestHelpers;

namespace PushDeliveredQueue.UnitTests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddSubscribableQueueWithOptions_ShouldRegisterServices()
    {
        // Arrange
        var services = GetServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SubscribableQueue:Ttl"] = "00:05:00",
                ["SubscribableQueue:RetryCount"] = "3",
                ["SubscribableQueue:DelayBetweenRetriesMs"] = "200"
            })
            .Build();

        // Act
        services.AddSubscribableQueueWithOptions(configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        // Should register SubscribableQueueOptions
        var options = serviceProvider.GetService<IOptions<SubscribableQueueOptions>>();
        options.Should().NotBeNull();
        options!.Value.Ttl.Should().Be(TimeSpan.FromMinutes(5));
        options.Value.RetryCount.Should().Be(3);
        options.Value.DelayBetweenRetriesMs.Should().Be(200);

        // Should register SubscribableQueue as singleton
        var queue1 = serviceProvider.GetService<SubscribableQueue>();
        var queue2 = serviceProvider.GetService<SubscribableQueue>();
        queue1.Should().NotBeNull();
        queue2.Should().NotBeNull();
        queue1.Should().BeSameAs(queue2); // Should be singleton
    }

    [Fact]
    public void AddSubscribableQueueWithOptions_WithInvalidConfiguration_ShouldThrowOnValidation()
    {
        // Arrange
        var services = GetServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SubscribableQueue:Ttl"] = "00:05:00",
                ["SubscribableQueue:RetryCount"] = "0", // Invalid: below minimum
                ["SubscribableQueue:DelayBetweenRetriesMs"] = "200"
            })
            .Build();

        // Act
        services.AddSubscribableQueueWithOptions(configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var action = () => serviceProvider.GetService<SubscribableQueue>();
        action.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddSubscribableQueueWithOptions_WithMissingConfiguration_ShouldNotThrow()
    {
        // Arrange
        var services = GetServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Missing Ttl configuration
                ["SubscribableQueue:RetryCount"] = "3",
                ["SubscribableQueue:DelayBetweenRetriesMs"] = "200"
            })
            .Build();

        // Act
        services.AddSubscribableQueueWithOptions(configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var action = () => serviceProvider.GetService<SubscribableQueue>();
        action.Should().NotThrow(); // Missing Ttl will use default value (TimeSpan.Zero)
    }

    [Fact]
    public void AddSubscribableQueueWithOptions_WithEmptyConfiguration_ShouldThrowOnValidation()
    {
        // Arrange
        var services = GetServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        services.AddSubscribableQueueWithOptions(configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var action = () => serviceProvider.GetService<SubscribableQueue>();
        action.Should().Throw<OptionsValidationException>(); // Default values fail validation
    }

    [Fact]
    public void AddSubscribableQueueWithOptions_ShouldReturnServiceCollection()
    {
        // Arrange
        var services = GetServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SubscribableQueue:Ttl"] = "00:05:00",
                ["SubscribableQueue:RetryCount"] = "3",
                ["SubscribableQueue:DelayBetweenRetriesMs"] = "200"
            })
            .Build();

        // Act
        var result = services.AddSubscribableQueueWithOptions(configuration);

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddSubscribableQueueWithOptions_WithValidConfiguration_ShouldValidateOnStart()
    {
        // Arrange
        var services = GetServiceCollection();
        services.AddLogging(config =>
        {
            config.ClearProviders();
            config.AddConsole(); // Add other providers as needed
            config.SetMinimumLevel(LogLevel.Information);
        });

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SubscribableQueue:Ttl"] = "00:05:00",
                ["SubscribableQueue:RetryCount"] = "3",
                ["SubscribableQueue:DelayBetweenRetriesMs"] = "200"
            })
            .Build();

        // Act
        services.AddSubscribableQueueWithOptions(configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        // Should not throw when getting the service (validation passes)
        var queue = serviceProvider.GetService<SubscribableQueue>();
        queue.Should().NotBeNull();
    }

    [Fact]
    public void AddSubscribableQueueWithOptions_WithBoundaryValues_ShouldWorkCorrectly()
    {
        // Arrange
        var services = GetServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SubscribableQueue:Ttl"] = "00:01:00",
                ["SubscribableQueue:RetryCount"] = "1", // Minimum value
                ["SubscribableQueue:DelayBetweenRetriesMs"] = "10" // Minimum value
            })
            .Build();

        // Act
        services.AddSubscribableQueueWithOptions(configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetService<IOptions<SubscribableQueueOptions>>();
        options.Should().NotBeNull();
        options!.Value.RetryCount.Should().Be(1);
        options.Value.DelayBetweenRetriesMs.Should().Be(10);
    }

    [Fact]
    public void AddSubscribableQueueWithOptions_WithMaximumValues_ShouldWorkCorrectly()
    {
        // Arrange
        var services = GetServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SubscribableQueue:Ttl"] = "00:01:00",
                ["SubscribableQueue:RetryCount"] = "100", // Maximum value
                ["SubscribableQueue:DelayBetweenRetriesMs"] = "1000" // Maximum value
            })
            .Build();

        // Act
        services.AddSubscribableQueueWithOptions(configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetService<IOptions<SubscribableQueueOptions>>();
        options.Should().NotBeNull();
        options!.Value.RetryCount.Should().Be(100);
        options.Value.DelayBetweenRetriesMs.Should().Be(1000);
    }

    [Fact]
    public void AddSubscribableQueueWithOptions_WithInvalidTimeSpan_ShouldThrowOnValidation()
    {
        // Arrange
        var services = GetServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SubscribableQueue:Ttl"] = "invalid-timespan",
                ["SubscribableQueue:RetryCount"] = "3",
                ["SubscribableQueue:DelayBetweenRetriesMs"] = "200"
            })
            .Build();

        // Act
        services.AddSubscribableQueueWithOptions(configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var action = () => serviceProvider.GetService<SubscribableQueue>();
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddSubscribableQueueWithOptions_WithInvalidIntegerValues_ShouldThrowOnValidation()
    {
        // Arrange
        var services = GetServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SubscribableQueue:Ttl"] = "00:05:00",
                ["SubscribableQueue:RetryCount"] = "not-a-number",
                ["SubscribableQueue:DelayBetweenRetriesMs"] = "200"
            })
            .Build();

        // Act
        services.AddSubscribableQueueWithOptions(configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var action = () => serviceProvider.GetService<SubscribableQueue>();
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddSubscribableQueueWithOptions_ShouldRegisterOptionsWithDataAnnotationsValidation()
    {
        // Arrange
        var services = GetServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SubscribableQueue:Ttl"] = "00:05:00",
                ["SubscribableQueue:RetryCount"] = "3",
                ["SubscribableQueue:DelayBetweenRetriesMs"] = "200"
            })
            .Build();

        // Act
        services.AddSubscribableQueueWithOptions(configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        // Should be able to get options without validation errors
        var options = serviceProvider.GetService<IOptions<SubscribableQueueOptions>>();
        options.Should().NotBeNull();

        // Should be able to get the queue service
        var queue = serviceProvider.GetService<SubscribableQueue>();
        queue.Should().NotBeNull();
    }
}
