using ChangeLogMonitor.Configuration.Mappers;
using ChangeLogMonitor.Configuration.Models;
using ChangeLogMonitor.Core.Enums;
using FluentAssertions;
using Xunit;

namespace ChangeLogMonitor.Configuration.Tests.Mappers;

public class AuditPolicyMapperTests
{
    private readonly AuditPolicyMapper _mapper;

    public AuditPolicyMapperTests()
    {
        _mapper = new AuditPolicyMapper();
    }

    [Fact]
    public void MapToDomain_BasicPolicy_ShouldMapCorrectly()
    {
        var yaml = new YamlAuditPolicy
        {
            Version = "1.5",
            Mode = "whitelist",
            OnCreate = "allFields",
            OnUpdate = "delta",
            OnDelete = "eventOnly",
            GlobalFieldExclusions = new List<string> { "RowVersion", "Timestamp" },
            DefaultCulture = "ru-RU",
            DefaultTimeZone = "Asia/Almaty"
        };

        var domain = _mapper.MapToDomain(yaml);

        domain.Should().NotBeNull();
        domain.Version.Should().Be("1.5");
        domain.Mode.Should().Be(AuditMode.Whitelist);
        domain.OnCreate.Should().Be(CreateBehavior.AllFields);
        domain.OnUpdate.Should().Be(UpdateBehavior.Delta);
        domain.OnDelete.Should().Be(DeleteBehavior.EventOnly);
        domain.GlobalFieldExclusions.Should().BeEquivalentTo("RowVersion", "Timestamp");
        domain.DefaultCulture.Should().Be("ru-RU");
        domain.DefaultTimeZone.Should().Be("Asia/Almaty");
    }

    [Theory]
    [InlineData("whitelist", AuditMode.Whitelist)]
    [InlineData("blacklist", AuditMode.Blacklist)]
    [InlineData("WHITELIST", AuditMode.Whitelist)]
    [InlineData("Blacklist", AuditMode.Blacklist)]
    public void MapToDomain_AuditMode_ShouldParseCorrectly(string yamlMode, AuditMode expectedMode)
    {
        var yaml = new YamlAuditPolicy
        {
            Version = "1.0",
            Mode = yamlMode
        };

        var domain = _mapper.MapToDomain(yaml);

        domain.Mode.Should().Be(expectedMode);
    }

    [Theory]
    [InlineData("eventOnly", CreateBehavior.EventOnly)]
    [InlineData("allFields", CreateBehavior.AllFields)]
    [InlineData("nonDefaultFields", CreateBehavior.NonDefaultFields)]
    [InlineData("eventonly", CreateBehavior.EventOnly)]
    public void MapToDomain_CreateBehavior_ShouldParseCorrectly(string yamlValue, CreateBehavior expected)
    {
        var yaml = new YamlAuditPolicy
        {
            Version = "1.0",
            OnCreate = yamlValue
        };

        var domain = _mapper.MapToDomain(yaml);

        domain.OnCreate.Should().Be(expected);
    }

    [Fact]
    public void MapToDomain_FieldPolicyWithAction_ShouldMapCorrectly()
    {
        var yaml = new YamlAuditPolicy
        {
            Version = "1.0",
            Entities = new Dictionary<string, YamlEntityPolicy>
            {
                ["User"] = new()
                {
                    Enabled = true,
                    Fields = new Dictionary<string, object>
                    {
                        ["Email"] = new YamlFieldPolicy
                        {
                            Action = "mask",
                            Mask = new YamlMaskSettings
                            {
                                Char = "*",
                                KeepLeft = 2,
                                KeepRight = 2,
                                PreserveDomain = true
                            }
                        }
                    }
                }
            }
        };

        var domain = _mapper.MapToDomain(yaml);

        domain.Entities.Should().ContainKey("User");
        var userEntity = domain.Entities["User"];
        userEntity.Fields.Should().ContainKey("Email");
        var emailField = userEntity.Fields["Email"];
        emailField.Action.Should().Be(FieldAction.Mask);
        emailField.Mask.Should().NotBeNull();
        emailField.Mask!.MaskChar.Should().Be('*');
        emailField.Mask.KeepLeft.Should().Be(2);
        emailField.Mask.KeepRight.Should().Be(2);
        emailField.Mask.PreserveDomain.Should().BeTrue();
    }

