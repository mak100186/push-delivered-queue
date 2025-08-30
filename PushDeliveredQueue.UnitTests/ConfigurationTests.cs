using System.ComponentModel.DataAnnotations;

using FluentAssertions;

using PushDeliveredQueue.Core.Configs;

using Xunit;

namespace PushDeliveredQueue.UnitTests;

public class ConfigurationTests
{
    [Fact]
    public void SubscribableQueueOptions_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var options = new SubscribableQueueOptions();

        // Assert
        options.Ttl.Should().Be(default);
        options.RetryCount.Should().Be(0);
        options.DelayBetweenRetriesMs.Should().Be(0);
    }

    [Fact]
    public void SubscribableQueueOptions_WithValidValues_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var ttl = TimeSpan.FromMinutes(5);
        var retryCount = 3;
        var delayMs = 200;

        // Act
        var options = new SubscribableQueueOptions
        {
            Ttl = ttl,
            RetryCount = retryCount,
            DelayBetweenRetriesMs = delayMs
        };

        // Assert
        options.Ttl.Should().Be(ttl);
        options.RetryCount.Should().Be(retryCount);
        options.DelayBetweenRetriesMs.Should().Be(delayMs);
    }

    [Fact]
    public void SubscribableQueueOptions_TtlProperty_ShouldHaveRequiredAttribute()
    {
        // Arrange
        var property = typeof(SubscribableQueueOptions).GetProperty(nameof(SubscribableQueueOptions.Ttl));

        // Act & Assert
        property.Should().NotBeNull();
        property!.GetCustomAttributes(typeof(RequiredAttribute), true).Should().HaveCount(1);
    }

    [Fact]
    public void SubscribableQueueOptions_RetryCountProperty_ShouldHaveRangeAttribute()
    {
        // Arrange
        var property = typeof(SubscribableQueueOptions).GetProperty(nameof(SubscribableQueueOptions.RetryCount));

        // Act & Assert
        property.Should().NotBeNull();
        var rangeAttribute = property!.GetCustomAttributes(typeof(RangeAttribute), true).FirstOrDefault() as RangeAttribute;
        rangeAttribute.Should().NotBeNull();
        rangeAttribute!.Minimum.Should().Be(1);
        rangeAttribute.Maximum.Should().Be(100);
    }

    [Fact]
    public void SubscribableQueueOptions_DelayBetweenRetriesMsProperty_ShouldHaveRangeAttribute()
    {
        // Arrange
        var property = typeof(SubscribableQueueOptions).GetProperty(nameof(SubscribableQueueOptions.DelayBetweenRetriesMs));

        // Act & Assert
        property.Should().NotBeNull();
        var rangeAttribute = property!.GetCustomAttributes(typeof(RangeAttribute), true).FirstOrDefault() as RangeAttribute;
        rangeAttribute.Should().NotBeNull();
        rangeAttribute!.Minimum.Should().Be(10);
        rangeAttribute.Maximum.Should().Be(1000);
    }

    [Theory]
    [InlineData(0, false)] // Below minimum
    [InlineData(1, true)]  // Minimum
    [InlineData(50, true)] // Valid
    [InlineData(100, true)] // Maximum
    [InlineData(101, false)] // Above maximum
    public void SubscribableQueueOptions_RetryCountValidation_ShouldWorkCorrectly(int retryCount, bool shouldBeValid)
    {
        // Arrange
        var options = new SubscribableQueueOptions
        {
            Ttl = TimeSpan.FromMinutes(1),
            RetryCount = retryCount,
            DelayBetweenRetriesMs = 100
        };

        var validationContext = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, validationContext, validationResults, true);

        // Assert
        isValid.Should().Be(shouldBeValid);
        if (!shouldBeValid)
        {
            validationResults.Should().ContainSingle();
            validationResults[0].MemberNames.Should().Contain(nameof(SubscribableQueueOptions.RetryCount));
        }
    }

    [Theory]
    [InlineData(9, false)]  // Below minimum
    [InlineData(10, true)]  // Minimum
    [InlineData(500, true)] // Valid
    [InlineData(1000, true)] // Maximum
    [InlineData(1001, false)] // Above maximum
    public void SubscribableQueueOptions_DelayBetweenRetriesMsValidation_ShouldWorkCorrectly(int delayMs, bool shouldBeValid)
    {
        // Arrange
        var options = new SubscribableQueueOptions
        {
            Ttl = TimeSpan.FromMinutes(1),
            RetryCount = 5,
            DelayBetweenRetriesMs = delayMs
        };

        var validationContext = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, validationContext, validationResults, true);

        // Assert
        isValid.Should().Be(shouldBeValid);
        if (!shouldBeValid)
        {
            validationResults.Should().ContainSingle();
            validationResults[0].MemberNames.Should().Contain(nameof(SubscribableQueueOptions.DelayBetweenRetriesMs));
        }
    }

    [Fact]
    public void SubscribableQueueOptions_WithMissingTtl_ShouldPassValidation()
    {
        // Arrange
        var options = new SubscribableQueueOptions
        {
            // Ttl not set (default value)
            RetryCount = 5,
            DelayBetweenRetriesMs = 100
        };

        var validationContext = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, validationContext, validationResults, true);

        // Assert
        isValid.Should().BeTrue();
        validationResults.Should().BeEmpty();
        // Note: Required attribute doesn't work as expected for TimeSpan default value
    }

    [Fact]
    public void SubscribableQueueOptions_WithValidConfiguration_ShouldPassValidation()
    {
        // Arrange
        var options = new SubscribableQueueOptions
        {
            Ttl = TimeSpan.FromMinutes(5),
            RetryCount = 3,
            DelayBetweenRetriesMs = 200
        };

        var validationContext = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, validationContext, validationResults, true);

        // Assert
        isValid.Should().BeTrue();
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void SubscribableQueueOptions_Equality_ShouldWorkCorrectly()
    {
        // Arrange
        var options1 = new SubscribableQueueOptions
        {
            Ttl = TimeSpan.FromMinutes(5),
            RetryCount = 3,
            DelayBetweenRetriesMs = 200
        };

        var options2 = new SubscribableQueueOptions
        {
            Ttl = TimeSpan.FromMinutes(5),
            RetryCount = 3,
            DelayBetweenRetriesMs = 200
        };

        var options3 = new SubscribableQueueOptions
        {
            Ttl = TimeSpan.FromMinutes(10),
            RetryCount = 3,
            DelayBetweenRetriesMs = 200
        };

        // Act & Assert
        options1.Should().BeEquivalentTo(options2);
        options1.Should().NotBeEquivalentTo(options3);
    }

    [Fact]
    public void SubscribableQueueOptions_WithZeroTtl_ShouldPassValidation()
    {
        // Arrange
        var options = new SubscribableQueueOptions
        {
            Ttl = TimeSpan.Zero,
            RetryCount = 5,
            DelayBetweenRetriesMs = 100
        };

        var validationContext = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, validationContext, validationResults, true);

        // Assert
        isValid.Should().BeTrue();
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void SubscribableQueueOptions_WithNegativeTtl_ShouldPassValidation()
    {
        // Arrange
        var options = new SubscribableQueueOptions
        {
            Ttl = TimeSpan.FromMinutes(-5),
            RetryCount = 5,
            DelayBetweenRetriesMs = 100
        };

        var validationContext = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, validationContext, validationResults, true);

        // Assert
        isValid.Should().BeTrue();
        validationResults.Should().BeEmpty();
    }
}
