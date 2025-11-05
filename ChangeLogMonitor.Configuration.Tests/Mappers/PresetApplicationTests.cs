using ChangeLogMonitor.Configuration.Mappers;
using ChangeLogMonitor.Configuration.Models;
using FluentAssertions;
using Xunit;

namespace ChangeLogMonitor.Configuration.Tests.Mappers;

/// <summary>
/// Тесты для проверки корректности применения пресетов
/// </summary>
public class PresetApplicationTests
{
    private readonly AuditPolicyMapper _mapper;

    public PresetApplicationTests()
    {
        _mapper = new AuditPolicyMapper();
    }

    [Fact]
    public void MapToDomain_MaskPresetApplication_ShouldApplyPresetValues()
    {
        // Arrange
        var yaml = new YamlAuditPolicy
        {
            Version = "1.0",
            MethodPresets = new YamlMethodPresets
            {
                Mask = new Dictionary<string, YamlMaskPreset>
                {
                    ["email"] = new YamlMaskPreset
                    {
                        Char = "•",
                        KeepLeft = 3,
                        KeepRight = 3,
                        PreserveDomain = true,
                        PreserveFormat = false
                    }
                }
            },
            Entities = new Dictionary<string, YamlEntityPolicy>
            {
                ["User"] = new YamlEntityPolicy
                {
                    Fields = new Dictionary<string, object>
                    {
                        ["Email"] = new YamlFieldPolicy
                        {
                            Action = "mask",
                            Mask = new YamlMaskSettings
                            {
                                Preset = "email"
                                // Локальные значения не указаны - должны взяться из пресета
                            }
                        }
                    }
                }
            }
        };

        // Act
        var domain = _mapper.MapToDomain(yaml);

        // Assert
        var emailField = domain.Entities["User"].Fields["Email"];
        emailField.Mask.Should().NotBeNull();
        emailField.Mask!.MaskChar.Should().Be('•'); // Из пресета
        emailField.Mask.KeepLeft.Should().Be(3); // Из пресета
        emailField.Mask.KeepRight.Should().Be(3); // Из пресета
        emailField.Mask.PreserveDomain.Should().BeTrue(); // Из пресета
        emailField.Mask.PreserveFormat.Should().BeFalse(); // Из пресета
    }

    [Fact]
    public void MapToDomain_MaskPresetWithLocalOverride_ShouldOverridePresetValues()
    {
        // Arrange
        var yaml = new YamlAuditPolicy
        {
            Version = "1.0",
            MethodPresets = new YamlMethodPresets
            {
                Mask = new Dictionary<string, YamlMaskPreset>
                {
                    ["email"] = new YamlMaskPreset
                    {
                        Char = "•",
                        KeepLeft = 3,
                        KeepRight = 3,
                        PreserveDomain = true
                    }
                }
            },
            Entities = new Dictionary<string, YamlEntityPolicy>
            {
                ["User"] = new YamlEntityPolicy
                {
                    Fields = new Dictionary<string, object>
                    {
                        ["Email"] = new YamlFieldPolicy
                        {
                            Action = "mask",
                            Mask = new YamlMaskSettings
                            {
                                Preset = "email",
                                KeepLeft = 1, // Перекрываем пресет
                                KeepRight = 1 // Перекрываем пресет
                                // Char и PreserveDomain должны взяться из пресета
                            }
                        }
                    }
                }
            }
        };

        // Act
        var domain = _mapper.MapToDomain(yaml);

        // Assert
        var emailField = domain.Entities["User"].Fields["Email"];
        emailField.Mask.Should().NotBeNull();
        emailField.Mask!.MaskChar.Should().Be('•'); // Из пресета (не перекрыто)
        emailField.Mask.KeepLeft.Should().Be(1); // Локальное значение перекрыло пресет
        emailField.Mask.KeepRight.Should().Be(1); // Локальное значение перекрыло пресет
        emailField.Mask.PreserveDomain.Should().BeTrue(); // Из пресета (не перекрыто)
    }

    [Fact]
    public void MapToDomain_HashPresetApplication_ShouldApplyPresetValues()
    {
        // Arrange
        var yaml = new YamlAuditPolicy
        {
            Version = "1.0",
            MethodPresets = new YamlMethodPresets
            {
                Hash = new Dictionary<string, YamlHashPreset>
                {
                    ["sha256_salted"] = new YamlHashPreset
                    {
                        Algo = "SHA-256",
                        Salt = new YamlSaltSettings { Strategy = "per-record" },
                        Encoding = "base64",
                        StoreRaw = false,
                        StoreHash = true
                    }
                }
            },
            Entities = new Dictionary<string, YamlEntityPolicy>
            {
                ["User"] = new YamlEntityPolicy
                {
                    Fields = new Dictionary<string, object>
                    {
                        ["SSN"] = new YamlFieldPolicy
                        {
                            Action = "hash",
                            Hash = new YamlHashSettings
                            {
                                Preset = "sha256_salted"
                            }
                        }
                    }
                }
            }
        };

        // Act
        var domain = _mapper.MapToDomain(yaml);

        // Assert
        var ssnField = domain.Entities["User"].Fields["SSN"];
        ssnField.Hash.Should().NotBeNull();
        ssnField.Hash!.Algo.Should().Be("SHA-256");
        ssnField.Hash.Salt.Should().NotBeNull();
        ssnField.Hash.Salt!.Strategy.Should().Be("per-record");
        ssnField.Hash.Encoding.Should().Be("base64");
        ssnField.Hash.StoreRaw.Should().BeFalse();
        ssnField.Hash.StoreHash.Should().BeTrue();
    }

