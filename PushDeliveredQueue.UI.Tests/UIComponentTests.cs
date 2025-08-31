using Bunit;

using FluentAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using PushDeliveredQueue.Core;
using PushDeliveredQueue.Core.Abstractions;
using PushDeliveredQueue.Core.Configs;
using PushDeliveredQueue.Core.Models;

using Xunit;

namespace PushDeliveredQueue.UI.Tests;

public class UIComponentTests : TestContext
{
    private readonly Mock<ILogger<SubscribableQueue>> _mockLogger;
    private readonly Mock<IOptions<SubscribableQueueOptions>> _mockOptions;
    private readonly SubscribableQueue _queue;

    public UIComponentTests()
    {
        _mockLogger = new Mock<ILogger<SubscribableQueue>>();
        _mockOptions = new Mock<IOptions<SubscribableQueueOptions>>();
        _mockOptions.Setup(x => x.Value).Returns(new SubscribableQueueOptions
        {
            Ttl = TimeSpan.FromMinutes(5),
            RetryCount = 3,
            DelayBetweenRetriesMs = 100
        });

        _queue = new SubscribableQueue(_mockOptions.Object, _mockLogger.Object);

        // Register services for Blazor components
        Services.AddSingleton(_queue);
        Services.AddSingleton(_mockLogger.Object);
    }

    [Fact]
    public void HomePage_ShouldRenderCorrectly()
    {
        // Act
        var cut = RenderComponent<PushDeliveredQueue.UI.Components.Pages.Home>();

        // Assert
        cut.Markup.Should().Contain("Hello, world!");
    }

    [Fact]
    public void CounterPage_ShouldIncrementCorrectly()
    {
        // Act
        var cut = RenderComponent<PushDeliveredQueue.UI.Components.Pages.Counter>();

        // Find the button and click it
        var button = cut.Find("button");
        button.Click();

        // Assert
        cut.Find("p").TextContent.Should().Contain("Current count: 1");
    }

    [Fact]
    public void WeatherPage_ShouldDisplayWeatherData()
    {
        // Act
        var cut = RenderComponent<PushDeliveredQueue.UI.Components.Pages.Weather>();

        // Assert
        cut.Markup.Should().Contain("Weather");
    }

    [Fact]
    public void ErrorPage_ShouldDisplayErrorInformation()
    {
        // Act
        var cut = RenderComponent<PushDeliveredQueue.UI.Components.Pages.Error>();

        // Assert
        cut.Markup.Should().Contain("Error");
    }

    [Fact]
    public void AppComponent_ShouldRenderWithoutErrors()
    {
        // Act
        var cut = RenderComponent<PushDeliveredQueue.UI.Components.App>();

        // Assert
        cut.Markup.Should().NotBeEmpty();
    }

    [Fact]
    public void Layout_ShouldContainNavigation()
    {
        // Act
        var cut = RenderComponent<PushDeliveredQueue.UI.Components.Layout.MainLayout>();

        // Assert
        cut.Markup.Should().Contain("nav");
    }
}
