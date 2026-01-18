using ChangeLogMonitor.Configuration.Models;
using ChangeLogMonitor.Configuration.Validators;
using FluentAssertions;
using Xunit;

namespace ChangeLogMonitor.Configuration.Tests.Validators;

public class YamlAuditPolicyValidatorTests
{
    private readonly YamlAuditPolicyValidator _validator;

    public YamlAuditPolicyValidatorTests()
    {
        _validator = new YamlAuditPolicyValidator();
    }

    [Fact]
    public void Validate_ValidPolicy_ShouldPass()
    {
        var policy = new YamlAuditPolicy
        {
            Version = "1.0",
            Mode = "whitelist",
            OnCreate = "eventOnly",
            OnUpdate = "delta",
            OnDelete = "eventOnly"
        };

        var result = _validator.Validate(policy);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MissingVersion_ShouldFail()
    {
        var policy = new YamlAuditPolicy
        {
            Version = null,
            Mode = "whitelist"
        };

        var result = _validator.Validate(policy);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Version");
        result.Errors.First().ErrorMessage.Should().Contain("Version is required");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("WHITELIST")]
    [InlineData("Blacklist")]
    public void Validate_InvalidMode_ShouldFail(string mode)
    {
        var policy = new YamlAuditPolicy
        {
            Version = "1.0",
            Mode = mode
        };

        var result = _validator.Validate(policy);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "Mode");
        result.Errors.First().ErrorMessage.Should().Contain("Mode must be one of");
    }

    [Theory]
    [InlineData("whitelist")]
    [InlineData("blacklist")]
    public void Validate_ValidMode_ShouldPass(string mode)
    {
        var policy = new YamlAuditPolicy
        {
            Version = "1.0",
            Mode = mode
        };

        var result = _validator.Validate(policy);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("EVENT_ONLY")]
    public void Validate_InvalidOnCreate_ShouldFail(string onCreate)
    {
        var policy = new YamlAuditPolicy
        {
            Version = "1.0",
            OnCreate = onCreate
        };

        var result = _validator.Validate(policy);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "OnCreate");
    }

    [Theory]
    [InlineData("eventOnly")]
    [InlineData("allFields")]
    [InlineData("nonDefaultFields")]
    public void Validate_ValidOnCreate_ShouldPass(string onCreate)
    {
        var policy = new YamlAuditPolicy
        {
            Version = "1.0",
            OnCreate = onCreate
        };

        var result = _validator.Validate(policy);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("DELTA")]
    public void Validate_InvalidOnUpdate_ShouldFail(string onUpdate)
    {
        var policy = new YamlAuditPolicy
        {
            Version = "1.0",
            OnUpdate = onUpdate
        };

        var result = _validator.Validate(policy);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "OnUpdate");
    }

    [Theory]
    [InlineData("delta")]
    [InlineData("fullSnapshot")]
    public void Validate_ValidOnUpdate_ShouldPass(string onUpdate)
    {
        var policy = new YamlAuditPolicy
        {
            Version = "1.0",
            OnUpdate = onUpdate
        };

        var result = _validator.Validate(policy);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidReferenceDefaultsTemplate_ShouldFail()
    {
        var policy = new YamlAuditPolicy
        {
            Version = "1.0",
            ReferenceDefaults = new YamlReferenceDefaults
            {
                ViewTemplate = "Invalid template without placeholders"
            }
        };

        var result = _validator.Validate(policy);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == "ReferenceDefaults.ViewTemplate");
        result.Errors.First().ErrorMessage.Should().Contain("{name}");
        result.Errors.First().ErrorMessage.Should().Contain("{key}");
    }

    [Theory]
    [InlineData("{name}")]
    [InlineData("{key}")]
    [InlineData("{name} (ID={key})")]
    public void Validate_ValidReferenceDefaultsTemplate_ShouldPass(string template)
    {
        var policy = new YamlAuditPolicy
        {
            Version = "1.0",
            ReferenceDefaults = new YamlReferenceDefaults
            {
                ViewTemplate = template
            }
        };

        var result = _validator.Validate(policy);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidCollectionLimits_ShouldFail()
    {
        var policy = new YamlAuditPolicy
        {
            Version = "1.0",
            CollectionDefaults = new YamlCollectionDefaults
            {
                Limits = new YamlCollectionLimits
                {
                    AddedMax = -1,
                    RemovedMax = 0
                }
            }
        };

        var result = _validator.Validate(policy);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void Validate_ValidCollectionLimits_ShouldPass()
    {
        var policy = new YamlAuditPolicy
        {
            Version = "1.0",
            CollectionDefaults = new YamlCollectionDefaults
            {
                Limits = new YamlCollectionLimits
                {
                    AddedMax = 100,
                    RemovedMax = 200
                }
            }
        };

        var result = _validator.Validate(policy);

        result.IsValid.Should().BeTrue();
    }
}