    [Fact]
    public void MapToDomain_FieldPolicyWithView_ShouldMapCorrectly()
    {
        var yaml = new YamlAuditPolicy
        {
            Version = "1.0",
            Entities = new Dictionary<string, YamlEntityPolicy>
            {
                ["Order"] = new()
                {
                    Fields = new Dictionary<string, object>
                    {
                        ["TotalAmount"] = new YamlFieldPolicy
                        {
                            Action = "include",
                            View = new YamlViewSettings
                            {
                                Format = "money",
                                Culture = "ru-RU"
                            }
                        }
                    }
                }
            }
        };

        var domain = _mapper.MapToDomain(yaml);

        var orderEntity = domain.Entities["Order"];
        var amountField = orderEntity.Fields["TotalAmount"];
        amountField.Action.Should().Be(FieldAction.Include);
        amountField.View.Should().NotBeNull();
        amountField.View!.Format.Should().Be(FieldType.Money);
        amountField.View.Culture.Should().Be("ru-RU");
    }

    [Fact]
    public void MapToDomain_ReferencePolicy_ShouldMapCorrectly()
    {
        var yaml = new YamlAuditPolicy
        {
            Version = "1.0",
            Entities = new Dictionary<string, YamlEntityPolicy>
            {
                ["User"] = new()
                {
                    References = new Dictionary<string, YamlReferencePolicy>
                    {
                        ["DepartmentId"] = new()
                        {
                            ShowKey = true,
                            ShowName = true,
                            ViewTemplate = "{name} (ID={key})",
                            NameSelector = "Department.Name",
                            NameResolve = new YamlNameResolveSettings
                            {
                                Stage = "normalization",
                                Fallback = "<Unknown>",
                                MaxLen = 256
                            }
                        }
                    }
                }
            }
        };

        var domain = _mapper.MapToDomain(yaml);

        var userEntity = domain.Entities["User"];
        userEntity.References.Should().ContainKey("DepartmentId");
        var deptRef = userEntity.References["DepartmentId"];
        deptRef.ShowKey.Should().BeTrue();
        deptRef.ShowName.Should().BeTrue();
        deptRef.ViewTemplate.Should().Be("{name} (ID={key})");
        deptRef.NameSelector.Should().Be("Department.Name");
        deptRef.NameResolve.Should().NotBeNull();
        deptRef.NameResolve.Stage.Should().Be("normalization");
        deptRef.NameResolve.Fallback.Should().Be("<Unknown>");
        deptRef.NameResolve.MaxLen.Should().Be(256);
    }

    [Fact]
    public void MapToDomain_CollectionPolicy_ShouldMapCorrectly()
    {
        var yaml = new YamlAuditPolicy
        {
            Version = "1.0",
            Entities = new Dictionary<string, YamlEntityPolicy>
            {
                ["User"] = new()
                {
                    Collections = new Dictionary<string, YamlCollectionPolicy>
                    {
                        ["Roles"] = new()
                        {
                            LogDeltas = true,
                            ShowKeys = true,
                            ShowNames = true,
                            ItemKeySelector = "Role.Id",
                            ItemNameSelector = "Role.Name",
                            ItemViewTemplate = "{name} (ID={key})",
                            Limits = new YamlCollectionLimits
                            {
                                AddedMax = 50,
                                RemovedMax = 50
                            },
                            DeltaView = new YamlDeltaViewSettings
                            {
                                AddedPrefix = "Добавлена роль:",
                                RemovedPrefix = "Удалена роль:",
                                Joiner = ", "
                            }
                        }
                    }
                }
            }
        };

        var domain = _mapper.MapToDomain(yaml);

        var userEntity = domain.Entities["User"];
        userEntity.Collections.Should().ContainKey("Roles");
        var rolesCollection = userEntity.Collections["Roles"];
        rolesCollection.LogDeltas.Should().BeTrue();
        rolesCollection.ShowKeys.Should().BeTrue();
        rolesCollection.ShowNames.Should().BeTrue();
        rolesCollection.ItemKeySelector.Should().Be("Role.Id");
        rolesCollection.ItemNameSelector.Should().Be("Role.Name");
        rolesCollection.ItemViewTemplate.Should().Be("{name} (ID={key})");
        rolesCollection.Limits.AddedMax.Should().Be(50);
        rolesCollection.Limits.RemovedMax.Should().Be(50);
        rolesCollection.DeltaView.AddedPrefix.Should().Be("Добавлена роль:");
        rolesCollection.DeltaView.RemovedPrefix.Should().Be("Удалена роль:");
        rolesCollection.DeltaView.Joiner.Should().Be(", ");
    }

