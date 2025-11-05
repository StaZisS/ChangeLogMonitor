using ChangeLogMonitor.Configuration.Models;
using FluentValidation;

namespace ChangeLogMonitor.Configuration.Validators;

/// <summary>
/// Валидатор для главной политики аудита
/// </summary>
public class YamlAuditPolicyValidator : AbstractValidator<YamlAuditPolicy>
{
    private static readonly string[] ValidModes = { "whitelist", "blacklist" };
    private static readonly string[] ValidCreateBehaviors = { "eventOnly", "allFields", "nonDefaultFields" };
    private static readonly string[] ValidUpdateBehaviors = { "delta", "fullSnapshot" };
    private static readonly string[] ValidDeleteBehaviors = { "eventOnly", "allFields" };

    public YamlAuditPolicyValidator()
    {
        RuleFor(x => x.Version)
            .NotEmpty()
            .WithMessage("Version is required");

        RuleFor(x => x.Mode)
            .Must(mode =>
            {
                if (string.IsNullOrWhiteSpace(mode)) return true;
                var normalized = mode.Trim();
                return ValidModes.Contains(normalized, StringComparer.Ordinal);
            })
            .WithMessage($"Mode must be one of: {string.Join(", ", ValidModes)}");

        RuleFor(x => x.OnCreate)
            .Must(behavior =>
            {
                if (string.IsNullOrWhiteSpace(behavior)) return true;
                var normalized = behavior.Trim();
                return ValidCreateBehaviors.Contains(normalized, StringComparer.Ordinal);
            })
            .WithMessage($"OnCreate must be one of: {string.Join(", ", ValidCreateBehaviors)}");

        RuleFor(x => x.OnUpdate)
            .Must(behavior =>
            {
                if (string.IsNullOrWhiteSpace(behavior)) return true;
                var normalized = behavior.Trim();
                return ValidUpdateBehaviors.Contains(normalized, StringComparer.Ordinal);
            })
            .WithMessage($"OnUpdate must be one of: {string.Join(", ", ValidUpdateBehaviors)}");

        RuleFor(x => x.OnDelete)
            .Must(behavior =>
            {
                if (string.IsNullOrWhiteSpace(behavior)) return true;
                var normalized = behavior.Trim();
                return ValidDeleteBehaviors.Contains(normalized, StringComparer.Ordinal);
            })
            .WithMessage($"OnDelete must be one of: {string.Join(", ", ValidDeleteBehaviors)}");

        When(x => x.Entities != null, () =>
        {
            RuleForEach(x => x.Entities!.Values)
                .SetValidator(new YamlEntityPolicyValidator());
        });

        When(x => x.ReferenceDefaults != null, () =>
        {
            RuleFor(x => x.ReferenceDefaults!)
                .SetValidator(new YamlReferenceDefaultsValidator());
        });

        When(x => x.CollectionDefaults != null, () =>
        {
            RuleFor(x => x.CollectionDefaults!)
                .SetValidator(new YamlCollectionDefaultsValidator());
        });
    }
}

/// <summary>
/// Валидатор для политики сущности
/// </summary>
public class YamlEntityPolicyValidator : AbstractValidator<YamlEntityPolicy>
{
    private static readonly string[] ValidCreateBehaviors = { "eventOnly", "allFields", "nonDefaultFields" };
    private static readonly string[] ValidUpdateBehaviors = { "delta", "fullSnapshot" };
    private static readonly string[] ValidDeleteBehaviors = { "eventOnly", "allFields" };

    public YamlEntityPolicyValidator()
    {
        RuleFor(x => x.OnCreate)
            .Must(behavior =>
            {
                if (string.IsNullOrWhiteSpace(behavior)) return true;
                var normalized = behavior.Trim();
                return ValidCreateBehaviors.Contains(normalized, StringComparer.Ordinal);
            })
            .WithMessage($"OnCreate must be one of: {string.Join(", ", ValidCreateBehaviors)}");

        RuleFor(x => x.OnUpdate)
            .Must(behavior =>
            {
                if (string.IsNullOrWhiteSpace(behavior)) return true;
                var normalized = behavior.Trim();
                return ValidUpdateBehaviors.Contains(normalized, StringComparer.Ordinal);
            })
            .WithMessage($"OnUpdate must be one of: {string.Join(", ", ValidUpdateBehaviors)}");

        RuleFor(x => x.OnDelete)
            .Must(behavior =>
            {
                if (string.IsNullOrWhiteSpace(behavior)) return true;
                var normalized = behavior.Trim();
                return ValidDeleteBehaviors.Contains(normalized, StringComparer.Ordinal);
            })
            .WithMessage($"OnDelete must be one of: {string.Join(", ", ValidDeleteBehaviors)}");

        When(x => x.References != null, () =>
        {
            RuleForEach(x => x.References!.Values)
                .SetValidator(new YamlReferencePolicyValidator());
        });

        When(x => x.Collections != null, () =>
        {
            RuleForEach(x => x.Collections!.Values)
                .SetValidator(new YamlCollectionPolicyValidator());
        });
    }
}

