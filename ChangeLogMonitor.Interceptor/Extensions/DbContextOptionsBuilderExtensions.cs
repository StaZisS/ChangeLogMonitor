using ChangeLogMonitor.Interceptor.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace ChangeLogMonitor.Interceptor.Extensions;

/// <summary>
/// Extension методы для DbContextOptionsBuilder
/// </summary>
public static class DbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Добавляет ChangeLogInterceptor к цепочке интерцепторов DbContext
    /// </summary>
    /// <param name="optionsBuilder">DbContext options builder</param>
    /// <param name="interceptor">Экземпляр ChangeLogInterceptor</param>
    /// <returns>DbContextOptionsBuilder для цепочки вызовов</returns>
    public static DbContextOptionsBuilder UseChangeLogInterceptor(
        this DbContextOptionsBuilder optionsBuilder,
        ChangeLogInterceptor interceptor)
    {
        return optionsBuilder.AddInterceptors(interceptor);
    }
}
