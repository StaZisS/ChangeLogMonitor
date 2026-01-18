using Xunit;

namespace ChangeLogMonitor.Interceptor.Tests.Infrastructure;

[CollectionDefinition("IntegrationTests", DisableParallelization = true)]
public class IntegrationTestCollection : ICollectionFixture<PostgreSqlFixture>
{
}