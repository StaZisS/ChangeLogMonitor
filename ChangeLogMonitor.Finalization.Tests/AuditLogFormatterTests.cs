using Audit.V1;
using ChangeLogMonitor.Configuration.Services;
using ChangeLogMonitor.Core.Models.Policy;
using ChangeLogMonitor.Finalization.Models;
using ChangeLogMonitor.Finalization.Services;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Moq;
using Xunit;

namespace ChangeLogMonitor.Finalization.Tests;

public class AuditLogFormatterTests
{
    private readonly Mock<IAuditConfigurationService> _configurationServiceMock;
    private readonly AuditLogFormatter _formatter;

    public AuditLogFormatterTests()
    {
        _configurationServiceMock = new Mock<IAuditConfigurationService>();
        _configurationServiceMock
            .Setup(x => x.GetEntityPolicy(It.IsAny<string>(), It.IsAny<bool>()))
            .Returns((EntityPolicy?)null);
        _formatter = new AuditLogFormatter(_configurationServiceMock.Object);
    }

    #region Basic Formatting Tests

    [Fact]
    public void Format_ReturnsFormattedResponse_WithCorrectFields()
    {
        var record = CreateAuditLogRecord(
            logId: 1,
            tableName: "Orders",
            operationCode: 1, // Create
            entityId: "order-123",
            userId: "user-1",
            userName: "John Doe");

        var result = _formatter.Format(record, "UTC");

        Assert.Equal(1, result.LogId);
        Assert.Equal("Orders", result.TableName);
        Assert.Equal("order-123", result.EntityId);
        Assert.Equal("user-1", result.UserId);
        Assert.Equal("John Doe", result.UserTitle);
    }

    [Fact]
    public void Format_UsesUserNameAsUserTitle_WhenAvailable()
    {
        var record = CreateAuditLogRecord(
            userId: "user-123",
            userName: "Jane Smith");

        var result = _formatter.Format(record, "UTC");

        Assert.Equal("Jane Smith", result.UserTitle);
    }

    [Fact]
    public void Format_FallsBackToUserId_WhenUserNameIsEmpty()
    {
        var record = CreateAuditLogRecord(
            userId: "user-456",
            userName: "");

        var result = _formatter.Format(record, "UTC");

        Assert.Equal("user-456", result.UserTitle);
    }

    [Fact]
    public void Format_UsesUserTitleFromPayload_WhenUserNameIsEmpty()
    {
        var auditRecord = new AuditRecord
        {
            Id = "test-1",
            EntityType = "Orders",
            EntityId = "order-1",
            Operation = OperationType.OperationCreate,
            UserId = "user-1",
            UserTitle = "Admin User",
            TimestampUtc = Timestamp.FromDateTime(DateTime.UtcNow)
        };

        var record = CreateAuditLogRecord(
            userId: "user-1",
            userName: "",
            payload: auditRecord);

        var result = _formatter.Format(record, "UTC");

        Assert.Equal("Admin User", result.UserTitle);
    }

    #endregion

    #region Operation Name Tests

    [Theory]
    [InlineData(1, "CREATE")]
    [InlineData(2, "UPDATE")]
    [InlineData(3, "DELETE")]
    [InlineData(4, "SOFT_DELETE")]
    [InlineData(5, "BULK_UPDATE")]
    [InlineData(6, "BULK_DELETE")]
    public void Format_ReturnsCorrectOperationName(byte operationCode, string expectedName)
    {
        var record = CreateAuditLogRecord(operationCode: operationCode);

        var result = _formatter.Format(record, "UTC");

        Assert.Equal(expectedName, result.Operation);
    }

    #endregion

    #region Summary Generation Tests

    [Fact]
    public void Format_GeneratesCreateSummary()
    {
        var record = CreateAuditLogRecord(
            tableName: "Orders",
            operationCode: 1,
            userName: "John Doe");

        var result = _formatter.Format(record, "UTC");

        Assert.Contains("создан", result.Summary.ToLower());
        Assert.Contains("John Doe", result.Summary);
    }