    [Fact]
    public void MapToDomain_ReferencePresetApplication_ShouldApplyPresetValues()
    {
        // Arrange
        var yaml = new YamlAuditPolicy
        {
            Version = "1.0",
            ReferencePresets = new Dictionary<string, YamlReferencePreset>
            {
                ["fk_verbose"] = new YamlReferencePreset
                {
                    ShowKey = true,
                    ShowName = true,
                    ViewTemplate = "{name} (ID={key})"
                }
            },
            Entities = new Dictionary<string, YamlEntityPolicy>
            {
                ["User"] = new YamlEntityPolicy
                {
                    References = new Dictionary<string, YamlReferencePolicy>
                    {
                        ["DepartmentId"] = new YamlReferencePolicy
                        {
                            Preset = "fk_verbose",
                            NameSelector = "Department.Name"
                        }
                    }
                }
            }
        };

        // Act
        var domain = _mapper.MapToDomain(yaml);

        // Assert
        var deptRef = domain.Entities["User"].References["DepartmentId"];
        deptRef.ShowKey.Should().BeTrue(); // Из пресета
        deptRef.ShowName.Should().BeTrue(); // Из пресета
        deptRef.ViewTemplate.Should().Be("{name} (ID={key})"); // Из пресета
        deptRef.NameSelector.Should().Be("Department.Name"); // Локальное значение
    }

    [Fact]
    public void MapToDomain_CollectionPresetApplication_ShouldApplyPresetValues()
    {
        // Arrange
        var yaml = new YamlAuditPolicy
        {
            Version = "1.0",
            CollectionPresets = new Dictionary<string, YamlCollectionPreset>
            {
                ["delta_verbose"] = new YamlCollectionPreset
                {
                    LogDeltas = true,
                    ShowKeys = true,
                    ShowNames = true,
                    ItemViewTemplate = "{name} (ID={key})"
                }
            },
            Entities = new Dictionary<string, YamlEntityPolicy>
            {
                ["User"] = new YamlEntityPolicy
                {
                    Collections = new Dictionary<string, YamlCollectionPolicy>
                    {
                        ["Roles"] = new YamlCollectionPolicy
                        {
                            Preset = "delta_verbose",
                            ItemNameSelector = "Role.Name"
                        }
                    }
                }
            }
        };

        // Act
        var domain = _mapper.MapToDomain(yaml);

        // Assert
        var rolesCollection = domain.Entities["User"].Collections["Roles"];
        rolesCollection.LogDeltas.Should().BeTrue(); // Из пресета
        rolesCollection.ShowKeys.Should().BeTrue(); // Из пресета
        rolesCollection.ShowNames.Should().BeTrue(); // Из пресета
        rolesCollection.ItemViewTemplate.Should().Be("{name} (ID={key})"); // Из пресета
        rolesCollection.ItemNameSelector.Should().Be("Role.Name"); // Локальное значение
    }

    [Fact]
    public void MapToDomain_CollectionPresetWithOverride_ShouldOverridePresetValues()
    {
        // Arrange
        var yaml = new YamlAuditPolicy
        {
            Version = "1.0",
            CollectionPresets = new Dictionary<string, YamlCollectionPreset>
            {
                ["delta_verbose"] = new YamlCollectionPreset
                {
                    LogDeltas = true,
                    ShowKeys = true,
                    ShowNames = true
                }
            },
            Entities = new Dictionary<string, YamlEntityPolicy>
            {
                ["User"] = new YamlEntityPolicy
                {
                    Collections = new Dictionary<string, YamlCollectionPolicy>
                    {
                        ["Roles"] = new YamlCollectionPolicy
                        {
                            Preset = "delta_verbose",
                            ShowKeys = false, // Перекрываем пресет
                            ItemNameSelector = "Role.Name"
                        }
                    }
                }
            }
        };

        // Act
        var domain = _mapper.MapToDomain(yaml);

        // Assert
        var rolesCollection = domain.Entities["User"].Collections["Roles"];
        rolesCollection.LogDeltas.Should().BeTrue(); // Из пресета
        rolesCollection.ShowKeys.Should().BeFalse(); // Перекрыто локально
        rolesCollection.ShowNames.Should().BeTrue(); // Из пресета
    }

    [Fact]
    public void MapToDomain_NonExistentPreset_ShouldNotCrash()
    {
        // Arrange
        var yaml = new YamlAuditPolicy
        {
            Version = "1.0",
            Entities = new Dictionary<string, YamlEntityPolicy>
            {
                ["User"] = new YamlEntityPolicy
                {
                    Fields = new Dictionary<string, object>
                    {
                        ["Email"] = new YamlFieldPolicy
                        {
                            Action = "mask",
                            Mask = new YamlMaskSettings
                            {
                                Preset = "nonexistent_preset",
                                KeepLeft = 2
                            }
                        }
                    }
                }
            }
        };

        // Act
        var act = () => _mapper.MapToDomain(yaml);

        // Assert
        act.Should().NotThrow();
        var domain = act();
        var emailField = domain.Entities["User"].Fields["Email"];
        emailField.Mask.Should().NotBeNull();
        emailField.Mask!.KeepLeft.Should().Be(2); // Локальное значение применено
    }
}
