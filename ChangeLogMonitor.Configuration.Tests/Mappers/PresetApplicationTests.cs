using ChangeLogMonitor.Configuration.Mappers;
using ChangeLogMonitor.Configuration.Models;
using FluentAssertions;
using Xunit;

namespace ChangeLogMonitor.Configuration.Tests.Mappers;

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
        var yaml = new YamlAuditPolicy
        {
            Version = "1.0",
            MethodPresets = new YamlMethodPresets
            {
                Mask = new Dictionary<string, YamlMaskPreset>
                {
                    ["email"] = new()
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
                ["User"] = new()
                {
                    Fields = new Dictionary<string, object>
                    {
                        ["Email"] = new YamlFieldPolicy
                        {
                            Action = "mask",
                            Mask = new YamlMaskSettings
                            {
                                Preset = "email"
                            }
                        }
                    }
                }
            }
        };

        var domain = _mapper.MapToDomain(yaml);

        var emailField = domain.Entities["User"].Fields["Email"];
        emailField.Mask.Should().NotBeNull();
        emailField.Mask!.MaskChar.Should().Be('•');
        emailField.Mask.KeepLeft.Should().Be(3);
        emailField.Mask.KeepRight.Should().Be(3);
        emailField.Mask.PreserveDomain.Should().BeTrue();
        emailField.Mask.PreserveFormat.Should().BeFalse();
    }

    [Fact]
    public void MapToDomain_MaskPresetWithLocalOverride_ShouldOverridePresetValues()
    {
        var yaml = new YamlAuditPolicy
        {
            Version = "1.0",
            MethodPresets = new YamlMethodPresets
            {
                Mask = new Dictionary<string, YamlMaskPreset>
                {
                    ["email"] = new()
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
                ["User"] = new()
                {
                    Fields = new Dictionary<string, object>
                    {
                        ["Email"] = new YamlFieldPolicy
                        {
                            Action = "mask",
                            Mask = new YamlMaskSettings
                            {
                                Preset = "email",
                                KeepLeft = 1,
                                KeepRight = 1
                            }
                        }
                    }
                }
            }
        };

        var domain = _mapper.MapToDomain(yaml);

        var emailField = domain.Entities["User"].Fields["Email"];
        emailField.Mask.Should().NotBeNull();
        emailField.Mask!.MaskChar.Should().Be('•');
        emailField.Mask.KeepLeft.Should().Be(1);
        emailField.Mask.KeepRight.Should().Be(1);
        emailField.Mask.PreserveDomain.Should().BeTrue();
    }

    [Fact]
    public void MapToDomain_HashPresetApplication_ShouldApplyPresetValues()
    {
        var yaml = new YamlAuditPolicy
        {
            Version = "1.0",
            MethodPresets = new YamlMethodPresets
            {
                Hash = new Dictionary<string, YamlHashPreset>
                {
                    ["sha256_salted"] = new()
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
                ["User"] = new()
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

        var domain = _mapper.MapToDomain(yaml);

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
        var yaml = new YamlAuditPolicy
        {
            Version = "1.0",
            ReferencePresets = new Dictionary<string, YamlReferencePreset>
            {
                ["fk_verbose"] = new()
                {
                    ShowKey = true,
                    ShowName = true,
                    ViewTemplate = "{name} (ID={key})"
                }
            },
            Entities = new Dictionary<string, YamlEntityPolicy>
            {
                ["User"] = new()
                {
                    References = new Dictionary<string, YamlReferencePolicy>
                    {
                        ["DepartmentId"] = new()
                        {
                            Preset = "fk_verbose",
                            NameSelector = "Department.Name"
                        }
                    }
                }
            }
        };

        var domain = _mapper.MapToDomain(yaml);

        var deptRef = domain.Entities["User"].References["DepartmentId"];
        deptRef.ShowKey.Should().BeTrue();
        deptRef.ShowName.Should().BeTrue();
        deptRef.ViewTemplate.Should().Be("{name} (ID={key})");
        deptRef.NameSelector.Should().Be("Department.Name");
    }

    [Fact]
    public void MapToDomain_CollectionPresetApplication_ShouldApplyPresetValues()
    {
        var yaml = new YamlAuditPolicy
        {
            Version = "1.0",
            CollectionPresets = new Dictionary<string, YamlCollectionPreset>
            {
                ["delta_verbose"] = new()
                {
                    LogDeltas = true,
                    ShowKeys = true,
                    ShowNames = true,
                    ItemViewTemplate = "{name} (ID={key})"
                }
            },
            Entities = new Dictionary<string, YamlEntityPolicy>
            {
                ["User"] = new()
                {
                    Collections = new Dictionary<string, YamlCollectionPolicy>
                    {
                        ["Roles"] = new()
                        {
                            Preset = "delta_verbose",
                            ItemNameSelector = "Role.Name"
                        }
                    }
                }
            }
        };

        var domain = _mapper.MapToDomain(yaml);

        var rolesCollection = domain.Entities["User"].Collections["Roles"];
        rolesCollection.LogDeltas.Should().BeTrue();
        rolesCollection.ShowKeys.Should().BeTrue();
        rolesCollection.ShowNames.Should().BeTrue();
        rolesCollection.ItemViewTemplate.Should().Be("{name} (ID={key})");
        rolesCollection.ItemNameSelector.Should().Be("Role.Name");
    }

    [Fact]
    public void MapToDomain_CollectionPresetWithOverride_ShouldOverridePresetValues()
    {
        var yaml = new YamlAuditPolicy
        {
            Version = "1.0",
            CollectionPresets = new Dictionary<string, YamlCollectionPreset>
            {
                ["delta_verbose"] = new()
                {
                    LogDeltas = true,
                    ShowKeys = true,
                    ShowNames = true
                }
            },
            Entities = new Dictionary<string, YamlEntityPolicy>
            {
                ["User"] = new()
                {
                    Collections = new Dictionary<string, YamlCollectionPolicy>
                    {
                        ["Roles"] = new()
                        {
                            Preset = "delta_verbose",
                            ShowKeys = false,
                            ItemNameSelector = "Role.Name"
                        }
                    }
                }
            }
        };

        var domain = _mapper.MapToDomain(yaml);

        var rolesCollection = domain.Entities["User"].Collections["Roles"];
        rolesCollection.LogDeltas.Should().BeTrue();
        rolesCollection.ShowKeys.Should().BeFalse();
        rolesCollection.ShowNames.Should().BeTrue();
    }

    [Fact]
    public void MapToDomain_NonExistentPreset_ShouldNotCrash()
    {
        var yaml = new YamlAuditPolicy
        {
            Version = "1.0",
            Entities = new Dictionary<string, YamlEntityPolicy>
            {
                ["User"] = new()
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

        var act = () => _mapper.MapToDomain(yaml);

        act.Should().NotThrow();
        var domain = act();
        var emailField = domain.Entities["User"].Fields["Email"];
        emailField.Mask.Should().NotBeNull();
        emailField.Mask!.KeepLeft.Should().Be(2);
    }
}