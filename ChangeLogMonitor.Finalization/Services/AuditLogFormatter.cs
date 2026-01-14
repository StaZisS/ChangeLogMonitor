using Audit.V1;
using ChangeLogMonitor.Configuration.Services;
using ChangeLogMonitor.Finalization.Localization;
using ChangeLogMonitor.Finalization.Models;
using Microsoft.Extensions.Logging;

namespace ChangeLogMonitor.Finalization.Services;

public sealed class AuditLogFormatter : IAuditLogFormatter
{
    private readonly IAuditConfigurationService _configurationService;
    private readonly ILogger<AuditLogFormatter>? _logger;

    public AuditLogFormatter(IAuditConfigurationService configurationService, ILogger<AuditLogFormatter>? logger = null)
    {
        _configurationService = configurationService;
        _logger = logger;
    }

    public FormattedDiffResponse Format(AuditLogRecord record, string timezone)
    {
        var tz = GetTimeZoneInfo(timezone);
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(record.ChangeTimeUtc, tz);
        var formattedTime = localTime.ToString(AuditLogMessages.DateTimeFormat);

        AuditRecord? auditRecord = null;
        try
        {
            var bytes = Convert.FromBase64String(record.Payload);
            auditRecord = AuditRecord.Parser.ParseFrom(bytes);
        }
        catch
        {
            // Failed to parse protobuf, use fallback values
        }

        var entityTitle = GetEntityTitle(auditRecord, record.TableName, _configurationService, _logger);
        var userTitle = GetUserTitle(auditRecord, record.UserId, record.UserName);
        var operationName = GetOperationName(record.OperationCode);
        var summary = BuildSummary(record.OperationCode, entityTitle, formattedTime, userTitle);
        var details = BuildDetails(auditRecord);

        return new FormattedDiffResponse(
            record.LogId,
            localTime,
            record.TableName,
            operationName,
            record.EntityId,
            entityTitle,
            record.TxId,
            record.UserId,
            userTitle,
            summary,
            details);
    }

    private static TimeZoneInfo GetTimeZoneInfo(string timezone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch
        {
            // Fallback to default timezone
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(AuditLogMessages.DefaultTimezone);
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }
    }

    private static string GetEntityTitle(AuditRecord? auditRecord, string tableName, IAuditConfigurationService configurationService, ILogger? logger)
    {
        // 1. Сначала проверяем EntityTitle из protobuf (может быть установлен динамически)
        if (auditRecord != null && !string.IsNullOrWhiteSpace(auditRecord.EntityTitle))
        {
            logger?.LogDebug("Using EntityTitle from protobuf: {EntityTitle}", auditRecord.EntityTitle);
            return auditRecord.EntityTitle;
        }

        // 2. Затем проверяем displayName из конфигурации
        var entityPolicy = configurationService.GetEntityPolicy(tableName);
        logger?.LogDebug("GetEntityPolicy for '{TableName}': policy={PolicyFound}, displayName='{DisplayName}'",
            tableName, entityPolicy != null, entityPolicy?.DisplayName);

        if (entityPolicy != null && !string.IsNullOrWhiteSpace(entityPolicy.DisplayName))
            return entityPolicy.DisplayName;

        // 3. Fallback на техническое имя таблицы
        return tableName;
    }

    private static string GetUserTitle(AuditRecord? auditRecord, string userId, string userName)
    {
        // Prefer userName from record
        if (!string.IsNullOrWhiteSpace(userName))
            return userName;

        // Then try UserTitle from protobuf
        if (auditRecord != null && !string.IsNullOrWhiteSpace(auditRecord.UserTitle))
            return auditRecord.UserTitle;

        // Fallback to userId
        return string.IsNullOrWhiteSpace(userId)
            ? AuditLogMessages.Ru.UnknownUser
            : userId;
    }

    private static string GetOperationName(byte operationCode)
    {
        return operationCode switch
        {
            1 => AuditLogMessages.Ru.OperationCreate,
            2 => AuditLogMessages.Ru.OperationUpdate,
            3 => AuditLogMessages.Ru.OperationDelete,
            4 => AuditLogMessages.Ru.OperationSoftDelete,
            5 => AuditLogMessages.Ru.OperationBulkUpdate,
            6 => AuditLogMessages.Ru.OperationBulkDelete,
            _ => "UNKNOWN"
        };
    }

