using ChangeLogMonitor.Interceptor.Services;
using FluentAssertions;
using Xunit;

namespace ChangeLogMonitor.Interceptor.Tests;

/// <summary>
///     Unit-тесты для AuditScope
/// </summary>
public class AuditScopeTests
{
    [Fact]
    public void Begin_ShouldSetCurrentScope()
    {
        // Arrange & Act
        using var scope = AuditScope.Begin();

        // Assert
        AuditScope.Current.Should().BeSameAs(scope);
    }

    [Fact]
    public void Dispose_ShouldClearCurrentScope()
    {
        // Arrange
        var scope = AuditScope.Begin();

        // Act
        scope.Dispose();

        // Assert
        AuditScope.Current.Should().BeNull();
    }

    [Fact]
    public void SetReason_ShouldSetAndReturnScope()
    {
        // Arrange
        using var scope = AuditScope.Begin();

        // Act
        var result = scope.SetReason("Test reason");

        // Assert
        result.Should().BeSameAs(scope);
        scope.Reason.Should().Be("Test reason");
    }

    [Fact]
    public void SetTargetEntity_ShouldSetAndReturnScope()
    {
        // Arrange
        using var scope = AuditScope.Begin();

        // Act
        var result = scope.SetTargetEntity("orders");

        // Assert
        result.Should().BeSameAs(scope);
        scope.TargetEntity.Should().Be("orders");
    }

    [Fact]
    public void AddHint_ShouldAddHintAndReturnScope()
    {
        // Arrange
        using var scope = AuditScope.Begin();

        // Act
        var result = scope.AddHint("key1", "value1");

        // Assert
        result.Should().BeSameAs(scope);
        scope.Hints.Should().ContainKey("key1").WhoseValue.Should().Be("value1");
    }

    [Fact]
    public void FluentApi_ShouldWorkCorrectly()
    {
        // Arrange & Act
        using var scope = AuditScope.Begin()
            .SetReason("Bulk update")
            .SetTargetEntity("users")
            .AddHint("batch_id", "123")
            .AddHint("source", "api");

        // Assert
        scope.Reason.Should().Be("Bulk update");
        scope.TargetEntity.Should().Be("users");
        scope.Hints.Should().HaveCount(2);
        scope.Hints["batch_id"].Should().Be("123");
        scope.Hints["source"].Should().Be("api");
    }

    [Fact]
    public void NestedScopes_ShouldRestoreParentOnDispose()
    {
        // Arrange
        using var outerScope = AuditScope.Begin()
            .SetReason("Outer")
            .SetTargetEntity("outer_table");

        // Act
        using (var innerScope = AuditScope.Begin()
                   .SetReason("Inner")
                   .SetTargetEntity("inner_table"))
        {
            AuditScope.Current.Should().BeSameAs(innerScope);
            AuditScope.Current!.Reason.Should().Be("Inner");
        }

        // Assert - после dispose внутреннего scope должен восстановиться внешний
        AuditScope.Current.Should().BeSameAs(outerScope);
        AuditScope.Current!.Reason.Should().Be("Outer");
    }

    [Fact]
    public void GetEffectiveReason_ShouldReturnFirstNonNullReason()
    {
        // Arrange
        using var outerScope = AuditScope.Begin();
        outerScope.SetReason("Outer reason");

        using var innerScope = AuditScope.Begin();
        // Inner scope не имеет reason

        // Act
        var effectiveReason = (AuditScope.Current!).GetEffectiveReason();

        // Assert
        effectiveReason.Should().Be("Outer reason");
    }

    [Fact]
    public void GetEffectiveReason_ShouldReturnInnerReasonWhenSet()
    {
        // Arrange
        using var outerScope = AuditScope.Begin();
        outerScope.SetReason("Outer reason");

        using var innerScope = AuditScope.Begin();
        innerScope.SetReason("Inner reason");

        // Act
        var effectiveReason = (AuditScope.Current!).GetEffectiveReason();

        // Assert
        effectiveReason.Should().Be("Inner reason");
    }

    [Fact]
    public void GetEffectiveTargetEntity_ShouldReturnFirstNonNullEntity()
    {
        // Arrange
        using var outerScope = AuditScope.Begin();
        outerScope.SetTargetEntity("outer_table");

        using var innerScope = AuditScope.Begin();
        // Inner scope не имеет target entity

        // Act
        var effectiveEntity = (AuditScope.Current!).GetEffectiveTargetEntity();

        // Assert
        effectiveEntity.Should().Be("outer_table");
    }

    [Fact]
    public void GetAllHints_ShouldMergeHintsFromAllScopes()
    {
        // Arrange
        using var outerScope = AuditScope.Begin();
        outerScope.AddHint("outer_key", "outer_value");
        outerScope.AddHint("shared_key", "outer_shared");

        using var innerScope = AuditScope.Begin();
        innerScope.AddHint("inner_key", "inner_value");
        innerScope.AddHint("shared_key", "inner_shared"); // перезаписывает

        // Act
        var allHints = (AuditScope.Current!).GetAllHints();

        // Assert
        allHints.Should().HaveCount(3);
        allHints["outer_key"].Should().Be("outer_value");
        allHints["inner_key"].Should().Be("inner_value");
        allHints["shared_key"].Should().Be("inner_shared"); // inner перезаписывает outer
    }

    [Fact]
    public async Task AsyncLocal_ShouldWorkAcrossAsyncCalls()
    {
        // Arrange
        using var scope = AuditScope.Begin()
            .SetReason("Async test")
            .SetTargetEntity("async_table");

        // Act
        await Task.Delay(10);
        var scopeAfterAwait = AuditScope.Current;

        // Assert
        scopeAfterAwait.Should().BeSameAs(scope);
        scopeAfterAwait!.Reason.Should().Be("Async test");
    }

    [Fact]
    public async Task AsyncLocal_ShouldBeIsolatedBetweenTasks()
    {
        // Arrange & Act
        var task1 = Task.Run(() =>
        {
            using var scope = AuditScope.Begin().SetReason("Task1");
            Thread.Sleep(50);
            return AuditScope.Current?.Reason;
        });

        var task2 = Task.Run(() =>
        {
            using var scope = AuditScope.Begin().SetReason("Task2");
            Thread.Sleep(50);
            return AuditScope.Current?.Reason;
        });

        var results = await Task.WhenAll(task1, task2);

        // Assert
        results[0].Should().Be("Task1");
        results[1].Should().Be("Task2");
    }
}
