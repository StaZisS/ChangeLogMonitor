namespace ChangeLogMonitor.Interceptor.Services;

/// <summary>
/// Реализация провайдера метаданных по умолчанию
/// Возвращает системную информацию
/// </summary>
public class DefaultAuditMetadataProvider : IAuditMetadataProvider
{
    public string GetUserId() => "system";

    public string GetUserName() => "System";

    public string? GetRequestId() => null;

    public string? GetServiceName() => Environment.GetEnvironmentVariable("SERVICE_NAME");

    public string? GetClientIp() => null;

    public string? GetUserAgent() => null;

    public Dictionary<string, string>? GetHints() => null;
}