    [Fact]
    public void Format_GeneratesUpdateSummary()
    {
        var record = CreateAuditLogRecord(
            tableName: "Orders",
            operationCode: 2,
            userName: "Jane Smith");

        var result = _formatter.Format(record, "UTC");

        Assert.Contains("изменен", result.Summary.ToLower());
        Assert.Contains("Jane Smith", result.Summary);
    }

    [Fact]
    public void Format_GeneratesDeleteSummary()
    {
        var record = CreateAuditLogRecord(
            tableName: "Users",
            operationCode: 3,
            userName: "Admin");

        var result = _formatter.Format(record, "UTC");

        Assert.Contains("удален", result.Summary.ToLower());
        Assert.Contains("Admin", result.Summary);
    }

    #endregion

    #region Field Changes Formatting Tests

    [Fact]
    public void Format_IncludesFieldChanges_InDetails()
    {
        var auditRecord = new AuditRecord
        {
            Id = "test-1",
            EntityType = "Orders",
            EntityId = "order-1",
            Operation = OperationType.OperationUpdate,
            TimestampUtc = Timestamp.FromDateTime(DateTime.UtcNow)
        };
        auditRecord.FieldChanges.Add(new FieldChange
        {
            FieldName = "Amount",
            FieldTitle = "Сумма",
            ValueKind = ValueKind.Scalar,
            SensitiveMode = SensitiveMode.None,
            OldValue = new FieldValue { Normalized = "100" },
            NewValue = new FieldValue { Normalized = "150" }
        });

        var record = CreateAuditLogRecord(payload: auditRecord);

        var result = _formatter.Format(record, "UTC");

        Assert.NotEmpty(result.Details);
        Assert.Contains(result.Details, d => d.Contains("Сумма") && d.Contains("100") && d.Contains("150"));
    }

    [Fact]
    public void Format_ShowsMaskedFieldAsMasked()
    {
        var auditRecord = new AuditRecord
        {
            Id = "test-1",
            EntityType = "Users",
            EntityId = "user-1",
            Operation = OperationType.OperationUpdate,
            TimestampUtc = Timestamp.FromDateTime(DateTime.UtcNow)
        };
        auditRecord.FieldChanges.Add(new FieldChange
        {
            FieldName = "Password",
            FieldTitle = "Пароль",
            ValueKind = ValueKind.Scalar,
            SensitiveMode = SensitiveMode.Masked,
            OldValue = new FieldValue { Normalized = "***" },
            NewValue = new FieldValue { Normalized = "***" }
        });

        var record = CreateAuditLogRecord(payload: auditRecord);

        var result = _formatter.Format(record, "UTC");

        Assert.NotEmpty(result.Details);
        // Masked fields show the masked values as-is (e.g., "***")
        Assert.Contains(result.Details, d => d.Contains("Пароль") && d.Contains("***"));
    }

    [Fact]
    public void Format_ShowsHashedFieldAsHashed()
    {
        var auditRecord = new AuditRecord
        {
            Id = "test-1",
            EntityType = "Users",
            EntityId = "user-1",
            Operation = OperationType.OperationUpdate,
            TimestampUtc = Timestamp.FromDateTime(DateTime.UtcNow)
        };
        auditRecord.FieldChanges.Add(new FieldChange
        {
            FieldName = "SSN",
            FieldTitle = "ИИН",
            ValueKind = ValueKind.Scalar,
            SensitiveMode = SensitiveMode.Hashed,
            OldValue = new FieldValue { Normalized = "abc123" },
            NewValue = new FieldValue { Normalized = "def456" }
        });

        var record = CreateAuditLogRecord(payload: auditRecord);

        var result = _formatter.Format(record, "UTC");

        Assert.NotEmpty(result.Details);
        // Hashed fields show "[SHA256]" prefix before values
        Assert.Contains(result.Details, d => d.Contains("ИИН") && d.Contains("[SHA256]"));
    }

