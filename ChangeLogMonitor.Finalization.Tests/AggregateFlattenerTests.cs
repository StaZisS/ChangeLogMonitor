using System.Text.Json;
using Audit.V1;
using ChangeLogMonitor.Configuration.Services;
using ChangeLogMonitor.Core.Enums;
using ChangeLogMonitor.Core.Models.Policy;
using ChangeLogMonitor.Finalization.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ChangeLogMonitor.Finalization.Tests;

public class AggregateFlattenerTests
{
    [Fact]
    public void Flatten_ProducesAuditRecordPayload()
    {
        var aggregateJson = """
                            {
                              "tx_id": "tx-123",
                              "meta": { "user_id": "u-1" },
                              "events": [
                                {
                                  "table": "orders",
                                  "op": "u",
                                  "ts_ms": 1717698123456,
                                  "payload": { "id": "order-1", "amount": 10 }
                                }
                              ]
                            }
                            """;

        var flattener = new AggregateFlattener(new NullLogger<AggregateFlattener>());

        var rows = flattener.Flatten(aggregateJson, CancellationToken.None);

        var record = Assert.Single(rows);
        Assert.Equal("orders", record.TableName);
        Assert.Equal("order-1", record.EntityId);
        Assert.Equal(OperationCode.Update, record.Operation);

        var parsed = AuditRecord.Parser.ParseFrom(Convert.FromBase64String(record.Payload));
        Assert.Equal("tx-123-0", parsed.Id);
        Assert.Equal("orders", parsed.EntityType);
        Assert.Equal("order-1", parsed.EntityId);
        Assert.Equal(OperationType.OperationUpdate, parsed.Operation);
        Assert.Equal("u-1", parsed.UserId);

        var payloadDoc = JsonDocument.Parse(parsed.RawPayloadJson);
        Assert.Equal("order-1", payloadDoc.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public void Flatten_ExtractsUserNameFromMeta()
    {
        var aggregateJson = """
                            {
                              "tx_id": "tx-456",
                              "meta": {
                                "actor": {
                                  "user_id": "user-123",
                                  "user_name": "John Doe"
                                }
                              },
                              "events": [
                                {
                                  "table": "Users",
                                  "op": "c",
                                  "ts_ms": 1717698123456,
                                  "payload": { "Id": "user-123", "Username": "johndoe" }
                                }
                              ]
                            }
                            """;

        var flattener = new AggregateFlattener(new NullLogger<AggregateFlattener>());

        var rows = flattener.Flatten(aggregateJson, CancellationToken.None);

        var record = Assert.Single(rows);
        Assert.Equal("user-123", record.UserId);
        Assert.Equal("John Doe", record.UserName);
    }

    #region Global Exclusions Tests

    [Fact]
    public void Flatten_WithGlobalFieldExclusions_OmitsGloballyExcludedFields()
    {
        var aggregateJson = """
                            {
                              "tx_id": "tx-global-exclude",
                              "meta": { "user_id": "u-1" },
                              "events": [
                                {
                                  "table": "Users",
                                  "op": "c",
                                  "ts_ms": 1717698123456,
                                  "payload": { "Id": "u-1", "Username": "john", "xmin": "12345", "__version": "1" }
                                }
                              ]
                            }
                            """;

        var mockConfigService = CreateMockConfigService(
            globalFieldExclusions: new List<string> { "xmin", "__version" },
            entityPolicies: new Dictionary<string, EntityPolicy>
            {
                ["Users"] = new()
                {
                    OnCreate = CreateBehavior.AllFields
                }
            });

        var flattener = new AggregateFlattener(new NullLogger<AggregateFlattener>(), mockConfigService.Object);

        var rows = flattener.Flatten(aggregateJson, CancellationToken.None);
        var record = Assert.Single(rows);

        var parsed = AuditRecord.Parser.ParseFrom(Convert.FromBase64String(record.Payload));

        Assert.DoesNotContain(parsed.FieldChanges, fc => fc.FieldName == "xmin");
        Assert.DoesNotContain(parsed.FieldChanges, fc => fc.FieldName == "__version");
        Assert.Contains(parsed.FieldChanges, fc => fc.FieldName == "Username");
    }

    #endregion

    #region Mask Preset Tests

    [Fact]
    public void Flatten_WithMaskPreset_AppliesPresetSettings()
    {
        var aggregateJson = """
                            {
                              "tx_id": "tx-preset",
                              "meta": { "user_id": "u-1" },
                              "events": [
                                {
                                  "table": "Users",
                                  "op": "c",
                                  "ts_ms": 1717698123456,
                                  "payload": { "Id": "u-1", "PhoneNumber": "+77001234567" }
                                }
                              ]
                            }
                            """;

        var mockConfigService = CreateMockConfigService(
            maskPresets: new Dictionary<string, MaskPreset>
            {
                ["phone"] = new()
                {
                    MaskChar = '*',
                    KeepLeft = 5,
                    KeepRight = 4
                }
            },
            entityPolicies: new Dictionary<string, EntityPolicy>
            {
                ["Users"] = new()
                {
                    OnCreate = CreateBehavior.AllFields,
                    Fields = new Dictionary<string, FieldPolicy>
                    {
                        ["PhoneNumber"] = new()
                        {
                            Action = FieldAction.Mask,
                            Mask = new MaskSettings { Preset = "phone" }
                        }
                    }
                }
            });

        var flattener = new AggregateFlattener(new NullLogger<AggregateFlattener>(), mockConfigService.Object);

        var rows = flattener.Flatten(aggregateJson, CancellationToken.None);
        var record = Assert.Single(rows);

        var parsed = AuditRecord.Parser.ParseFrom(Convert.FromBase64String(record.Payload));

        var phoneChange = Assert.Single(parsed.FieldChanges, fc => fc.FieldName == "PhoneNumber");
        Assert.StartsWith("+7700", phoneChange.NewValue.Normalized);
        Assert.EndsWith("4567", phoneChange.NewValue.Normalized);
    }

    #endregion

    #region Helper Methods

    private static Mock<IAuditConfigurationService> CreateMockConfigService(
        Dictionary<string, EntityPolicy>? entityPolicies = null,
        List<string>? globalFieldExclusions = null,
        Dictionary<string, MaskPreset>? maskPresets = null,
        Dictionary<string, HashPreset>? hashPresets = null)
    {
        var mock = new Mock<IAuditConfigurationService>();

        var policy = new AuditPolicy
        {
            GlobalFieldExclusions = globalFieldExclusions ?? new List<string>(),
            MaskPresets = maskPresets ?? new Dictionary<string, MaskPreset>(),
            HashPresets = hashPresets ?? new Dictionary<string, HashPreset>(),
            Entities = entityPolicies ?? new Dictionary<string, EntityPolicy>()
        };

        mock.Setup(x => x.GetPolicy(It.IsAny<bool>())).Returns(policy);

        mock.Setup(x => x.GetEntityPolicy(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns((string entityName, bool _) =>
                policy.Entities.TryGetValue(entityName, out var ep) ? ep : null);

        return mock;
    }

    #endregion

    #region Field Policy Tests

    [Fact]
    public void Flatten_WithExcludePolicy_OmitsField()
    {
        var aggregateJson = """
                            {
                              "tx_id": "tx-789",
                              "meta": { "user_id": "u-1" },
                              "events": [
                                {
                                  "table": "Users",
                                  "op": "c",
                                  "ts_ms": 1717698123456,
                                  "payload": { "Id": "u-1", "Username": "john", "PasswordHash": "secret123", "Email": "john@test.com" }
                                }
                              ]
                            }
                            """;

        var mockConfigService = CreateMockConfigService(
            new Dictionary<string, EntityPolicy>
            {
                ["Users"] = new()
                {
                    OnCreate = CreateBehavior.AllFields,
                    Fields = new Dictionary<string, FieldPolicy>
                    {
                        ["PasswordHash"] = new() { Action = FieldAction.Exclude }
                    }
                }
            });

        var flattener = new AggregateFlattener(new NullLogger<AggregateFlattener>(), mockConfigService.Object);

        var rows = flattener.Flatten(aggregateJson, CancellationToken.None);
        var record = Assert.Single(rows);

        var parsed = AuditRecord.Parser.ParseFrom(Convert.FromBase64String(record.Payload));

        Assert.DoesNotContain(parsed.FieldChanges, fc => fc.FieldName == "PasswordHash");
        Assert.Contains(parsed.FieldChanges, fc => fc.FieldName == "Username");
        Assert.Contains(parsed.FieldChanges, fc => fc.FieldName == "Email");
    }

    [Fact]
    public void Flatten_WithMaskPolicy_MasksFieldValue()
    {
        var aggregateJson = """
                            {
                              "tx_id": "tx-mask",
                              "meta": { "user_id": "u-1" },
                              "events": [
                                {
                                  "table": "Users",
                                  "op": "c",
                                  "ts_ms": 1717698123456,
                                  "payload": { "Id": "u-1", "Email": "john.doe@example.com" }
                                }
                              ]
                            }
                            """;

        var mockConfigService = CreateMockConfigService(
            new Dictionary<string, EntityPolicy>
            {
                ["Users"] = new()
                {
                    OnCreate = CreateBehavior.AllFields,
                    Fields = new Dictionary<string, FieldPolicy>
                    {
                        ["Email"] = new()
                        {
                            Action = FieldAction.Mask,
                            Mask = new MaskSettings
                            {
                                MaskChar = '*',
                                KeepLeft = 2,
                                KeepRight = 4
                            }
                        }
                    }
                }
            });

        var flattener = new AggregateFlattener(new NullLogger<AggregateFlattener>(), mockConfigService.Object);

        var rows = flattener.Flatten(aggregateJson, CancellationToken.None);
        var record = Assert.Single(rows);

        var parsed = AuditRecord.Parser.ParseFrom(Convert.FromBase64String(record.Payload));

        var emailChange = Assert.Single(parsed.FieldChanges, fc => fc.FieldName == "Email");
        Assert.Equal(SensitiveMode.Masked, emailChange.SensitiveMode);
        Assert.StartsWith("jo", emailChange.NewValue.Normalized);
        Assert.EndsWith(".com", emailChange.NewValue.Normalized);
        Assert.Contains("*", emailChange.NewValue.Normalized);
    }

    [Fact]
    public void Flatten_WithHashPolicy_HashesFieldValue()
    {
        var aggregateJson = """
                            {
                              "tx_id": "tx-hash",
                              "meta": { "user_id": "u-1" },
                              "events": [
                                {
                                  "table": "Orders",
                                  "op": "c",
                                  "ts_ms": 1717698123456,
                                  "payload": { "Id": "order-1", "Amount": "100.50" }
                                }
                              ]
                            }
                            """;

        var mockConfigService = CreateMockConfigService(
            new Dictionary<string, EntityPolicy>
            {
                ["Orders"] = new()
                {
                    OnCreate = CreateBehavior.AllFields,
                    Fields = new Dictionary<string, FieldPolicy>
                    {
                        ["Amount"] = new()
                        {
                            Action = FieldAction.Hash,
                            Hash = new HashSettings { Algo = "SHA-256" }
                        }
                    }
                }
            });

        var flattener = new AggregateFlattener(new NullLogger<AggregateFlattener>(), mockConfigService.Object);

        var rows = flattener.Flatten(aggregateJson, CancellationToken.None);
        var record = Assert.Single(rows);

        var parsed = AuditRecord.Parser.ParseFrom(Convert.FromBase64String(record.Payload));

        var amountChange = Assert.Single(parsed.FieldChanges, fc => fc.FieldName == "Amount");
        Assert.Equal(SensitiveMode.Hashed, amountChange.SensitiveMode);
        Assert.NotEqual("100.50", amountChange.NewValue.Normalized);
        Assert.Equal(44, amountChange.NewValue.Normalized.Length);
    }

    [Fact]
    public void Flatten_WithEncryptPolicy_MarksAsEncrypted()
    {
        var aggregateJson = """
                            {
                              "tx_id": "tx-encrypt",
                              "meta": { "user_id": "u-1" },
                              "events": [
                                {
                                  "table": "Users",
                                  "op": "c",
                                  "ts_ms": 1717698123456,
                                  "payload": { "Id": "u-1", "CreditCard": "4111111111111111" }
                                }
                              ]
                            }
                            """;

        var mockConfigService = CreateMockConfigService(
            new Dictionary<string, EntityPolicy>
            {
                ["Users"] = new()
                {
                    OnCreate = CreateBehavior.AllFields,
                    Fields = new Dictionary<string, FieldPolicy>
                    {
                        ["CreditCard"] = new() { Action = FieldAction.Encrypt }
                    }
                }
            });

        var flattener = new AggregateFlattener(new NullLogger<AggregateFlattener>(), mockConfigService.Object);

        var rows = flattener.Flatten(aggregateJson, CancellationToken.None);
        var record = Assert.Single(rows);

        var parsed = AuditRecord.Parser.ParseFrom(Convert.FromBase64String(record.Payload));

        var cardChange = Assert.Single(parsed.FieldChanges, fc => fc.FieldName == "CreditCard");
        Assert.Equal(SensitiveMode.Encrypted, cardChange.SensitiveMode);
        Assert.Equal("[ENCRYPTED]", cardChange.NewValue.Normalized);
    }

    #endregion

    #region Operation Behavior Tests

    [Fact]
    public void Flatten_OnCreate_EventOnly_NoFieldChanges()
    {
        var aggregateJson = """
                            {
                              "tx_id": "tx-event-only",
                              "meta": { "user_id": "u-1" },
                              "events": [
                                {
                                  "table": "Tags",
                                  "op": "c",
                                  "ts_ms": 1717698123456,
                                  "payload": { "Id": "tag-1", "Name": "Important", "Color": "#FF0000" }
                                }
                              ]
                            }
                            """;

        var mockConfigService = CreateMockConfigService(
            new Dictionary<string, EntityPolicy>
            {
                ["Tags"] = new()
                {
                    OnCreate = CreateBehavior.EventOnly
                }
            });

        var flattener = new AggregateFlattener(new NullLogger<AggregateFlattener>(), mockConfigService.Object);

        var rows = flattener.Flatten(aggregateJson, CancellationToken.None);
        var record = Assert.Single(rows);

        var parsed = AuditRecord.Parser.ParseFrom(Convert.FromBase64String(record.Payload));

        Assert.Empty(parsed.FieldChanges);
        Assert.Equal("Tags", parsed.EntityType);
        Assert.Equal(OperationType.OperationCreate, parsed.Operation);
    }

    [Fact]
    public void Flatten_OnCreate_AllFields_IncludesAllFields()
    {
        var aggregateJson = """
                            {
                              "tx_id": "tx-all-fields",
                              "meta": { "user_id": "u-1" },
                              "events": [
                                {
                                  "table": "Users",
                                  "op": "c",
                                  "ts_ms": 1717698123456,
                                  "payload": { "Id": "u-1", "Username": "john", "FullName": "John Doe" }
                                }
                              ]
                            }
                            """;

        var mockConfigService = CreateMockConfigService(
            new Dictionary<string, EntityPolicy>
            {
                ["Users"] = new()
                {
                    OnCreate = CreateBehavior.AllFields
                }
            });

        var flattener = new AggregateFlattener(new NullLogger<AggregateFlattener>(), mockConfigService.Object);

        var rows = flattener.Flatten(aggregateJson, CancellationToken.None);
        var record = Assert.Single(rows);

        var parsed = AuditRecord.Parser.ParseFrom(Convert.FromBase64String(record.Payload));

        Assert.Equal(3, parsed.FieldChanges.Count);
        Assert.Contains(parsed.FieldChanges, fc => fc.FieldName == "Id");
        Assert.Contains(parsed.FieldChanges, fc => fc.FieldName == "Username");
        Assert.Contains(parsed.FieldChanges, fc => fc.FieldName == "FullName");
    }

    [Fact]
    public void Flatten_OnDelete_AllFields_IncludesFieldsFromBefore()
    {
        var aggregateJson = """
                            {
                              "tx_id": "tx-delete",
                              "meta": { "user_id": "u-1" },
                              "events": [
                                {
                                  "table": "OrderTags",
                                  "op": "d",
                                  "ts_ms": 1717698123456,
                                  "payload": {
                                    "before": { "OrderId": "order-1", "TagId": "tag-1", "AssignedAt": "2024-01-01" },
                                    "after": null
                                  }
                                }
                              ]
                            }
                            """;

        var mockConfigService = CreateMockConfigService(
            new Dictionary<string, EntityPolicy>
            {
                ["OrderTags"] = new()
                {
                    OnDelete = DeleteBehavior.AllFields
                }
            });

        var flattener = new AggregateFlattener(new NullLogger<AggregateFlattener>(), mockConfigService.Object);

        var rows = flattener.Flatten(aggregateJson, CancellationToken.None);
        var record = Assert.Single(rows);

        var parsed = AuditRecord.Parser.ParseFrom(Convert.FromBase64String(record.Payload));

        Assert.Equal(OperationType.OperationDelete, parsed.Operation);
        Assert.Equal(3, parsed.FieldChanges.Count);
        foreach (var fc in parsed.FieldChanges)
        {
            Assert.NotNull(fc.OldValue);
            Assert.Null(fc.NewValue);
        }
    }

    [Fact]
    public void Flatten_OnUpdate_Delta_OnlyChangedFields()
    {
        var aggregateJson = """
                            {
                              "tx_id": "tx-update-delta",
                              "meta": { "user_id": "u-1" },
                              "events": [
                                {
                                  "table": "Orders",
                                  "op": "u",
                                  "ts_ms": 1717698123456,
                                  "payload": {
                                    "before": { "Id": "order-1", "Amount": "100", "Description": "Test" },
                                    "after": { "Id": "order-1", "Amount": "150", "Description": "Test" }
                                  }
                                }
                              ]
                            }
                            """;

        var mockConfigService = CreateMockConfigService(
            new Dictionary<string, EntityPolicy>
            {
                ["Orders"] = new()
                {
                    OnUpdate = UpdateBehavior.Delta
                }
            });

        var flattener = new AggregateFlattener(new NullLogger<AggregateFlattener>(), mockConfigService.Object);

        var rows = flattener.Flatten(aggregateJson, CancellationToken.None);
        var record = Assert.Single(rows);

        var parsed = AuditRecord.Parser.ParseFrom(Convert.FromBase64String(record.Payload));

        var amountChange = Assert.Single(parsed.FieldChanges);
        Assert.Equal("Amount", amountChange.FieldName);
        Assert.Equal("100", amountChange.OldValue.Normalized);
        Assert.Equal("150", amountChange.NewValue.Normalized);
    }

    #endregion
}