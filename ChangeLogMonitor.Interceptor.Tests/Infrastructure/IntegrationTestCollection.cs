using Xunit;

namespace ChangeLogMonitor.Interceptor.Tests.Infrastructure;

/// <summary>
///     Collection definition для интеграционных тестов.
///     Все тесты в этой коллекции используют общий PostgreSQL контейнер
///     и выполняются последовательно (не параллельно),
///     чтобы избежать конфликтов при работе с общей базой данных.
/// </summary>
[CollectionDefinition("IntegrationTests", DisableParallelization = true)]
public class IntegrationTestCollection : ICollectionFixture<PostgreSqlFixture>
{
}