/// <summary>
/// Валидатор для политики полей
/// </summary>
public class YamlFieldPolicyValidator : AbstractValidator<YamlFieldPolicy>
{
    private static readonly string[] ValidActions = { "include", "exclude", "mask", "hash", "encrypt" };
    private static readonly string[] ValidFormats = { "date", "datetime", "money", "number", "boolean", "string" };

    public YamlFieldPolicyValidator()
    {
        RuleFor(x => x.Action)
            .Must(action => string.IsNullOrWhiteSpace(action) || ValidActions.Contains(action.Trim().ToLowerInvariant()))
            .WithMessage($"Action must be one of: {string.Join(", ", ValidActions)}");

        When(x => x.View != null && !string.IsNullOrWhiteSpace(x.View.Format), () =>
        {
            RuleFor(x => x.View!.Format)
                .Must(format => ValidFormats.Contains(format!.Trim().ToLowerInvariant()))
                .WithMessage($"View.Format must be one of: {string.Join(", ", ValidFormats)}");
        });

        When(x => x.Action == "mask" && x.Mask == null, () =>
        {
            RuleFor(x => x.Mask)
                .NotNull()
                .WithMessage("Mask settings are required when action is 'mask'");
        });

        When(x => x.Action == "hash" && x.Hash == null, () =>
        {
            RuleFor(x => x.Hash)
                .NotNull()
                .WithMessage("Hash settings are required when action is 'hash'");
        });

        When(x => x.Action == "encrypt" && x.Encrypt == null, () =>
        {
            RuleFor(x => x.Encrypt)
                .NotNull()
                .WithMessage("Encrypt settings are required when action is 'encrypt'");
        });
    }
}

/// <summary>
/// Валидатор для политики ссылок
/// </summary>
public class YamlReferencePolicyValidator : AbstractValidator<YamlReferencePolicy>
{
    public YamlReferencePolicyValidator()
    {
        RuleFor(x => x.ViewTemplate)
            .Must(template => template == null || (template.Contains("{name}") || template.Contains("{key}")))
            .WithMessage("ViewTemplate must contain {name} or {key} placeholder");

        When(x => x.NameResolve != null, () =>
        {
            RuleFor(x => x.NameResolve!.Stage)
                .Must(stage => string.IsNullOrWhiteSpace(stage) || new[] { "raw", "normalization" }.Contains(stage.Trim().ToLowerInvariant()))
                .WithMessage("NameResolve.Stage must be 'raw' or 'normalization'");
        });
    }
}

/// <summary>
/// Валидатор для политики коллекций
/// </summary>
public class YamlCollectionPolicyValidator : AbstractValidator<YamlCollectionPolicy>
{
    public YamlCollectionPolicyValidator()
    {
        When(x => x.Limits != null, () =>
        {
            RuleFor(x => x.Limits!.AddedMax)
                .GreaterThan(0)
                .When(x => x.Limits!.AddedMax.HasValue)
                .WithMessage("Limits.AddedMax must be greater than 0");

            RuleFor(x => x.Limits!.RemovedMax)
                .GreaterThan(0)
                .When(x => x.Limits!.RemovedMax.HasValue)
                .WithMessage("Limits.RemovedMax must be greater than 0");
        });

        When(x => x.CountOnlyWhenLarge != null, () =>
        {
            RuleFor(x => x.CountOnlyWhenLarge!.Threshold)
                .GreaterThan(0)
                .When(x => x.CountOnlyWhenLarge!.Threshold.HasValue)
                .WithMessage("CountOnlyWhenLarge.Threshold must be greater than 0");
        });
    }
}

public class YamlReferenceDefaultsValidator : AbstractValidator<YamlReferenceDefaults>
{
    public YamlReferenceDefaultsValidator()
    {
        RuleFor(x => x.ViewTemplate)
            .Must(template => template == null || (template.Contains("{name}") || template.Contains("{key}")))
            .WithMessage("ViewTemplate must contain {name} or {key} placeholder");
    }
}

public class YamlCollectionDefaultsValidator : AbstractValidator<YamlCollectionDefaults>
{
    public YamlCollectionDefaultsValidator()
    {
        When(x => x.Limits != null, () =>
        {
            RuleFor(x => x.Limits!.AddedMax)
                .GreaterThan(0)
                .When(x => x.Limits!.AddedMax.HasValue);

            RuleFor(x => x.Limits!.RemovedMax)
                .GreaterThan(0)
                .When(x => x.Limits!.RemovedMax.HasValue);
        });
    }
}
