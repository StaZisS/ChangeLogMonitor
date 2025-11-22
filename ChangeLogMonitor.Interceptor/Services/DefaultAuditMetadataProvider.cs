namespace ChangeLogMonitor.Interceptor.Services;

/// <summary>
///     Реализация провайдера метаданных по умолчанию
///     Возвращает системную информацию
/// </summary>
public class DefaultAuditMetadataProvider : IAuditMetadataProvider
{
    public string GetUserId()
    {
        return "system";
    }

    public string GetUserName()
    {
        return "System";
    }

    public string? GetRequestId()
    {
        return null;
    }

    public string? GetServiceName()
    {
        return Environment.GetEnvironmentVariable("SERVICE_NAME");
    }

    public string? GetClientIp()
    {
        return null;
    }

    public string? GetUserAgent()
    {
        return null;
    }

    public Dictionary<string, string>? GetHints()
    {
        return null;
    }
}