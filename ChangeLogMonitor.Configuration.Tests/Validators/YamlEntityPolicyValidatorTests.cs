using ChangeLogMonitor.Configuration.Models;
using ChangeLogMonitor.Configuration.Validators;
using FluentAssertions;
using Xunit;

namespace ChangeLogMonitor.Configuration.Tests.Validators;

public class YamlEntityPolicyValidatorTests
{
    private readonly YamlEntityPolicyValidator _validator;

    public YamlEntityPolicyValidatorTests()
    {
        _validator = new YamlEntityPolicyValidator();
    }

    [Fact]
    public void Validate_ValidEntityPolicy_ShouldPass()
    {
        // Arrange
        var entity = new YamlEntityPolicy
        {
            Enabled = true,
            OnCreate = "allFields",
            OnUpdate = "delta",
            OnDelete = "eventOnly"
        };

        // Act
        var result = _validator.Validate(entity);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("eventOnly")]
    [InlineData("allFields")]
    [InlineData("nonDefaultFields")]
    public void Validate_ValidOnCreate_ShouldPass(string onCreate)
    {
        // Arrange
        var entity = new YamlEntityPolicy { OnCreate = onCreate };

        // Act
        var result = _validator.Validate(entity);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("ALL_FIELDS")]
    public void Validate_InvalidOnCreate_ShouldFail(string onCreate)
    {
        // Arrange
        var entity = new YamlEntityPolicy { OnCreate = onCreate };

        // Act
        var result = _validator.Validate(entity);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "OnCreate");
    }

    [Fact]
    public void Validate_WithValidReferences_ShouldPass()
    {
        // Arrange
        var entity = new YamlEntityPolicy
        {
            References = new Dictionary<string, YamlReferencePolicy>
            {
                ["DepartmentId"] = new()
                {
                    ViewTemplate = "{name} (ID={key})"
                }
            }
        };

        // Act
        var result = _validator.Validate(entity);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithInvalidReferences_ShouldFail()
    {
        // Arrange
        var entity = new YamlEntityPolicy
        {
            References = new Dictionary<string, YamlReferencePolicy>
            {
                ["DepartmentId"] = new()
                {
                    ViewTemplate = "No placeholders"
                }
            }
        };

        // Act
        var result = _validator.Validate(entity);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("ViewTemplate"));
    }

    [Fact]
    public void Validate_WithValidCollections_ShouldPass()
    {
        // Arrange
        var entity = new YamlEntityPolicy
        {
            Collections = new Dictionary<string, YamlCollectionPolicy>
            {
                ["Roles"] = new()
                {
                    Limits = new YamlCollectionLimits
                    {
                        AddedMax = 50,
                        RemovedMax = 50
                    }
                }
            }
        };

        // Act
        var result = _validator.Validate(entity);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithInvalidCollectionLimits_ShouldFail()
    {
        // Arrange
        var entity = new YamlEntityPolicy
        {
            Collections = new Dictionary<string, YamlCollectionPolicy>
            {
                ["Roles"] = new()
                {
                    Limits = new YamlCollectionLimits
                    {
                        AddedMax = -10,
                        RemovedMax = 0
                    }
                }
            }
        };

        // Act
        var result = _validator.Validate(entity);

        // Assert
        result.IsValid.Should().BeFalse();
    }
}