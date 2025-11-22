using ChangeLogMonitor.Configuration.Providers;
using ChangeLogMonitor.Configuration.Services;
using ChangeLogMonitor.Core.Enums;
using FluentAssertions;
using FluentValidation;
using Xunit;

namespace ChangeLogMonitor.Configuration.Tests.Services;

public class AuditConfigurationServiceTests : IDisposable
{
    private readonly string _testConfigPath;
    private readonly string _testDirectory;

    public AuditConfigurationServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "ChangeLogMonitorTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _testConfigPath = Path.Combine(_testDirectory, "test-config.yaml");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory)) Directory.Delete(_testDirectory, true);
    }

    [Fact]
    public void GetPolicy_ValidConfiguration_ShouldReturnPolicy()
    {
        // Arrange
        var yamlContent = @"
auditPolicy:
  version: ""1.0""
  mode: whitelist
  onCreate: eventOnly
  onUpdate: delta
  onDelete: eventOnly

  entities:
    User:
      enabled: true
      fields:
        Password: exclude
        Email: include
";
        File.WriteAllText(_testConfigPath, yamlContent);

        var provider = new YamlAuditPolicyProvider(_testConfigPath);
        var service = new AuditConfigurationService(provider);

        // Act
        var policy = service.GetPolicy();

        // Assert
        policy.Should().NotBeNull();
        policy.Version.Should().Be("1.0");
        policy.Mode.Should().Be(AuditMode.Whitelist);
        policy.Entities.Should().ContainKey("User");
    }

    [Fact]
    public void GetPolicy_CachesResult_ShouldReturnSameInstance()
    {
        // Arrange
        var yamlContent = @"
auditPolicy:
  version: ""1.0""
  mode: whitelist
";
        File.WriteAllText(_testConfigPath, yamlContent);

        var provider = new YamlAuditPolicyProvider(_testConfigPath);
        var service = new AuditConfigurationService(provider);

        // Act
        var policy1 = service.GetPolicy();
        var policy2 = service.GetPolicy();

        // Assert
        policy1.Should().BeSameAs(policy2);
    }

    [Fact]
    public void GetPolicy_ForceReload_ShouldReloadConfiguration()
    {
        // Arrange
        var yamlContent1 = @"
auditPolicy:
  version: ""1.0""
  mode: whitelist
";
        File.WriteAllText(_testConfigPath, yamlContent1);

        var provider = new YamlAuditPolicyProvider(_testConfigPath);
        var service = new AuditConfigurationService(provider);

        var policy1 = service.GetPolicy();

        // Изменяем конфигурацию
        var yamlContent2 = @"
auditPolicy:
  version: ""2.0""
  mode: blacklist
";
        File.WriteAllText(_testConfigPath, yamlContent2);

        // Act
        var policy2 = service.GetPolicy(true);

        // Assert
        policy1.Version.Should().Be("1.0");
        policy2.Version.Should().Be("2.0");
        policy2.Should().NotBeSameAs(policy1);
    }

    [Fact]
    public void GetPolicy_InvalidYaml_ShouldThrowValidationException()
    {
        // Arrange
        var yamlContent = @"
auditPolicy:
  version: ""1.0""
  mode: invalid_mode
";
        File.WriteAllText(_testConfigPath, yamlContent);

        var provider = new YamlAuditPolicyProvider(_testConfigPath);
        var service = new AuditConfigurationService(provider);

        // Act
        var act = () => service.GetPolicy();

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*validation failed*");
    }

    [Fact]
    public void GetPolicy_FileNotFound_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.yaml");
        var provider = new YamlAuditPolicyProvider(nonExistentPath);
        var service = new AuditConfigurationService(provider);

        // Act
        var act = () => service.GetPolicy();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public void GetEntityPolicy_ExistingEntity_ShouldReturnEntityPolicy()
    {
        // Arrange
        var yamlContent = @"
auditPolicy:
  version: ""1.0""
  mode: whitelist

  entities:
    User:
      enabled: true
      fields:
        Email: include
";
        File.WriteAllText(_testConfigPath, yamlContent);

        var provider = new YamlAuditPolicyProvider(_testConfigPath);
        var service = new AuditConfigurationService(provider);

        // Act
        var entityPolicy = service.GetEntityPolicy("User");

        // Assert
        entityPolicy.Should().NotBeNull();
        entityPolicy!.Enabled.Should().BeTrue();
        entityPolicy.Fields.Should().ContainKey("Email");
    }

    [Fact]
    public void GetEntityPolicy_NonExistingEntity_ShouldReturnNull()
    {
        // Arrange
        var yamlContent = @"
auditPolicy:
  version: ""1.0""
  mode: whitelist
";
        File.WriteAllText(_testConfigPath, yamlContent);

        var provider = new YamlAuditPolicyProvider(_testConfigPath);
        var service = new AuditConfigurationService(provider);

        // Act
        var entityPolicy = service.GetEntityPolicy("NonExistent");

        // Assert
        entityPolicy.Should().BeNull();
    }

    [Theory]
    [InlineData("whitelist", "User", true, true)]
    [InlineData("whitelist", "NonExistent", true, false)]
    [InlineData("blacklist", "User", true, true)]
    [InlineData("blacklist", "User", false, false)]
    [InlineData("blacklist", "NonExistent", true, true)]
    public void IsEntityEnabled_VariousScenarios_ShouldReturnCorrectResult(
        string mode, string entityName, bool enabled, bool expectedResult)
    {
        // Arrange
        var yamlContent = $@"
auditPolicy:
  version: ""1.0""
  mode: {mode}

  entities:
    User:
      enabled: {enabled.ToString().ToLower()}
";
        File.WriteAllText(_testConfigPath, yamlContent);

        var provider = new YamlAuditPolicyProvider(_testConfigPath);
        var service = new AuditConfigurationService(provider);

        // Act
        var result = service.IsEntityEnabled(entityName);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public void ReloadConfiguration_ShouldClearCacheAndReload()
    {
        // Arrange
        var yamlContent1 = @"
auditPolicy:
  version: ""1.0""
  mode: whitelist
";
        File.WriteAllText(_testConfigPath, yamlContent1);

        var provider = new YamlAuditPolicyProvider(_testConfigPath);
        var service = new AuditConfigurationService(provider);

        var policy1 = service.GetPolicy();

        // Изменяем конфигурацию
        var yamlContent2 = @"
auditPolicy:
  version: ""2.0""
  mode: blacklist
";
        File.WriteAllText(_testConfigPath, yamlContent2);

        // Act
        service.ReloadConfiguration();
        var policy2 = service.GetPolicy();

        // Assert
        policy1.Version.Should().Be("1.0");
        policy2.Version.Should().Be("2.0");
        policy2.Should().NotBeSameAs(policy1);
    }

    [Fact]
    public void GetConfigurationInfo_LoadedConfiguration_ShouldReturnInfo()
    {
        // Arrange
        var yamlContent = @"
auditPolicy:
  version: ""1.5""
  mode: whitelist

  entities:
    User:
      enabled: true
    Order:
      enabled: true
";
        File.WriteAllText(_testConfigPath, yamlContent);

        var provider = new YamlAuditPolicyProvider(_testConfigPath);
        var service = new AuditConfigurationService(provider);

        // Загружаем конфигурацию
        service.GetPolicy();

        // Act
        var info = service.GetConfigurationInfo();

        // Assert
        info.IsLoaded.Should().BeTrue();
        info.LoadTime.Should().NotBeNull();
        info.Version.Should().Be("1.5");
        info.Mode.Should().Be("Whitelist");
        info.EntityCount.Should().Be(2);
    }

    [Fact]
    public void GetConfigurationInfo_NotYetLoaded_ShouldReturnEmptyInfo()
    {
        // Arrange
        var yamlContent = @"
auditPolicy:
  version: ""1.0""
  mode: whitelist
";
        File.WriteAllText(_testConfigPath, yamlContent);

        var provider = new YamlAuditPolicyProvider(_testConfigPath);
        var service = new AuditConfigurationService(provider);

        // Act (не загружаем конфигурацию)
        var info = service.GetConfigurationInfo();

        // Assert
        info.IsLoaded.Should().BeFalse();
        info.LoadTime.Should().BeNull();
        info.Version.Should().BeNull();
        info.EntityCount.Should().Be(0);
    }

    [Fact]
    public async Task GetPolicyAsync_ValidConfiguration_ShouldReturnPolicy()
    {
        // Arrange
        var yamlContent = @"
auditPolicy:
  version: ""1.0""
  mode: whitelist

  entities:
    User:
      enabled: true
";
        File.WriteAllText(_testConfigPath, yamlContent);

        var provider = new YamlAuditPolicyProvider(_testConfigPath);
        var service = new AuditConfigurationService(provider);

        // Act
        var policy = await service.GetPolicyAsync();

        // Assert
        policy.Should().NotBeNull();
        policy.Version.Should().Be("1.0");
        policy.Entities.Should().ContainKey("User");
    }
}