    [Fact]
    public void MapToDomain_MaskPresets_ShouldMapCorrectly()
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
                        Char = "*",
                        KeepLeft = 2,
                        KeepRight = 2,
                        PreserveDomain = true,
                        PreserveFormat = true
                    }
                }
            }
        };

        var domain = _mapper.MapToDomain(yaml);

        domain.MaskPresets.Should().ContainKey("email");
        var emailPreset = domain.MaskPresets["email"];
        emailPreset.MaskChar.Should().Be('*');
        emailPreset.KeepLeft.Should().Be(2);
        emailPreset.KeepRight.Should().Be(2);
        emailPreset.PreserveDomain.Should().BeTrue();
        emailPreset.PreserveFormat.Should().BeTrue();
    }

    [Fact]
    public void MapToDomain_HashPresets_ShouldMapCorrectly()
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
                        Salt = new YamlSaltSettings
                        {
                            Strategy = "per-record",
                            Ref = "kms:alias/audit-salt"
                        },
                        PepperRef = "env:AUDIT_PEPPER",
                        Encoding = "base64",
                        StoreRaw = false,
                        StoreHash = true,
                        EqualityToken = true
                    }
                }
            }
        };

        var domain = _mapper.MapToDomain(yaml);

        domain.HashPresets.Should().ContainKey("sha256_salted");
        var hashPreset = domain.HashPresets["sha256_salted"];
        hashPreset.Algo.Should().Be("SHA-256");
        hashPreset.Salt.Should().NotBeNull();
        hashPreset.Salt!.Strategy.Should().Be("per-record");
        hashPreset.Salt.Ref.Should().Be("kms:alias/audit-salt");
        hashPreset.PepperRef.Should().Be("env:AUDIT_PEPPER");
        hashPreset.Encoding.Should().Be("base64");
        hashPreset.StoreRaw.Should().BeFalse();
        hashPreset.StoreHash.Should().BeTrue();
        hashPreset.EqualityToken.Should().BeTrue();
    }

    [Fact]
    public void MapToDomain_ReferenceDefaults_ShouldMapCorrectly()
    {
        var yaml = new YamlAuditPolicy
        {
            Version = "1.0",
            ReferenceDefaults = new YamlReferenceDefaults
            {
                ShowKey = false,
                ShowName = true,
                ViewTemplate = "{name}",
                NameResolve = new YamlNameResolveSettings
                {
                    Stage = "raw",
                    Fallback = "<deleted>",
                    MaxLen = 512
                }
            }
        };

        var domain = _mapper.MapToDomain(yaml);

        domain.ReferenceDefaults.ShowKey.Should().BeFalse();
        domain.ReferenceDefaults.ShowName.Should().BeTrue();
        domain.ReferenceDefaults.ViewTemplate.Should().Be("{name}");
        domain.ReferenceDefaults.NameResolve.Stage.Should().Be("raw");
        domain.ReferenceDefaults.NameResolve.Fallback.Should().Be("<deleted>");
        domain.ReferenceDefaults.NameResolve.MaxLen.Should().Be(512);
    }

    [Fact]
    public void MapToDomain_CollectionDefaults_ShouldMapCorrectly()
    {
        var yaml = new YamlAuditPolicy
        {
            Version = "1.0",
            CollectionDefaults = new YamlCollectionDefaults
            {
                LogDeltas = false,
                ShowKeys = false,
                ShowNames = true,
                ItemViewTemplate = "{name}",
                Limits = new YamlCollectionLimits
                {
                    AddedMax = 100,
                    RemovedMax = 150
                }
            }
        };

        var domain = _mapper.MapToDomain(yaml);

        domain.CollectionDefaults.LogDeltas.Should().BeFalse();
        domain.CollectionDefaults.ShowKeys.Should().BeFalse();
        domain.CollectionDefaults.ShowNames.Should().BeTrue();
        domain.CollectionDefaults.ItemViewTemplate.Should().Be("{name}");
        domain.CollectionDefaults.Limits.AddedMax.Should().Be(100);
        domain.CollectionDefaults.Limits.RemovedMax.Should().Be(150);
    }

    [Fact]
    public void MapToDomain_DefaultValues_ShouldBeAppliedWhenMissing()
    {
        var yaml = new YamlAuditPolicy
        {
            Version = "1.0"
        };

        var domain = _mapper.MapToDomain(yaml);

        domain.Version.Should().Be("1.0");
        domain.Mode.Should().Be(AuditMode.Whitelist);
        domain.OnCreate.Should().Be(CreateBehavior.EventOnly);
        domain.OnUpdate.Should().Be(UpdateBehavior.Delta);
        domain.OnDelete.Should().Be(DeleteBehavior.EventOnly);
        domain.DefaultCulture.Should().Be("ru-RU");
        domain.DefaultTimeZone.Should().Be("Asia/Almaty");
    }
}