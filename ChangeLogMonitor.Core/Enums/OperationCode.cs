namespace ChangeLogMonitor.Core.Enums;

/// <summary>
///     Числовой код операции аудита, соответствующий значениям в БД и protobuf.
///     Значения должны оставаться стабильными, так как хранятся в базе данных.
/// </summary>
public enum OperationCode : byte
{
    Unknown = 0,
    Create = 1,
    Update = 2,
    Delete = 3,
    SoftDelete = 4,
    BulkUpdate = 5,
    BulkDelete = 6
}

/// <summary>
///     Вспомогательные методы для работы с OperationCode.
/// </summary>
public static class OperationCodeExtensions
{
    /// <summary>
    ///     Преобразует строку CDC операции (c/u/d/r) в OperationCode.
    /// </summary>
    public static OperationCode FromCdcOperation(string? rawOp)
    {
        var op = rawOp?.Trim();
        if (string.IsNullOrWhiteSpace(op))
            return OperationCode.Unknown;

        return char.ToLowerInvariant(op[0]) switch
        {
            'c' => OperationCode.Create,
            'r' => OperationCode.Create, // snapshot/read treated as create
            'u' => OperationCode.Update,
            'd' => OperationCode.Delete,
            _ => OperationCode.Unknown
        };
    }

    /// <summary>
    ///     Преобразует строковое название операции (CREATE/UPDATE/DELETE) в OperationCode.
    /// </summary>
    public static OperationCode FromDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return OperationCode.Unknown;

        return displayName.ToUpperInvariant() switch
        {
            OperationNames.Create => OperationCode.Create,
            OperationNames.Update => OperationCode.Update,
            OperationNames.Delete => OperationCode.Delete,
            OperationNames.SoftDelete => OperationCode.SoftDelete,
            OperationNames.BulkUpdate => OperationCode.BulkUpdate,
            OperationNames.BulkDelete => OperationCode.BulkDelete,
            _ => OperationCode.Unknown
        };
    }

    /// <summary>
    ///     Возвращает строковое название операции.
    /// </summary>
    public static string ToDisplayName(this OperationCode code)
    {
        return code switch
        {
            OperationCode.Create => OperationNames.Create,
            OperationCode.Update => OperationNames.Update,
            OperationCode.Delete => OperationNames.Delete,
            OperationCode.SoftDelete => OperationNames.SoftDelete,
            OperationCode.BulkUpdate => OperationNames.BulkUpdate,
            OperationCode.BulkDelete => OperationNames.BulkDelete,
            _ => OperationNames.Unknown
        };
    }
}

/// <summary>
///     Строковые константы названий операций.
/// </summary>
public static class OperationNames
{
    public const string Unknown = "UNKNOWN";
    public const string Create = "CREATE";
    public const string Update = "UPDATE";
    public const string Delete = "DELETE";
    public const string SoftDelete = "SOFT_DELETE";
    public const string BulkUpdate = "BULK_UPDATE";
    public const string BulkDelete = "BULK_DELETE";
}
