using Auditmeta.Raw;
using Google.Protobuf;

namespace ChangeLogMonitor.Interceptor.Services;

/// <summary>
///     Сервис для сериализации метаданных аудита в protobuf
/// </summary>
public class AuditMetadataSerializer
{
    private readonly IAuditMetadataProvider _metadataProvider;

    public AuditMetadataSerializer(IAuditMetadataProvider metadataProvider)
    {
        _metadataProvider = metadataProvider ?? throw new ArgumentNullException(nameof(metadataProvider));
    }

    /// <summary>
    ///     Создает и сериализует AuditMetaEnvelope
    /// </summary>
    /// <param name="transactionId">ID транзакции</param>
    /// <returns>Сериализованный protobuf как массив байт</returns>
    public byte[] Serialize(string transactionId)
    {
        var envelope = new AuditMetaEnvelope
        {
            TransactionId = transactionId,
            CreatedAtUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Actor = new UserInfo
            {
                UserId = _metadataProvider.GetUserId(),
                UserName = _metadataProvider.GetUserName()
            }
        };

        // Добавляем контекст запроса (если доступен)
        var requestId = _metadataProvider.GetRequestId();
        var serviceName = _metadataProvider.GetServiceName();
        var clientIp = _metadataProvider.GetClientIp();
        var userAgent = _metadataProvider.GetUserAgent();

        if (requestId != null || serviceName != null || clientIp != null || userAgent != null)
        {
            envelope.Request = new RequestContext
            {
                RequestId = requestId ?? string.Empty
            };

            if (serviceName != null)
                envelope.Request.ServiceName = serviceName;

            if (clientIp != null)
                envelope.Request.ClientIp = clientIp;

            if (userAgent != null)
                envelope.Request.UserAgent = userAgent;
        }

        // Добавляем подсказки (если есть)
        var hints = _metadataProvider.GetHints();
        if (hints != null && hints.Count > 0)
            foreach (var hint in hints)
                envelope.Hints.Add(new Hint
                {
                    Key = hint.Key,
                    Value = hint.Value
                });

        return envelope.ToByteArray();
    }

    /// <summary>
    ///     Десериализует AuditMetaEnvelope из байт
    /// </summary>
    public AuditMetaEnvelope Deserialize(byte[] data)
    {
        return AuditMetaEnvelope.Parser.ParseFrom(data);
    }
}