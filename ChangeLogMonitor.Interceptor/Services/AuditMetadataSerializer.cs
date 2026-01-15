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
        return Serialize(transactionId, null);
    }

    /// <summary>
    ///     Создает и сериализует AuditMetaEnvelope с enum снепшотами
    /// </summary>
    /// <param name="transactionId">ID транзакции</param>
    /// <param name="enumSnapshots">Снепшоты enum значений: имя типа → (код → лейбл)</param>
    /// <returns>Сериализованный protobuf как массив байт</returns>
    public byte[] Serialize(string transactionId, Dictionary<string, Dictionary<string, string>>? enumSnapshots)
    {
        return Serialize(transactionId, enumSnapshots, null, null);
    }

    /// <summary>
    ///     Создает и сериализует AuditMetaEnvelope со всеми метаданными
    /// </summary>
    /// <param name="transactionId">ID транзакции</param>
    /// <param name="enumSnapshots">Снепшоты enum значений</param>
    /// <param name="referenceSnapshots">Снепшоты ссылочных полей (FK)</param>
    /// <param name="collectionDeltas">Дельты коллекций</param>
    /// <returns>Сериализованный protobuf как массив байт</returns>
    public byte[] Serialize(
        string transactionId,
        Dictionary<string, Dictionary<string, string>>? enumSnapshots,
        List<ReferenceSnapshotData>? referenceSnapshots,
        List<CollectionDeltaData>? collectionDeltas)
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

        // Добавляем enum снепшоты (если есть)
        if (enumSnapshots != null && enumSnapshots.Count > 0)
            foreach (var (enumType, pairs) in enumSnapshots)
            {
                var snapshot = new EnumDictSnapshot
                {
                    EnumType = enumType
                };

                foreach (var (code, label) in pairs)
                    snapshot.Pairs.Add(new CodeLabel
                    {
                        Code = code,
                        Label = label
                    });

                envelope.EnumSnapshots.Add(snapshot);
            }

        // Добавляем снепшоты ссылочных полей (если есть)
        if (referenceSnapshots != null && referenceSnapshots.Count > 0)
            foreach (var refData in referenceSnapshots)
                envelope.ReferenceSnapshots.Add(new ReferenceSnapshot
                {
                    EntityType = refData.EntityType,
                    FieldName = refData.FieldName,
                    RelatedEntityType = refData.RelatedEntityType,
                    Key = refData.Key,
                    Title = refData.Title
                });

        // Добавляем дельты коллекций (если есть)
        if (collectionDeltas != null && collectionDeltas.Count > 0)
            foreach (var delta in collectionDeltas)
            {
                var deltaSnapshot = new CollectionDeltaSnapshot
                {
                    EntityType = delta.EntityType,
                    EntityId = delta.EntityId,
                    FieldName = delta.FieldName,
                    RelatedEntityType = delta.RelatedEntityType
                };

                foreach (var added in delta.Added)
                    deltaSnapshot.Added.Add(new CollectionItemSnapshot
                    {
                        Key = added.Key,
                        Title = added.Title
                    });

                foreach (var removed in delta.Removed)
                    deltaSnapshot.Removed.Add(new CollectionItemSnapshot
                    {
                        Key = removed.Key,
                        Title = removed.Title
                    });

                envelope.CollectionDeltas.Add(deltaSnapshot);
            }

        return envelope.ToByteArray();
    }

    /// <summary>
    ///     Создает и сериализует AuditMetaEnvelope для bulk/raw операции
    /// </summary>
    /// <param name="transactionId">ID транзакции</param>
    /// <param name="targetEntity">Целевая сущность/таблица</param>
    /// <param name="affectedCount">Количество затронутых строк</param>
    /// <param name="additionalHints">Дополнительные hints (опционально)</param>
    /// <param name="enumSnapshots">Снепшоты enum значений (опционально)</param>
    /// <returns>Сериализованный protobuf как массив байт</returns>
    public byte[] SerializeBulkOperation(
        string transactionId,
        string targetEntity,
        int affectedCount,
        Dictionary<string, string>? additionalHints = null,
        Dictionary<string, Dictionary<string, string>>? enumSnapshots = null)
    {
        var envelope = new AuditMetaEnvelope
        {
            TransactionId = transactionId,
            CreatedAtUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Actor = new UserInfo
            {
                UserId = _metadataProvider.GetUserId(),
                UserName = _metadataProvider.GetUserName()
            },
            Bulk = new BulkInfo
            {
                IsBulk = true,
                AffectedCount = (uint)affectedCount,
                Target = targetEntity
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

        // Добавляем подсказки от провайдера
        var providerHints = _metadataProvider.GetHints();
        if (providerHints != null && providerHints.Count > 0)
            foreach (var hint in providerHints)
                envelope.Hints.Add(new Hint
                {
                    Key = hint.Key,
                    Value = hint.Value
                });

        // Добавляем дополнительные hints (перезаписывают провайдерные)
        if (additionalHints != null && additionalHints.Count > 0)
            foreach (var hint in additionalHints)
                envelope.Hints.Add(new Hint
                {
                    Key = hint.Key,
                    Value = hint.Value
                });

        // Добавляем enum снепшоты (если есть)
        if (enumSnapshots != null && enumSnapshots.Count > 0)
            foreach (var (enumType, pairs) in enumSnapshots)
            {
                var snapshot = new EnumDictSnapshot
                {
                    EnumType = enumType
                };

                foreach (var (code, label) in pairs)
                    snapshot.Pairs.Add(new CodeLabel
                    {
                        Code = code,
                        Label = label
                    });

                envelope.EnumSnapshots.Add(snapshot);
            }

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