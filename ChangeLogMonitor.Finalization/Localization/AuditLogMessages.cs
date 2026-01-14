namespace ChangeLogMonitor.Finalization.Localization;

public static class AuditLogMessages
{
    public const string DefaultTimezone = "Asia/Almaty";
    public const string DateTimeFormat = "dd.MM.yyyy HH:mm:ss";

    public static class Ru
    {
        public const string CreateSummary = "Создана запись \"{0}\". ({1}, {2})";
        public const string UpdateSummary = "Изменена запись \"{0}\". ({1}, {2})";
        public const string DeleteSummary = "Удалена запись \"{0}\". ({1}, {2})";
        public const string SoftDeleteSummary = "Запись \"{0}\" помечена как удалённая. ({1}, {2})";
        public const string BulkUpdateSummary = "Массовое обновление записей \"{0}\". ({1}, {2})";
        public const string BulkDeleteSummary = "Массовое удаление записей \"{0}\". ({1}, {2})";

        public const string FieldChanged = "Значение поля \"{0}\" было изменено с \"{1}\" на \"{2}\".";
        public const string FieldChangedFromEmpty = "Значение поля \"{0}\" установлено в \"{1}\".";
        public const string FieldChangedToEmpty = "Значение поля \"{0}\" было очищено (было \"{1}\").";
        public const string FieldChangedMasked = "Значение поля \"{0}\" было изменено (данные скрыты).";
        public const string FieldChangedFactOnly = "Поле \"{0}\" было изменено.";

        public const string CollectionItemAdded = "Добавлен элемент \"{0}\".";
        public const string CollectionItemRemoved = "Удалён элемент \"{0}\".";
        public const string CollectionItemUpdated = "Изменён элемент \"{0}\".";

        public const string OperationCreate = "CREATE";
        public const string OperationUpdate = "UPDATE";
        public const string OperationDelete = "DELETE";
        public const string OperationSoftDelete = "SOFT_DELETE";
        public const string OperationBulkUpdate = "BULK_UPDATE";
        public const string OperationBulkDelete = "BULK_DELETE";

        public const string UnknownUser = "Неизвестный пользователь";
        public const string UnknownEntity = "Запись";
    }
}
