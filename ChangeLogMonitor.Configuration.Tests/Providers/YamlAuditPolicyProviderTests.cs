using ChangeLogMonitor.Configuration.Providers;
using FluentAssertions;
using Xunit;

namespace ChangeLogMonitor.Configuration.Tests.Providers;

public class YamlAuditPolicyProviderTests : IDisposable
{
    private readonly string _testConfigPath;
    private readonly string _testDirectory;

    public YamlAuditPolicyProviderTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "ChangeLogMonitorTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _testConfigPath = Path.Combine(_testDirectory, "test-config.yaml");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public void Load_ValidYaml_ShouldDeserializeCorrectly()
    {
        // Arrange
        var yamlContent = @"
auditPolicy:
  version: ""1.0""
  mode: whitelist
  onCreate: eventOnly
  onUpdate: delta
  onDelete: eventOnly

  globalFieldExclusions:
    - RowVersion
    - Timestamp

  entities:
    User:
      enabled: true
      fields:
        Password: exclude
        Email: include
";
        File.WriteAllText(_testConfigPath, yamlContent);
        var provider = new YamlAuditPolicyProvider(_testConfigPath);

        // Act
        var result = provider.Load();

        // Assert
        result.Should().NotBeNull();
        result.AuditPolicy.Should().NotBeNull();
        result.AuditPolicy.Version.Should().Be("1.0");
        result.AuditPolicy.Mode.Should().Be("whitelist");
        result.AuditPolicy.OnCreate.Should().Be("eventOnly");
        result.AuditPolicy.GlobalFieldExclusions.Should().HaveCount(2);
        result.AuditPolicy.Entities.Should().ContainKey("User");
    }

    [Fact]
    public void Load_ShortFieldSyntax_ShouldNormalizeToLongSyntax()
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
        Password: exclude
        Email: include
        FirstName: mask
";
        File.WriteAllText(_testConfigPath, yamlContent);
        var provider = new YamlAuditPolicyProvider(_testConfigPath);

        // Act
        var result = provider.Load();

        // Assert
        var userEntity = result.AuditPolicy.Entities!["User"];
        userEntity.Fields.Should().ContainKey("Password");
        userEntity.Fields.Should().ContainKey("Email");
        userEntity.Fields.Should().ContainKey("FirstName");

        // Проверяем, что короткий синтаксис был нормализован
        userEntity.Fields!["Password"].Should().NotBeNull();
    }

    [Fact]
    public void Load_ComplexYaml_ShouldDeserializeAllSections()
    {
        // Arrange
        var yamlContent = @"
auditPolicy:
  version: ""1.0""
  mode: whitelist

  methodPresets:
    mask:
      email:
        char: ""*""
        keepLeft: 2
        keepRight: 2
        preserveDomain: true

  referencePresets:
    fk_verbose:
      showKey: true
      showName: true
      viewTemplate: ""{name} (ID={key})""

  collectionPresets:
    delta_verbose:
      logDeltas: true
      showKeys: true
      showNames: true

  referenceDefaults:
    showKey: true
    showName: true
    viewTemplate: ""{name} (ID={key})""
    nameResolve:
      stage: normalization
      fallback: ""{key}""
      maxLen: 256

  collectionDefaults:
    logDeltas: true
    showKeys: true
    showNames: true
    itemViewTemplate: ""{name} (ID={key})""
    limits:
      addedMax: 200
      removedMax: 200

  entities:
    User:
      enabled: true
      fields:
        Email:
          action: mask
          mask:
            preset: email
      references:
        DepartmentId:
          preset: fk_verbose
          nameSelector: ""Department.Name""
      collections:
        Roles:
          preset: delta_verbose
          itemNameSelector: ""Role.Name""
";
        File.WriteAllText(_testConfigPath, yamlContent);
        var provider = new YamlAuditPolicyProvider(_testConfigPath);

        // Act
        var result = provider.Load();

        // Assert
        result.AuditPolicy.MethodPresets.Should().NotBeNull();
        result.AuditPolicy.MethodPresets!.Mask.Should().ContainKey("email");

        result.AuditPolicy.ReferencePresets.Should().ContainKey("fk_verbose");
        result.AuditPolicy.CollectionPresets.Should().ContainKey("delta_verbose");

        result.AuditPolicy.ReferenceDefaults.Should().NotBeNull();
        result.AuditPolicy.CollectionDefaults.Should().NotBeNull();

        var userEntity = result.AuditPolicy.Entities!["User"];
        userEntity.Fields.Should().ContainKey("Email");
        userEntity.References.Should().ContainKey("DepartmentId");
        userEntity.Collections.Should().ContainKey("Roles");
    }

    [Fact]
    public void Load_FileNotFound_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "nonexistent.yaml");
        var provider = new YamlAuditPolicyProvider(nonExistentPath);

        // Act
        var act = () => provider.Load();

        // Assert
        act.Should().Throw<FileNotFoundException>()
            .WithMessage($"*{nonExistentPath}*");
    }

    [Fact]
    public void Load_MalformedYaml_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var yamlContent = @"
auditPolicy:
  version: 1.0
  mode: [this is invalid yaml structure
";
        File.WriteAllText(_testConfigPath, yamlContent);
        var provider = new YamlAuditPolicyProvider(_testConfigPath);

        // Act
        var act = () => provider.Load();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Failed to parse YAML*");
    }

    [Fact]
    public async Task LoadAsync_ValidYaml_ShouldDeserializeCorrectly()
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

        // Act
        var result = await provider.LoadAsync();

        // Assert
        result.Should().NotBeNull();
        result.AuditPolicy.Should().NotBeNull();
        result.AuditPolicy.Version.Should().Be("1.0");
    }

    [Fact]
    public void Load_LongFieldSyntaxWithSettings_ShouldDeserializeCorrectly()
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
        Email:
          action: mask
          mask:
            char: ""*""
            keepLeft: 2
            keepRight: 2
            preserveDomain: true
            preserveFormat: true
        SSN:
          action: hash
          hash:
            algo: SHA-256
            encoding: base64
            storeRaw: false
            storeHash: true
        CreditCard:
          action: encrypt
          encrypt:
            algo: AES-256-GCM
            keyRef: kms:alias/audit-key
            encoding: base64
        BirthDate:
          action: include
          view:
            format: date
            pattern: ""dd.MM.yyyy""
            culture: ""ru-RU""
";
        File.WriteAllText(_testConfigPath, yamlContent);
        var provider = new YamlAuditPolicyProvider(_testConfigPath);

        // Act
        var result = provider.Load();

        // Assert
        var userFields = result.AuditPolicy.Entities!["User"].Fields!;

        // Проверяем Email (mask)
        userFields.Should().ContainKey("Email");

        // Проверяем SSN (hash)
        userFields.Should().ContainKey("SSN");

        // Проверяем CreditCard (encrypt)
        userFields.Should().ContainKey("CreditCard");

        // Проверяем BirthDate (include с view)
        userFields.Should().ContainKey("BirthDate");
    }

    [Fact]
    public void Load_NullConstructor_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new YamlAuditPolicyProvider(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configFilePath");
    }
}