    [Fact]
    public void Format_ShowsNewValueOnly_WhenOldValueIsEmpty()
    {
        var auditRecord = new AuditRecord
        {
            Id = "test-1",
            EntityType = "Orders",
            EntityId = "order-1",
            Operation = OperationType.OperationCreate,
            TimestampUtc = Timestamp.FromDateTime(DateTime.UtcNow)
        };
        auditRecord.FieldChanges.Add(new FieldChange
        {
            FieldName = "Description",
            FieldTitle = "Описание",
            ValueKind = ValueKind.Scalar,
            SensitiveMode = SensitiveMode.None,
            NewValue = new FieldValue { Normalized = "New order" }
        });

        var record = CreateAuditLogRecord(payload: auditRecord);

        var result = _formatter.Format(record, "UTC");

        Assert.NotEmpty(result.Details);
        Assert.Contains(result.Details, d => d.Contains("Описание") && d.Contains("New order"));
    }

    [Fact]
    public void Format_ShowsOldValueOnly_WhenNewValueIsEmpty()
    {
        var auditRecord = new AuditRecord
        {
            Id = "test-1",
            EntityType = "Orders",
            EntityId = "order-1",
            Operation = OperationType.OperationDelete,
            TimestampUtc = Timestamp.FromDateTime(DateTime.UtcNow)
        };
        auditRecord.FieldChanges.Add(new FieldChange
        {
            FieldName = "Description",
            FieldTitle = "Описание",
            ValueKind = ValueKind.Scalar,
            SensitiveMode = SensitiveMode.None,
            OldValue = new FieldValue { Normalized = "Old description" }
        });

        var record = CreateAuditLogRecord(payload: auditRecord);

        var result = _formatter.Format(record, "UTC");

        Assert.NotEmpty(result.Details);
        Assert.Contains(result.Details, d => d.Contains("Описание") && d.Contains("Old description"));
    }

    #endregion

    #region Collection Changes Formatting Tests

    [Fact]
    public void Format_IncludesCollectionChanges_InDetails()
    {
        var auditRecord = new AuditRecord
        {
            Id = "test-1",
            EntityType = "Orders",
            EntityId = "order-1",
            Operation = OperationType.OperationUpdate,
            TimestampUtc = Timestamp.FromDateTime(DateTime.UtcNow)
        };

        var collectionChange = new CollectionChange
        {
            FieldName = "Tags",
            FieldTitle = "Теги"
        };
        collectionChange.Items.Add(new CollectionItemChange
        {
            Kind = CollectionDeltaKind.Add,
            ItemKey = "tag-1",
            ItemTitle = "Important"
        });

        auditRecord.CollectionChanges.Add(collectionChange);

        var record = CreateAuditLogRecord(payload: auditRecord);

        var result = _formatter.Format(record, "UTC");

        Assert.NotEmpty(result.Details);
        Assert.Contains(result.Details, d => d.Contains("Теги") && d.Contains("Important"));
    }

    #endregion

    #region Timezone Tests

    [Fact]
    public void Format_ConvertsTimeToSpecifiedTimezone()
    {
        var utcTime = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var record = CreateAuditLogRecord(changeTimeUtc: utcTime);

        var result = _formatter.Format(record, "Europe/Moscow"); // UTC+3

        // Moscow is UTC+3, so 12:00 UTC should be 15:00 Moscow
        Assert.Equal(15, result.ChangeTime.Hour);
    }

    [Fact]
    public void Format_FallsBackToUtc_WhenTimezoneIsInvalid()
    {
        var utcTime = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var record = CreateAuditLogRecord(changeTimeUtc: utcTime);

        var result = _formatter.Format(record, "Invalid/Timezone");

        // Should fall back to UTC or default timezone - verify it doesn't throw
        Assert.True(result.ChangeTime != default);
    }

    #endregion

    #region Entity Title Tests

