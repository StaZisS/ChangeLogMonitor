using Xunit;

namespace ChangeLogMonitor.Integration.Tests.Infrastructure;

/// <summary>
///     Коллекция тестов с общим PostgreSQL контейнером.
///     DisableParallelization = true для последовательного выполнения.
/// </summary>
[CollectionDefinition("IntegrationTests", DisableParallelization = true)]
public class IntegrationTestCollection : ICollectionFixture<PostgreSqlFixture>
{
}