    private static string BuildSummary(byte operationCode, string entityTitle, string formattedTime, string userTitle)
    {
        return operationCode switch
        {
            1 => string.Format(AuditLogMessages.Ru.CreateSummary, entityTitle, formattedTime, userTitle),
            2 => string.Format(AuditLogMessages.Ru.UpdateSummary, entityTitle, formattedTime, userTitle),
            3 => string.Format(AuditLogMessages.Ru.DeleteSummary, entityTitle, formattedTime, userTitle),
            4 => string.Format(AuditLogMessages.Ru.SoftDeleteSummary, entityTitle, formattedTime, userTitle),
            5 => string.Format(AuditLogMessages.Ru.BulkUpdateSummary, entityTitle, formattedTime, userTitle),
            6 => string.Format(AuditLogMessages.Ru.BulkDeleteSummary, entityTitle, formattedTime, userTitle),
            _ => $"Операция над записью \"{entityTitle}\". ({formattedTime}, {userTitle})"
        };
    }

    private static IReadOnlyList<string> BuildDetails(AuditRecord? auditRecord)
    {
        var details = new List<string>();

        if (auditRecord == null)
            return details;

        // Process field changes
        foreach (var fieldChange in auditRecord.FieldChanges)
        {
            var detail = FormatFieldChange(fieldChange);
            if (!string.IsNullOrEmpty(detail))
                details.Add(detail);
        }

        // Process collection changes
        foreach (var collectionChange in auditRecord.CollectionChanges)
        {
            var collectionDetails = FormatCollectionChange(collectionChange);
            details.AddRange(collectionDetails);
        }

        return details;
    }

    private static string FormatFieldChange(FieldChange fieldChange)
    {
        var fieldName = !string.IsNullOrWhiteSpace(fieldChange.FieldTitle)
            ? fieldChange.FieldTitle
            : fieldChange.FieldName;

        // Handle sensitive modes
        if (fieldChange.SensitiveMode == SensitiveMode.Encrypted)
        {
            // Encrypted - cannot show values
            return string.Format(AuditLogMessages.Ru.FieldChangedMasked, fieldName);
        }

        if (fieldChange.SensitiveMode == SensitiveMode.FactOnly)
        {
            return string.Format(AuditLogMessages.Ru.FieldChangedFactOnly, fieldName);
        }

        if (fieldChange.SensitiveMode == SensitiveMode.Excluded)
        {
            return string.Empty;
        }

        var oldValue = GetDisplayValue(fieldChange.OldValue, fieldChange.ValueKind);
        var newValue = GetDisplayValue(fieldChange.NewValue, fieldChange.ValueKind);

        // For Hashed/Masked - show the transformed values (hash or masked)
        var valuePrefix = fieldChange.SensitiveMode switch
        {
            SensitiveMode.Hashed => "[SHA256] ",
            SensitiveMode.Masked => "",
            _ => ""
        };

        if (string.IsNullOrEmpty(oldValue) && !string.IsNullOrEmpty(newValue))
        {
            return string.Format(AuditLogMessages.Ru.FieldChangedFromEmpty, fieldName, valuePrefix + newValue);
        }

        if (!string.IsNullOrEmpty(oldValue) && string.IsNullOrEmpty(newValue))
        {
            return string.Format(AuditLogMessages.Ru.FieldChangedToEmpty, fieldName, valuePrefix + oldValue);
        }

        return string.Format(AuditLogMessages.Ru.FieldChanged, fieldName, valuePrefix + oldValue, valuePrefix + newValue);
    }

    private static string GetDisplayValue(FieldValue? fieldValue, ValueKind valueKind)
    {
        if (fieldValue == null)
            return string.Empty;

        return valueKind switch
        {
            ValueKind.Enum when !string.IsNullOrWhiteSpace(fieldValue.EnumTitle) => fieldValue.EnumTitle,
            ValueKind.Reference when !string.IsNullOrWhiteSpace(fieldValue.ReferenceTitle) => fieldValue.ReferenceTitle,
            _ => fieldValue.Normalized ?? string.Empty
        };
    }

    private static IEnumerable<string> FormatCollectionChange(CollectionChange collectionChange)
    {
        var collectionName = !string.IsNullOrWhiteSpace(collectionChange.FieldTitle)
            ? collectionChange.FieldTitle
            : collectionChange.FieldName;

        foreach (var item in collectionChange.Items)
        {
            var itemTitle = !string.IsNullOrWhiteSpace(item.ItemTitle)
                ? item.ItemTitle
                : item.ItemKey;

            var message = item.Kind switch
            {
                CollectionDeltaKind.Add => string.Format(AuditLogMessages.Ru.CollectionItemAdded, itemTitle),
                CollectionDeltaKind.Remove => string.Format(AuditLogMessages.Ru.CollectionItemRemoved, itemTitle),
                CollectionDeltaKind.Update => string.Format(AuditLogMessages.Ru.CollectionItemUpdated, itemTitle),
                _ => null
            };

            if (!string.IsNullOrEmpty(message))
            {
                yield return $"{collectionName}: {message}";
            }
        }
    }
}
