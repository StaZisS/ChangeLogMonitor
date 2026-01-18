using System.Collections.Concurrent;
using System.Diagnostics;
using Auditmeta.Raw;
using ChangeLogMonitor.Integration.Tests.Infrastructure;
using ChangeLogMonitor.Integration.Tests.TestEntities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace ChangeLogMonitor.Integration.Tests;

[Collection("IntegrationTests")]
public class LoadTests : IntegrationTestBase
{
    private const string ConfigPath = "TestConfigs/e2e-config.yaml";
    private readonly ITestOutputHelper _output;

    public LoadTests(PostgreSqlFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _output = output;
    }

    [Theory]
    [InlineData(10, 5)]
    [InlineData(50, 20)]
    [InlineData(100, 50)]
    public async Task LoadTest_ParallelCreates_AllChangesRecorded(int customerCount, int productCount)
    {
        MetadataProvider.SetUser("load-test-user", "Load Test Runner");
        var stopwatch = Stopwatch.StartNew();
        var createdCustomerIds = new ConcurrentBag<int>();
        var createdProductIds = new ConcurrentBag<int>();
        var saveChangesCount = 0;

        var customerTasks = Enumerable.Range(0, customerCount).Select(async i =>
        {
            await using var appContext = CreateAppDbContext(ConfigPath);
            var customer = new Customer
            {
                Name = $"LoadTest Customer {i}",
                Email = $"customer{i}@loadtest.com",
                Phone = $"+7-999-{i:D7}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            appContext.Customers.Add(customer);
            await appContext.SaveChangesAsync();
            Interlocked.Increment(ref saveChangesCount);
            createdCustomerIds.Add(customer.Id);
        });

        var productTasks = Enumerable.Range(0, productCount).Select(async i =>
        {
            await using var appContext = CreateAppDbContext(ConfigPath);
            var product = new Product
            {
                Name = $"LoadTest Product {i}",
                Description = $"Product description {i}",
                Price = 100.00m + i,
                StockQuantity = 100 + i,
                IsAvailable = true
            };
            appContext.Products.Add(product);
            await appContext.SaveChangesAsync();
            Interlocked.Increment(ref saveChangesCount);
            createdProductIds.Add(product.Id);
        });

        await Task.WhenAll(customerTasks.Concat(productTasks));
        stopwatch.Stop();

        var auditLogs = await GetAllAuditLogsAsync();

        _output.WriteLine("=== Load Test Results (Parallel Creates) ===");
        _output.WriteLine($"Customers created: {customerCount}");
        _output.WriteLine($"Products created: {productCount}");
        _output.WriteLine($"Total SaveChanges calls: {saveChangesCount}");
        _output.WriteLine($"Total audit log entries: {auditLogs.Count}");
        _output.WriteLine($"Elapsed time: {stopwatch.ElapsedMilliseconds}ms");

        auditLogs.Should().HaveCount(saveChangesCount,
            "каждый SaveChanges должен создавать одну запись в audit_log");

        foreach (var log in auditLogs)
        {
            log.TransactionId.Should().NotBeNullOrEmpty();
            log.Payload.Should().NotBeEmpty();

            var envelope = AuditMetaEnvelope.Parser.ParseFrom(log.Payload);
            envelope.TransactionId.Should().Be(log.TransactionId);
            envelope.Actor.UserId.Should().Be("load-test-user");
        }
    }

    [Theory]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(50)]
    public async Task LoadTest_FullCrudCycle_AllOperationsAudited(int iterations)
    {
        MetadataProvider.SetUser("crud-test-user", "CRUD Test Runner");
        var stopwatch = Stopwatch.StartNew();

        var expectedAuditLogs = 0;
        var operationCounts = new ConcurrentDictionary<string, int>();
        operationCounts["create"] = 0;
        operationCounts["update"] = 0;
        operationCounts["delete"] = 0;

        for (var i = 0; i < iterations; i++)
        {
            await using var appContext = CreateAppDbContext(ConfigPath);

            var customer = new Customer
            {
                Name = $"CRUD Customer {i}",
                Email = $"crud{i}@test.com",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            appContext.Customers.Add(customer);
            await appContext.SaveChangesAsync();
            expectedAuditLogs++;
            operationCounts.AddOrUpdate("create", 1, (_, v) => v + 1);

            customer.Name = $"CRUD Customer {i} - Updated";
            customer.IsActive = false;
            await appContext.SaveChangesAsync();
            expectedAuditLogs++;
            operationCounts.AddOrUpdate("update", 1, (_, v) => v + 1);

            appContext.Customers.Remove(customer);
            await appContext.SaveChangesAsync();
            expectedAuditLogs++;
            operationCounts.AddOrUpdate("delete", 1, (_, v) => v + 1);
        }

        stopwatch.Stop();

        var auditLogs = await GetAllAuditLogsAsync();

        _output.WriteLine("=== Load Test Results (Full CRUD Cycle) ===");
        _output.WriteLine($"Iterations: {iterations}");
        _output.WriteLine($"Creates: {operationCounts["create"]}");
        _output.WriteLine($"Updates: {operationCounts["update"]}");
        _output.WriteLine($"Deletes: {operationCounts["delete"]}");
        _output.WriteLine($"Expected audit logs: {expectedAuditLogs}");
        _output.WriteLine($"Actual audit logs: {auditLogs.Count}");
        _output.WriteLine($"Elapsed time: {stopwatch.ElapsedMilliseconds}ms");

        auditLogs.Should().HaveCount(expectedAuditLogs,
            "каждая операция (create/update/delete) должна создавать audit запись");

        var invalidPayloads = 0;
        foreach (var log in auditLogs)
            try
            {
                var envelope = AuditMetaEnvelope.Parser.ParseFrom(log.Payload);
                envelope.TransactionId.Should().NotBeNullOrEmpty();
            }
            catch
            {
                invalidPayloads++;
            }

        invalidPayloads.Should().Be(0, "все payload должны быть валидными protobuf сообщениями");
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(10, 5)]
    [InlineData(20, 10)]
    public async Task LoadTest_ComplexTransactions_OneAuditLogPerSaveChanges(int orderCount, int itemsPerOrder)
    {
        MetadataProvider.SetUser("complex-tx-user", "Complex Transaction Runner");
        var stopwatch = Stopwatch.StartNew();

        await using var setupContext = CreateAppDbContext(ConfigPath);
        var customer = new Customer
        {
            Name = "Complex TX Customer",
            Email = "complex@tx.com",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        setupContext.Customers.Add(customer);

        var products = Enumerable.Range(0, itemsPerOrder).Select(i => new Product
        {
            Name = $"TX Product {i}",
            Price = 50.00m + i * 10,
            StockQuantity = 100,
            IsAvailable = true
        }).ToList();
        setupContext.Products.AddRange(products);
        await setupContext.SaveChangesAsync();

        var expectedAuditLogs = 1;

        for (var i = 0; i < orderCount; i++)
        {
            await using var orderContext = CreateAppDbContext(ConfigPath);

            var order = new Order
            {
                OrderNumber = $"LOAD-{i:D4}",
                CustomerId = customer.Id,
                TotalAmount = products.Sum(p => p.Price),
                Status = "New",
                CreatedAt = DateTime.UtcNow
            };
            orderContext.Orders.Add(order);

            foreach (var product in products)
                orderContext.OrderItems.Add(new OrderItem
                {
                    Order = order,
                    ProductId = product.Id,
                    Quantity = 1,
                    UnitPrice = product.Price
                });

            await orderContext.SaveChangesAsync();
            expectedAuditLogs++;
        }

        stopwatch.Stop();

        var auditLogs = await GetAllAuditLogsAsync();

        _output.WriteLine("=== Load Test Results (Complex Transactions) ===");
        _output.WriteLine($"Orders created: {orderCount}");
        _output.WriteLine($"Items per order: {itemsPerOrder}");
        _output.WriteLine($"Total entities: {orderCount * (1 + itemsPerOrder) + 1 + itemsPerOrder}");
        _output.WriteLine($"Expected audit logs: {expectedAuditLogs}");
        _output.WriteLine($"Actual audit logs: {auditLogs.Count}");
        _output.WriteLine($"Elapsed time: {stopwatch.ElapsedMilliseconds}ms");

        auditLogs.Should().HaveCount(expectedAuditLogs,
            "каждый SaveChanges (даже с несколькими сущностями) должен создавать одну audit запись");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    public async Task LoadTest_ConcurrentUpdates_AllUpdatesAudited(int concurrentUpdates)
    {
        MetadataProvider.SetUser("concurrent-update-user", "Concurrent Update Runner");

        await using var setupContext = CreateAppDbContext(ConfigPath);
        var product = new Product
        {
            Name = "Concurrent Update Product",
            Price = 100.00m,
            StockQuantity = 1000,
            IsAvailable = true
        };
        setupContext.Products.Add(product);
        await setupContext.SaveChangesAsync();
        var productId = product.Id;

        var stopwatch = Stopwatch.StartNew();

        var updateTasks = Enumerable.Range(0, concurrentUpdates).Select(async i =>
        {
            await using var updateContext = CreateAppDbContext(ConfigPath);
            var p = await updateContext.Products.FindAsync(productId);
            if (p != null)
            {
                p.StockQuantity -= 1;
                p.Price += 0.01m * i;
                await updateContext.SaveChangesAsync();
            }
        });

        var results = await Task.WhenAll(updateTasks.Select(async t =>
        {
            try
            {
                await t;
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                return false;
            }
        }));

        stopwatch.Stop();

        var successfulUpdates = results.Count(r => r);
        var auditLogs = await GetAllAuditLogsAsync();
        var totalExpectedLogs = 1 + successfulUpdates;

        _output.WriteLine("=== Load Test Results (Concurrent Updates) ===");
        _output.WriteLine($"Concurrent update attempts: {concurrentUpdates}");
        _output.WriteLine($"Successful updates: {successfulUpdates}");
        _output.WriteLine($"Expected audit logs: {totalExpectedLogs} (1 create + {successfulUpdates} updates)");
        _output.WriteLine($"Actual audit logs: {auditLogs.Count}");
        _output.WriteLine($"Elapsed time: {stopwatch.ElapsedMilliseconds}ms");

        auditLogs.Should().HaveCount(totalExpectedLogs,
            "1 создание + {0} обновлений должны создать {1} audit записей", successfulUpdates, totalExpectedLogs);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task LoadTest_BulkCreate_VerifyDataIntegrity(int entityCount)
    {
        MetadataProvider.SetUser("integrity-test-user", "Data Integrity Tester");
        var stopwatch = Stopwatch.StartNew();

        var expectedData = new Dictionary<int, (string Name, string Email)>();
        var customerIds = new List<int>();

        for (var i = 0; i < entityCount; i++)
        {
            await using var appContext = CreateAppDbContext(ConfigPath);
            var customer = new Customer
            {
                Name = $"Integrity Customer {i}",
                Email = $"integrity{i}@test.com",
                Phone = $"+7-999-{i:D7}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            appContext.Customers.Add(customer);
            await appContext.SaveChangesAsync();

            expectedData[customer.Id] = (customer.Name, customer.Email);
            customerIds.Add(customer.Id);
        }

        stopwatch.Stop();

        var auditLogs = await GetAllAuditLogsAsync();

        _output.WriteLine("=== Load Test Results (Data Integrity) ===");
        _output.WriteLine($"Entities created: {entityCount}");
        _output.WriteLine($"Audit logs: {auditLogs.Count}");
        _output.WriteLine($"Elapsed time: {stopwatch.ElapsedMilliseconds}ms");

        auditLogs.Should().HaveCount(entityCount);

        var validPayloads = 0;
        var uniqueTransactionIds = new HashSet<string>();

        foreach (var log in auditLogs)
        {
            var envelope = AuditMetaEnvelope.Parser.ParseFrom(log.Payload);

            envelope.Should().NotBeNull();
            envelope.TransactionId.Should().NotBeNullOrEmpty();
            envelope.Actor.UserId.Should().Be("integrity-test-user");
            envelope.Actor.UserName.Should().Be("Data Integrity Tester");

            uniqueTransactionIds.Add(log.TransactionId);
            validPayloads++;
        }

        validPayloads.Should().Be(entityCount);
        uniqueTransactionIds.Should().HaveCount(entityCount,
            "каждая транзакция должна иметь уникальный ID");
    }

    [Theory]
    [InlineData(10, 10, 5)]
    [InlineData(25, 20, 10)]
    [InlineData(50, 40, 20)]
    public async Task LoadTest_MixedWorkload_AllOperationsTracked(
        int createCount, int updateCount, int deleteCount)
    {
        MetadataProvider.SetUser("mixed-workload-user", "Mixed Workload Runner");
        var stopwatch = Stopwatch.StartNew();
        var expectedAuditLogs = 0;

        var createdIds = new List<int>();
        for (var i = 0; i < createCount; i++)
        {
            await using var ctx = CreateAppDbContext(ConfigPath);
            var product = new Product
            {
                Name = $"Mixed Product {i}",
                Price = 100 + i,
                StockQuantity = 50,
                IsAvailable = true
            };
            ctx.Products.Add(product);
            await ctx.SaveChangesAsync();
            createdIds.Add(product.Id);
            expectedAuditLogs++;
        }

        var updateTargets = createdIds.Take(Math.Min(updateCount, createdIds.Count)).ToList();
        foreach (var id in updateTargets)
        {
            await using var ctx = CreateAppDbContext(ConfigPath);
            var product = await ctx.Products.FindAsync(id);
            if (product != null)
            {
                product.Price += 10;
                product.StockQuantity -= 5;
                await ctx.SaveChangesAsync();
                expectedAuditLogs++;
            }
        }

        var deleteTargets = createdIds.TakeLast(Math.Min(deleteCount, createdIds.Count)).ToList();
        foreach (var id in deleteTargets)
        {
            await using var ctx = CreateAppDbContext(ConfigPath);
            var product = await ctx.Products.FindAsync(id);
            if (product != null)
            {
                ctx.Products.Remove(product);
                await ctx.SaveChangesAsync();
                expectedAuditLogs++;
            }
        }

        stopwatch.Stop();

        var auditLogs = await GetAllAuditLogsAsync();

        _output.WriteLine("=== Load Test Results (Mixed Workload) ===");
        _output.WriteLine($"Creates: {createCount}");
        _output.WriteLine($"Updates: {updateTargets.Count}");
        _output.WriteLine($"Deletes: {deleteTargets.Count}");
        _output.WriteLine($"Expected audit logs: {expectedAuditLogs}");
        _output.WriteLine($"Actual audit logs: {auditLogs.Count}");
        _output.WriteLine($"Elapsed time: {stopwatch.ElapsedMilliseconds}ms");

        auditLogs.Should().HaveCount(expectedAuditLogs,
            "все операции должны быть записаны в audit_log");
    }

    [Fact]
    public async Task StressTest_HighConcurrency_SystemRemainConsistent()
    {
        MetadataProvider.SetUser("stress-test-user", "Stress Test Runner");
        var stopwatch = Stopwatch.StartNew();

        var completedOperations = 0;
        var failedOperations = 0;

        var customerIds = new ConcurrentBag<int>();
        var productIds = new ConcurrentBag<int>();

        var createTasks = new List<Task>();

        for (var i = 0; i < 20; i++)
            createTasks.Add(Task.Run(async () =>
            {
                try
                {
                    await using var ctx = CreateAppDbContext(ConfigPath);
                    var customer = new Customer
                    {
                        Name = $"Stress Customer {Guid.NewGuid():N}",
                        Email = $"stress{Guid.NewGuid():N}@test.com",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    ctx.Customers.Add(customer);
                    await ctx.SaveChangesAsync();
                    customerIds.Add(customer.Id);
                    Interlocked.Increment(ref completedOperations);
                }
                catch
                {
                    Interlocked.Increment(ref failedOperations);
                }
            }));

        for (var i = 0; i < 30; i++)
            createTasks.Add(Task.Run(async () =>
            {
                try
                {
                    await using var ctx = CreateAppDbContext(ConfigPath);
                    var product = new Product
                    {
                        Name = $"Stress Product {Guid.NewGuid():N}",
                        Price = Random.Shared.Next(10, 1000),
                        StockQuantity = Random.Shared.Next(1, 100),
                        IsAvailable = true
                    };
                    ctx.Products.Add(product);
                    await ctx.SaveChangesAsync();
                    productIds.Add(product.Id);
                    Interlocked.Increment(ref completedOperations);
                }
                catch
                {
                    Interlocked.Increment(ref failedOperations);
                }
            }));

        await Task.WhenAll(createTasks);

        var mixedTasks = new List<Task>();

        foreach (var id in productIds.Take(25))
            mixedTasks.Add(Task.Run(async () =>
            {
                try
                {
                    await using var ctx = CreateAppDbContext(ConfigPath);
                    var product = await ctx.Products.FindAsync(id);
                    if (product != null)
                    {
                        product.Price += Random.Shared.Next(1, 50);
                        await ctx.SaveChangesAsync();
                        Interlocked.Increment(ref completedOperations);
                    }
                }
                catch
                {
                    Interlocked.Increment(ref failedOperations);
                }
            }));

        for (var i = 0; i < 25; i++)
            mixedTasks.Add(Task.Run(async () =>
            {
                try
                {
                    await using var ctx = CreateAppDbContext(ConfigPath);
                    var product = new Product
                    {
                        Name = $"Stress Product Extra {Guid.NewGuid():N}",
                        Price = Random.Shared.Next(10, 500),
                        StockQuantity = Random.Shared.Next(1, 50),
                        IsAvailable = true
                    };
                    ctx.Products.Add(product);
                    await ctx.SaveChangesAsync();
                    Interlocked.Increment(ref completedOperations);
                }
                catch
                {
                    Interlocked.Increment(ref failedOperations);
                }
            }));

        await Task.WhenAll(mixedTasks);
        stopwatch.Stop();

        var auditLogs = await GetAllAuditLogsAsync();

        _output.WriteLine("=== Stress Test Results ===");
        _output.WriteLine($"Completed operations: {completedOperations}");
        _output.WriteLine($"Failed operations: {failedOperations}");
        _output.WriteLine($"Audit log entries: {auditLogs.Count}");
        _output.WriteLine($"Elapsed time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Throughput: {completedOperations * 1000.0 / stopwatch.ElapsedMilliseconds:F2} ops/sec");

        auditLogs.Should().HaveCount(completedOperations,
            "количество audit записей должно соответствовать успешным операциям");

        var invalidCount = 0;
        foreach (var log in auditLogs)
            try
            {
                var envelope = AuditMetaEnvelope.Parser.ParseFrom(log.Payload);
                if (string.IsNullOrEmpty(envelope.TransactionId))
                    invalidCount++;
            }
            catch
            {
                invalidCount++;
            }

        invalidCount.Should().Be(0, "все audit записи должны быть валидными");
    }

    [Fact]
    public async Task LoadTest_TransactionIds_AreUnique()
    {
        MetadataProvider.SetUser("tx-id-test-user", "Transaction ID Tester");
        const int operationCount = 50;

        for (var i = 0; i < operationCount; i++)
        {
            await using var ctx = CreateAppDbContext(ConfigPath);
            ctx.Products.Add(new Product
            {
                Name = $"TxId Product {i}",
                Price = 100 + i,
                StockQuantity = 10,
                IsAvailable = true
            });
            await ctx.SaveChangesAsync();
        }

        var auditLogs = await GetAllAuditLogsAsync();
        var transactionIds = auditLogs.Select(l => l.TransactionId).ToList();
        var uniqueTransactionIds = transactionIds.Distinct().ToList();

        _output.WriteLine("=== Transaction ID Uniqueness Test ===");
        _output.WriteLine($"Operations: {operationCount}");
        _output.WriteLine($"Total transaction IDs: {transactionIds.Count}");
        _output.WriteLine($"Unique transaction IDs: {uniqueTransactionIds.Count}");

        uniqueTransactionIds.Should().HaveCount(transactionIds.Count,
            "все transaction_id должны быть уникальными");
    }

    [Fact]
    public async Task LoadTest_Timestamps_AreInChronologicalOrder()
    {
        MetadataProvider.SetUser("timestamp-test-user", "Timestamp Tester");
        const int operationCount = 30;

        for (var i = 0; i < operationCount; i++)
        {
            await using var ctx = CreateAppDbContext(ConfigPath);
            ctx.Customers.Add(new Customer
            {
                Name = $"Timestamp Customer {i}",
                Email = $"ts{i}@test.com",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
            await Task.Delay(10);
        }

        var auditLogs = await GetAllAuditLogsAsync();

        _output.WriteLine("=== Timestamp Order Test ===");
        _output.WriteLine($"Operations: {operationCount}");
        _output.WriteLine($"Audit logs: {auditLogs.Count}");

        var timestamps = auditLogs.Select(l => l.CreatedAt).ToList();
        var sortedTimestamps = timestamps.OrderBy(t => t).ToList();

        timestamps.Should().Equal(sortedTimestamps,
            "audit записи должны быть в хронологическом порядке");

        foreach (var log in auditLogs)
        {
            log.CreatedAt.Should().BeAfter(DateTime.UtcNow.AddHours(-1),
                "timestamp должен быть недавним");
            log.CreatedAt.Should().BeBefore(DateTime.UtcNow.AddMinutes(1),
                "timestamp не должен быть в будущем");
        }
    }
}