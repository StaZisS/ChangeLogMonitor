using Audit.V1;
using Auditmeta.Raw;
using ChangeLogMonitor.Core.Enums;
using ChangeLogMonitor.Finalization.Models;
using ChangeLogMonitor.Finalization.Services;
using ChangeLogMonitor.Integration.Tests.Infrastructure;
using ChangeLogMonitor.Integration.Tests.TestEntities;
using FluentAssertions;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Xunit;

namespace ChangeLogMonitor.Integration.Tests;

[Collection("IntegrationTests")]
public class EndToEndIntegrationTests : IntegrationTestBase
{
    private const string ConfigPath = "TestConfigs/e2e-config.yaml";

    public EndToEndIntegrationTests(PostgreSqlFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task FullChain_CreateCustomer_ProducesCorrectFormattedOutput()
    {
        MetadataProvider.SetUser("user-123", "Иван Петров");
        MetadataProvider.AddHint("source", "web-app");

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

        var auditLogs = await GetAllAuditLogsAsync();
        auditLogs.Should().HaveCount(1, "Interceptor should create one audit log entry per SaveChanges");

        var auditLog = auditLogs[0];
        auditLog.TransactionId.Should().NotBeNullOrEmpty();
        auditLog.Payload.Should().NotBeEmpty();

        var envelope = AuditMetaEnvelope.Parser.ParseFrom(auditLog.Payload);
        envelope.TransactionId.Should().Be(auditLog.TransactionId);
        envelope.Actor.UserId.Should().Be("user-123");
        envelope.Actor.UserName.Should().Be("Иван Петров");
        envelope.Request.Should().NotBeNull();
        envelope.Request.ServiceName.Should().Be("IntegrationTests");
        envelope.Hints.Should().Contain(h => h.Key == "source" && h.Value == "web-app");

        var auditRecord = CreateAuditRecordForCreate(
            "customers",
            customer.Id.ToString(),
            customer.Name,
            envelope.Actor.UserId,
            envelope.Actor.UserName,
            new[]
            {
                CreateFieldChange("name", "Имя клиента", null, customer.Name),
                CreateFieldChange("email", "Электронная почта", null, customer.Email),
                CreateFieldChange("phone", "Телефон", null, customer.Phone),
                CreateFieldChange("password_hash", null, null, "***HASHED***", SensitiveMode.Hashed),
                CreateFieldChange("is_active", "Активен", null, customer.IsActive.ToString())
            });

        var auditLogRecord = new AuditLogRecord(
            auditLog.Id,
            auditLog.CreatedAt,
            envelope.Actor.UserId,
            envelope.Actor.UserName,
            "customers",
            OperationCode.Create,
            customer.Id.ToString(),
            auditLog.TransactionId,
            Convert.ToBase64String(auditRecord.ToByteArray()));

        var configService = CreateConfigurationService(ConfigPath);
        var formatter = new AuditLogFormatter(configService);
        var formatted = formatter.Format(auditLogRecord, "Europe/Moscow");

        formatted.LogId.Should().Be(auditLog.Id);
        formatted.TableName.Should().Be("customers");
        formatted.Operation.Should().Be("CREATE");
        formatted.EntityId.Should().Be(customer.Id.ToString());
        formatted.EntityTitle.Should().Be("ООО Рога и Копыта");
        formatted.UserId.Should().Be("user-123");
        formatted.UserTitle.Should().Be("Иван Петров");
        formatted.Summary.Should().Contain("Создана запись");
        formatted.Summary.Should().Contain("Иван Петров");
        formatted.Details.Should().NotBeEmpty();
        formatted.Details.Should().Contain(d => d.Contains("Имя клиента"));
        formatted.Details.Should().Contain(d => d.Contains("ООО Рога и Копыта"));
    }

    [Fact]
    public async Task FullChain_UpdateOrder_ProducesCorrectFormattedOutput()
    {
        MetadataProvider.SetUser("admin-456", "Администратор");

        await using var appContext = CreateAppDbContext(ConfigPath);

        var customer = new Customer
        {
            Name = "Тестовый Клиент",
            Email = "test@example.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        appContext.Customers.Add(customer);
        await appContext.SaveChangesAsync();

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

        order.Status = "Processing";
        order.TotalAmount = 1500.00m;
        order.UpdatedAt = DateTime.UtcNow;
        await appContext.SaveChangesAsync();

        var auditLogs = await GetAllAuditLogsAsync();
        auditLogs.Should().HaveCount(3, "Should have 3 audit logs: customer create, order create, order update");
        var updateAuditLog = auditLogs.Last();

        var envelope = AuditMetaEnvelope.Parser.ParseFrom(updateAuditLog.Payload);
        envelope.Actor.UserId.Should().Be("admin-456");
        envelope.Actor.UserName.Should().Be("Администратор");

        var auditRecord = CreateAuditRecordForUpdate(
            "orders",
            order.Id.ToString(),
            order.OrderNumber,
            envelope.Actor.UserId,
            envelope.Actor.UserName,
            new[]
            {
                CreateFieldChange("status", "Статус", "New", "Processing"),
                CreateFieldChange("total_amount", "Сумма заказа", "1000.00", "1500.00")
            });

        var auditLogRecord = new AuditLogRecord(
            updateAuditLog.Id,
            updateAuditLog.CreatedAt,
            envelope.Actor.UserId,
            envelope.Actor.UserName,
            "orders",
            OperationCode.Update,
            order.Id.ToString(),
            updateAuditLog.TransactionId,
            Convert.ToBase64String(auditRecord.ToByteArray()));

        var configService = CreateConfigurationService(ConfigPath);
        var formatter = new AuditLogFormatter(configService);
        var formatted = formatter.Format(auditLogRecord, "Europe/Moscow");

        formatted.Operation.Should().Be("UPDATE");
        formatted.EntityTitle.Should().Be("ORD-001");
        formatted.Summary.Should().Contain("Изменена запись");
        formatted.Details.Should().Contain(d => d.Contains("Статус"));
        formatted.Details.Should().Contain(d => d.Contains("New") && d.Contains("Processing"));
    }

    [Fact]
    public async Task FullChain_ComplexTransaction_MultipleEntities_AllAudited()
    {
        MetadataProvider.SetUser("operator-789", "Оператор склада");
        MetadataProvider.AddHint("transaction_type", "order_fulfillment");

        await using var appContext = CreateAppDbContext(ConfigPath);

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

        await appContext.SaveChangesAsync();

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

        var auditLogs = await GetAllAuditLogsAsync();
        auditLogs.Should().HaveCount(3, "Should have 3 audit logs (one per SaveChanges call)");

        foreach (var log in auditLogs)
        {
            var envelope = AuditMetaEnvelope.Parser.ParseFrom(log.Payload);
            envelope.Actor.UserId.Should().Be("operator-789");
            envelope.TransactionId.Should().NotBeNullOrEmpty();
        }

        var lastLog = auditLogs.Last();
        var envelope2 = AuditMetaEnvelope.Parser.ParseFrom(lastLog.Payload);

        var auditRecord = CreateAuditRecordForCreate(
            "order_items",
            item2.Id.ToString(),
            $"Товар B x{item2.Quantity}",
            envelope2.Actor.UserId,
            envelope2.Actor.UserName,
            new[]
            {
                CreateFieldChange("quantity", "Количество", null, item2.Quantity.ToString()),
                CreateFieldChange("unit_price", "Цена за единицу", null, item2.UnitPrice.ToString("F2"))
            });

        var auditLogRecord = new AuditLogRecord(
            lastLog.Id,
            lastLog.CreatedAt,
            envelope2.Actor.UserId,
            envelope2.Actor.UserName,
            "order_items",
            OperationCode.Create,
            item2.Id.ToString(),
            lastLog.TransactionId,
            Convert.ToBase64String(auditRecord.ToByteArray()));

        var configService = CreateConfigurationService(ConfigPath);
        var formatter = new AuditLogFormatter(configService);
        var formatted = formatter.Format(auditLogRecord, "UTC");

        formatted.Operation.Should().Be("CREATE");
        formatted.Details.Should().Contain(d => d.Contains("Количество"));
    }

    [Fact]
    public async Task FullChain_DeleteEntity_ProducesCorrectFormattedOutput()
    {
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

        appContext.Products.Remove(product);
        await appContext.SaveChangesAsync();

        var auditLogs = await GetAllAuditLogsAsync();
        auditLogs.Should().HaveCount(2, "Should have 2 audit logs: create and delete");
        var deleteAuditLog = auditLogs.Last();

        var envelope = AuditMetaEnvelope.Parser.ParseFrom(deleteAuditLog.Payload);
        envelope.Actor.UserName.Should().Be("Менеджер");

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
            deleteAuditLog.Id,
            deleteAuditLog.CreatedAt,
            envelope.Actor.UserId,
            envelope.Actor.UserName,
            "products",
            OperationCode.Delete,
            productId.ToString(),
            deleteAuditLog.TransactionId,
            Convert.ToBase64String(auditRecord.ToByteArray()));

        var configService = CreateConfigurationService(ConfigPath);
        var formatter = new AuditLogFormatter(configService);
        var formatted = formatter.Format(auditLogRecord, "Europe/Moscow");

        formatted.Operation.Should().Be("DELETE");
        formatted.EntityTitle.Should().Be("Удаляемый товар");
        formatted.Summary.Should().Contain("Удалена запись");
        formatted.Summary.Should().Contain("Менеджер");
    }

    [Fact]
    public async Task FullChain_SensitiveData_IsMaskedInOutput()
    {
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

        var auditRecord = CreateAuditRecordForCreate(
            "customers",
            customer.Id.ToString(),
            customer.Name,
            "admin",
            "System Admin",
            new[]
            {
                CreateFieldChange("name", "Имя клиента", null, customer.Name),
                CreateFieldChange("email", "Электронная почта", null, customer.Email),
                CreateFieldChange("password_hash", null, null, "a1b2c3d4e5f6...", SensitiveMode.Hashed)
            });

        var auditLogs = await GetAllAuditLogsAsync();
        var log = auditLogs.Last();

        var auditLogRecord = new AuditLogRecord(
            log.Id,
            log.CreatedAt,
            "admin",
            "System Admin",
            "customers",
            OperationCode.Create,
            customer.Id.ToString(),
            log.TransactionId,
            Convert.ToBase64String(auditRecord.ToByteArray()));

        var configService = CreateConfigurationService(ConfigPath);
        var formatter = new AuditLogFormatter(configService);
        var formatted = formatter.Format(auditLogRecord, "UTC");

        formatted.Details.Should().Contain(d => d.Contains("[SHA256]") || d.Contains("password_hash"));
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