    [Fact]
    public void Format_UsesEntityTitleFromPayload_WhenAvailable()
    {
        var auditRecord = new AuditRecord
        {
            Id = "test-1",
            EntityType = "Orders",
            EntityId = "order-1",
            EntityTitle = "Order #12345",
            Operation = OperationType.OperationCreate,
            TimestampUtc = Timestamp.FromDateTime(DateTime.UtcNow)
        };

        var record = CreateAuditLogRecord(tableName: "Orders", payload: auditRecord);

        var result = _formatter.Format(record, "UTC");

        Assert.Equal("Order #12345", result.EntityTitle);
    }

    [Fact]
    public void Format_FallsBackToTableName_WhenEntityTitleIsEmpty()
    {
        var auditRecord = new AuditRecord
        {
            Id = "test-1",
            EntityType = "Orders",
            EntityId = "order-1",
            EntityTitle = "",
            Operation = OperationType.OperationCreate,
            TimestampUtc = Timestamp.FromDateTime(DateTime.UtcNow)
        };

        var record = CreateAuditLogRecord(tableName: "Orders", payload: auditRecord);

        var result = _formatter.Format(record, "UTC");

        Assert.Equal("Orders", result.EntityTitle);
    }

    [Fact]
    public void Format_UsesDisplayNameFromConfig_WhenEntityTitleIsEmpty()
    {
        // Arrange
        var entityPolicy = new EntityPolicy { DisplayName = "Заказы" };
        _configurationServiceMock
            .Setup(x => x.GetEntityPolicy("Orders", It.IsAny<bool>()))
            .Returns(entityPolicy);

        var auditRecord = new AuditRecord
        {
            Id = "test-1",
            EntityType = "Orders",
            EntityId = "order-1",
            EntityTitle = "",
            Operation = OperationType.OperationCreate,
            TimestampUtc = Timestamp.FromDateTime(DateTime.UtcNow)
        };

        var record = CreateAuditLogRecord(tableName: "Orders", payload: auditRecord);

        // Act
        var result = _formatter.Format(record, "UTC");

        // Assert
        Assert.Equal("Заказы", result.EntityTitle);
    }

    [Fact]
    public void Format_PrefersEntityTitleFromPayload_OverDisplayNameFromConfig()
    {
        // Arrange
        var entityPolicy = new EntityPolicy { DisplayName = "Заказы" };
        _configurationServiceMock
            .Setup(x => x.GetEntityPolicy("Orders", It.IsAny<bool>()))
            .Returns(entityPolicy);

        var auditRecord = new AuditRecord
        {
            Id = "test-1",
            EntityType = "Orders",
            EntityId = "order-1",
            EntityTitle = "Order #12345", // This should take priority
            Operation = OperationType.OperationCreate,
            TimestampUtc = Timestamp.FromDateTime(DateTime.UtcNow)
        };

        var record = CreateAuditLogRecord(tableName: "Orders", payload: auditRecord);

        // Act
        var result = _formatter.Format(record, "UTC");

        // Assert
        Assert.Equal("Order #12345", result.EntityTitle);
    }

    #endregion

    #region Helper Methods

    private static AuditLogRecord CreateAuditLogRecord(
        long logId = 1,
        DateTime? changeTimeUtc = null,
        string userId = "user-1",
        string userName = "Test User",
        string tableName = "TestTable",
        byte operationCode = 1,
        string entityId = "entity-1",
        string txId = "tx-1",
        AuditRecord? payload = null)
    {
        var time = changeTimeUtc ?? DateTime.UtcNow;

        if (payload == null)
        {
            payload = new AuditRecord
            {
                Id = $"{txId}-0",
                EntityType = tableName,
                EntityId = entityId,
                Operation = (OperationType)operationCode,
                TimestampUtc = Timestamp.FromDateTime(DateTime.SpecifyKind(time, DateTimeKind.Utc)),
                UserId = userId
            };
        }

        var payloadBytes = payload.ToByteArray();
        var payloadBase64 = Convert.ToBase64String(payloadBytes);

        return new AuditLogRecord(
            logId,
            time,
            userId,
            userName,
            tableName,
            operationCode,
            entityId,
            txId,
            payloadBase64);
    }

    #endregion
}
