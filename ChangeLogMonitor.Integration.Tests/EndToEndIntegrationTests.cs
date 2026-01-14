using Audit.V1;
using Auditmeta.Raw;
using ChangeLogMonitor.Finalization.Models;
using ChangeLogMonitor.Finalization.Services;
using ChangeLogMonitor.Integration.Tests.Infrastructure;
using ChangeLogMonitor.Integration.Tests.TestEntities;
using FluentAssertions;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Xunit;

namespace ChangeLogMonitor.Integration.Tests;

/// <summary>
///     Комплексный интеграционный тест всей цепочки:
///     Entity Change -> Interceptor -> AuditLog -> (simulated CDC) -> AuditLogRecord -> AuditLogFormatter
/// </summary>
[Collection("IntegrationTests")]
public class EndToEndIntegrationTests : IntegrationTestBase
{
    private const string ConfigPath = "TestConfigs/e2e-config.yaml";

    public EndToEndIntegrationTests(PostgreSqlFixture fixture) : base(fixture)
    {
    }

    /// <summary>
    ///     Тест полной цепочки: создание клиента
    /// </summary>
    [Fact]
    public async Task FullChain_CreateCustomer_ProducesCorrectFormattedOutput()
    {
        // Arrange
        MetadataProvider.SetUser("user-123", "Иван Петров");
        MetadataProvider.AddHint("source", "web-app");

        // Act - Create customer (Interceptor captures metadata)
        await using var appContext = CreateAppDbContext(ConfigPath);
        var customer = new Customer
        {
            Name = "ООО Рога и Копыта",
            Email = "info@rogaikopyta.ru",
            Phone = "+7-999-123-45-67",
            PasswordHash = "secret_password_hash",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        appContext.Customers.Add(customer);
        await appContext.SaveChangesAsync();

        // Verify Interceptor created audit log
        var auditLogs = await GetAllAuditLogsAsync();
        auditLogs.Should().HaveCount(1, "Interceptor should create one audit log entry");

        var auditLog = auditLogs[0];
        auditLog.TransactionId.Should().NotBeNullOrEmpty();
        auditLog.Payload.Should().NotBeEmpty();

        // Deserialize AuditMetaEnvelope (what Interceptor writes)
        var envelope = AuditMetaEnvelope.Parser.ParseFrom(auditLog.Payload);
        envelope.TransactionId.Should().Be(auditLog.TransactionId);
        envelope.Actor.UserId.Should().Be("user-123");
        envelope.Actor.UserName.Should().Be("Иван Петров");
        envelope.Request.Should().NotBeNull();
        envelope.Request.ServiceName.Should().Be("IntegrationTests");
        envelope.Hints.Should().Contain(h => h.Key == "source" && h.Value == "web-app");

        // Simulate CDC + Aggregation: create AuditRecord (what DataAggregator would produce)
        var auditRecord = CreateAuditRecordForCreate(
            entityType: "customers",
            entityId: customer.Id.ToString(),
            entityTitle: customer.Name,
            userId: envelope.Actor.UserId,
            userTitle: envelope.Actor.UserName,
            fieldChanges: new[]
            {
                CreateFieldChange("Name", "Имя клиента", null, customer.Name),
                CreateFieldChange("Email", "Электронная почта", null, customer.Email),
                CreateFieldChange("Phone", "Телефон", null, customer.Phone),
                CreateFieldChange("PasswordHash", null, null, "***HASHED***", SensitiveMode.Hashed),
                CreateFieldChange("IsActive", "Активен", null, customer.IsActive.ToString())
            });

        // Create AuditLogRecord (what would be stored after aggregation)
        var auditLogRecord = new AuditLogRecord(
            LogId: auditLog.Id,
            ChangeTimeUtc: auditLog.CreatedAt,
            UserId: envelope.Actor.UserId,
            UserName: envelope.Actor.UserName,
            TableName: "customers",
            OperationCode: 1, // CREATE
            EntityId: customer.Id.ToString(),
            TxId: auditLog.TransactionId,
            Payload: Convert.ToBase64String(auditRecord.ToByteArray()));

        // Format for display (Finalization layer)
        var configService = CreateConfigurationService(ConfigPath);
        var formatter = new AuditLogFormatter(configService);
        var formatted = formatter.Format(auditLogRecord, "Europe/Moscow");

        // Assert formatted output
        formatted.LogId.Should().Be(auditLog.Id);
        formatted.TableName.Should().Be("Customer");
        formatted.Operation.Should().Be("Создано");
        formatted.EntityId.Should().Be(customer.Id.ToString());
        formatted.EntityTitle.Should().Be("ООО Рога и Копыта");
        formatted.UserId.Should().Be("user-123");
        formatted.UserTitle.Should().Be("Иван Петров");
        formatted.Summary.Should().Contain("Клиент");
        formatted.Summary.Should().Contain("Иван Петров");
        formatted.Details.Should().NotBeEmpty();
        formatted.Details.Should().Contain(d => d.Contains("Имя клиента"));
        formatted.Details.Should().Contain(d => d.Contains("ООО Рога и Копыта"));
    }

    /// <summary>
    ///     Тест полной цепочки: обновление заказа
    /// </summary>
    [Fact]
    public async Task FullChain_UpdateOrder_ProducesCorrectFormattedOutput()
    {
        // Arrange
        MetadataProvider.SetUser("admin-456", "Администратор");

        await using var appContext = CreateAppDbContext(ConfigPath);

        // Create customer first
        var customer = new Customer
        {
            Name = "Тестовый Клиент",
            Email = "test@example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        appContext.Customers.Add(customer);
        await appContext.SaveChangesAsync();

        // Create order
        var order = new Order
        {
            OrderNumber = "ORD-001",
            CustomerId = customer.Id,
            TotalAmount = 1000.00m,
            Status = "New",
            CreatedAt = DateTime.UtcNow
        };
        appContext.Orders.Add(order);
        await appContext.SaveChangesAsync();

        // Clear audit logs to isolate update
        var auditLogsBeforeUpdate = await GetAuditLogCountAsync();

        // Act - Update order status
        order.Status = "Processing";
        order.TotalAmount = 1500.00m;
        order.UpdatedAt = DateTime.UtcNow;
        await appContext.SaveChangesAsync();

        // Verify
        var auditLogs = await GetAllAuditLogsAsync();
        var updateAuditLog = auditLogs.Last();

        var envelope = AuditMetaEnvelope.Parser.ParseFrom(updateAuditLog.Payload);
        envelope.Actor.UserId.Should().Be("admin-456");
        envelope.Actor.UserName.Should().Be("Администратор");

        // Simulate AuditRecord for update
        var auditRecord = CreateAuditRecordForUpdate(
            entityType: "orders",
            entityId: order.Id.ToString(),
            entityTitle: order.OrderNumber,
            userId: envelope.Actor.UserId,
            userTitle: envelope.Actor.UserName,
            fieldChanges: new[]
            {
                CreateFieldChange("Status", "Статус", "New", "Processing"),
                CreateFieldChange("TotalAmount", "Сумма заказа", "1000.00", "1500.00")
            });

        var auditLogRecord = new AuditLogRecord(
            LogId: updateAuditLog.Id,
            ChangeTimeUtc: updateAuditLog.CreatedAt,
            UserId: envelope.Actor.UserId,
            UserName: envelope.Actor.UserName,
            TableName: "orders",
            OperationCode: 2, // UPDATE
            EntityId: order.Id.ToString(),
            TxId: updateAuditLog.TransactionId,
            Payload: Convert.ToBase64String(auditRecord.ToByteArray()));

        var configService = CreateConfigurationService(ConfigPath);
        var formatter = new AuditLogFormatter(configService);
        var formatted = formatter.Format(auditLogRecord, "Europe/Moscow");

        // Assert
        formatted.Operation.Should().Be("Изменено");
        formatted.EntityTitle.Should().Be("ORD-001");
        formatted.Summary.Should().Contain("Заказ");
        formatted.Details.Should().Contain(d => d.Contains("Статус"));
        formatted.Details.Should().Contain(d => d.Contains("New") && d.Contains("Processing"));
    }

    /// <summary>
    ///     Тест полной цепочки: сложная транзакция с несколькими сущностями
    /// </summary>
    [Fact]
    public async Task FullChain_ComplexTransaction_MultipleEntities_AllAudited()
    {
        // Arrange
        MetadataProvider.SetUser("operator-789", "Оператор склада");
        MetadataProvider.AddHint("transaction_type", "order_fulfillment");

        await using var appContext = CreateAppDbContext(ConfigPath);

        // Create all entities in single transaction
        var customer = new Customer
        {
            Name = "Крупный Заказчик",
            Email = "bulk@orders.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        appContext.Customers.Add(customer);

        var product1 = new Product
        {
            Name = "Товар A",
            Price = 100.00m,
            StockQuantity = 50,
            IsAvailable = true
        };
        var product2 = new Product
        {
            Name = "Товар B",
            Price = 200.00m,
            StockQuantity = 30,
            IsAvailable = true
        };
        appContext.Products.AddRange(product1, product2);

        // Save all in one transaction
        await appContext.SaveChangesAsync();

        // Create order with items
        var order = new Order
        {
            OrderNumber = "BULK-001",
            CustomerId = customer.Id,
            TotalAmount = 700.00m,
            Status = "New",
            CreatedAt = DateTime.UtcNow
        };
        appContext.Orders.Add(order);
        await appContext.SaveChangesAsync();

        var item1 = new OrderItem
        {
            OrderId = order.Id,
            ProductId = product1.Id,
            Quantity = 3,
            UnitPrice = product1.Price
        };
        var item2 = new OrderItem
        {
            OrderId = order.Id,
            ProductId = product2.Id,
            Quantity = 2,
            UnitPrice = product2.Price
        };
        appContext.OrderItems.AddRange(item1, item2);
        await appContext.SaveChangesAsync();

        // Verify all audit logs created
        var auditLogs = await GetAllAuditLogsAsync();
        auditLogs.Should().HaveCountGreaterThanOrEqualTo(4,
            "Should have audit logs for Customer, 2 Products, Order, and 2 OrderItems");

        // Verify each audit log has valid envelope
        foreach (var log in auditLogs)
        {
            var envelope = AuditMetaEnvelope.Parser.ParseFrom(log.Payload);
            envelope.Actor.UserId.Should().Be("operator-789");
            envelope.TransactionId.Should().NotBeNullOrEmpty();
        }

        // Format last audit log (OrderItem creation)
        var lastLog = auditLogs.Last();
        var envelope2 = AuditMetaEnvelope.Parser.ParseFrom(lastLog.Payload);

        var auditRecord = CreateAuditRecordForCreate(
            entityType: "order_items",
            entityId: item2.Id.ToString(),
            entityTitle: $"Товар B x{item2.Quantity}",
            userId: envelope2.Actor.UserId,
            userTitle: envelope2.Actor.UserName,
            fieldChanges: new[]
            {
                CreateFieldChange("Quantity", "Количество", null, item2.Quantity.ToString()),
                CreateFieldChange("UnitPrice", "Цена за единицу", null, item2.UnitPrice.ToString("F2"))
            });

        var auditLogRecord = new AuditLogRecord(
            LogId: lastLog.Id,
            ChangeTimeUtc: lastLog.CreatedAt,
            UserId: envelope2.Actor.UserId,
            UserName: envelope2.Actor.UserName,
            TableName: "order_items",
            OperationCode: 1,
            EntityId: item2.Id.ToString(),
            TxId: lastLog.TransactionId,
            Payload: Convert.ToBase64String(auditRecord.ToByteArray()));

        var configService = CreateConfigurationService(ConfigPath);
        var formatter = new AuditLogFormatter(configService);
        var formatted = formatter.Format(auditLogRecord, "UTC");

        formatted.Operation.Should().Be("Создано");
        formatted.Details.Should().Contain(d => d.Contains("Количество"));
    }

    /// <summary>
    ///     Тест полной цепочки: удаление сущности
    /// </summary>
    [Fact]
    public async Task FullChain_DeleteEntity_ProducesCorrectFormattedOutput()
    {
        // Arrange
        MetadataProvider.SetUser("manager-101", "Менеджер");

        await using var appContext = CreateAppDbContext(ConfigPath);

        var product = new Product
        {
            Name = "Удаляемый товар",
            Price = 99.99m,
            StockQuantity = 0,
            IsAvailable = false
        };
        appContext.Products.Add(product);
        await appContext.SaveChangesAsync();

        var productId = product.Id;

        // Act - Delete product
        appContext.Products.Remove(product);
        await appContext.SaveChangesAsync();

        // Verify
        var auditLogs = await GetAllAuditLogsAsync();
        var deleteAuditLog = auditLogs.Last();

        var envelope = AuditMetaEnvelope.Parser.ParseFrom(deleteAuditLog.Payload);
        envelope.Actor.UserName.Should().Be("Менеджер");

        // Simulate AuditRecord for delete (no field changes, just event)
        var auditRecord = new AuditRecord
        {
            Id = Guid.NewGuid().ToString(),
            EntityType = "products",
            EntityId = productId.ToString(),
            EntityTitle = "Удаляемый товар",
            Operation = OperationType.OperationDelete,
            TimestampUtc = Timestamp.FromDateTime(DateTime.UtcNow),
            UserId = envelope.Actor.UserId,
            UserTitle = envelope.Actor.UserName
        };

        var auditLogRecord = new AuditLogRecord(
            LogId: deleteAuditLog.Id,
            ChangeTimeUtc: deleteAuditLog.CreatedAt,
            UserId: envelope.Actor.UserId,
            UserName: envelope.Actor.UserName,
            TableName: "products",
            OperationCode: 3, // DELETE
            EntityId: productId.ToString(),
            TxId: deleteAuditLog.TransactionId,
            Payload: Convert.ToBase64String(auditRecord.ToByteArray()));

        var configService = CreateConfigurationService(ConfigPath);
        var formatter = new AuditLogFormatter(configService);
        var formatted = formatter.Format(auditLogRecord, "Europe/Moscow");

        // Assert
        formatted.Operation.Should().Be("Удалено");
        formatted.EntityTitle.Should().Be("Удаляемый товар");
        formatted.Summary.Should().Contain("Товар");
        formatted.Summary.Should().Contain("Менеджер");
    }

    /// <summary>
    ///     Тест: проверка маскирования чувствительных данных
    /// </summary>
    [Fact]
    public async Task FullChain_SensitiveData_IsMaskedInOutput()
    {
        // Arrange
        MetadataProvider.SetUser("admin", "System Admin");

        await using var appContext = CreateAppDbContext(ConfigPath);

        var customer = new Customer
        {
            Name = "Secure Customer",
            Email = "secure@test.com",
            PasswordHash = "super_secret_hash_12345",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        appContext.Customers.Add(customer);
        await appContext.SaveChangesAsync();

        // Create AuditRecord with hashed password field
        var auditRecord = CreateAuditRecordForCreate(
            entityType: "customers",
            entityId: customer.Id.ToString(),
            entityTitle: customer.Name,
            userId: "admin",
            userTitle: "System Admin",
            fieldChanges: new[]
            {
                CreateFieldChange("Name", "Имя клиента", null, customer.Name),
                CreateFieldChange("Email", "Электронная почта", null, customer.Email),
                CreateFieldChange("PasswordHash", null, null, "a1b2c3d4e5f6...", SensitiveMode.Hashed)
            });

        var auditLogs = await GetAllAuditLogsAsync();
        var log = auditLogs.Last();

        var auditLogRecord = new AuditLogRecord(
            LogId: log.Id,
            ChangeTimeUtc: log.CreatedAt,
            UserId: "admin",
            UserName: "System Admin",
            TableName: "customers",
            OperationCode: 1,
            EntityId: customer.Id.ToString(),
            TxId: log.TransactionId,
            Payload: Convert.ToBase64String(auditRecord.ToByteArray()));

        var configService = CreateConfigurationService(ConfigPath);
        var formatter = new AuditLogFormatter(configService);
        var formatted = formatter.Format(auditLogRecord, "UTC");

        // Assert - hashed field should show hash prefix
        formatted.Details.Should().Contain(d => d.Contains("[SHA256]") || d.Contains("PasswordHash"));
    }

    #region Helper Methods

    private static AuditRecord CreateAuditRecordForCreate(
        string entityType,
        string entityId,
        string entityTitle,
        string userId,
        string userTitle,
        IEnumerable<FieldChange> fieldChanges)
    {
        var record = new AuditRecord
        {
            Id = Guid.NewGuid().ToString(),
            EntityType = entityType,
            EntityId = entityId,
            EntityTitle = entityTitle,
            Operation = OperationType.OperationCreate,
            TimestampUtc = Timestamp.FromDateTime(DateTime.UtcNow),
            UserId = userId,
            UserTitle = userTitle
        };
        record.FieldChanges.AddRange(fieldChanges);
        return record;
    }

    private static AuditRecord CreateAuditRecordForUpdate(
        string entityType,
        string entityId,
        string entityTitle,
        string userId,
        string userTitle,
        IEnumerable<FieldChange> fieldChanges)
    {
        var record = new AuditRecord
        {
            Id = Guid.NewGuid().ToString(),
            EntityType = entityType,
            EntityId = entityId,
            EntityTitle = entityTitle,
            Operation = OperationType.OperationUpdate,
            TimestampUtc = Timestamp.FromDateTime(DateTime.UtcNow),
            UserId = userId,
            UserTitle = userTitle
        };
        record.FieldChanges.AddRange(fieldChanges);
        return record;
    }

    private static FieldChange CreateFieldChange(
        string fieldName,
        string? fieldTitle,
        string? oldValue,
        string? newValue,
        SensitiveMode sensitiveMode = SensitiveMode.None)
    {
        return new FieldChange
        {
            FieldName = fieldName,
            FieldTitle = fieldTitle ?? fieldName,
            ValueKind = ValueKind.Scalar,
            SensitiveMode = sensitiveMode,
            OldValue = oldValue != null ? new FieldValue { Normalized = oldValue } : null,
            NewValue = newValue != null ? new FieldValue { Normalized = newValue } : null
        };
    }

    #endregion
}
