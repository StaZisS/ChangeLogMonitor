using ChangeLogMonitor.Configuration.Models;
using ChangeLogMonitor.Core.Enums;
using ChangeLogMonitor.Core.Models.Policy;

namespace ChangeLogMonitor.Configuration.Mappers;

public class AuditPolicyMapper
{
    public AuditPolicy MapToDomain(YamlAuditPolicy yaml)
    {
        var policy = new AuditPolicy
        {
            Version = yaml.Version ?? "1.0",
            Mode = ParseAuditMode(yaml.Mode),
            OnCreate = ParseCreateBehavior(yaml.OnCreate),
            OnUpdate = ParseUpdateBehavior(yaml.OnUpdate),
            OnDelete = ParseDeleteBehavior(yaml.OnDelete),
            GlobalFieldExclusions = yaml.GlobalFieldExclusions ?? new List<string>(),
            DefaultCulture = yaml.DefaultCulture ?? "ru-RU",
            DefaultTimeZone = yaml.DefaultTimeZone ?? "Asia/Almaty"
        };

        if (yaml.MethodPresets != null)
        {
            policy.MaskPresets = MapMaskPresets(yaml.MethodPresets.Mask);
            policy.HashPresets = MapHashPresets(yaml.MethodPresets.Hash);
            policy.EncryptPresets = MapEncryptPresets(yaml.MethodPresets.Encrypt);
        }

        policy.ReferencePresets = MapReferencePresets(yaml.ReferencePresets);
        policy.CollectionPresets = MapCollectionPresets(yaml.CollectionPresets);

        if (yaml.ReferenceDefaults != null) policy.ReferenceDefaults = MapReferenceDefaults(yaml.ReferenceDefaults);

        if (yaml.CollectionDefaults != null) policy.CollectionDefaults = MapCollectionDefaults(yaml.CollectionDefaults);

        if (yaml.Entities != null)
            foreach (var (entityName, yamlEntity) in yaml.Entities)
                policy.Entities[entityName] = MapEntityPolicy(yamlEntity, policy);

        if (yaml.AccessControl != null)
            policy.AccessControl = MapAccessControl(yaml.AccessControl);

        return policy;
    }

    private EntityPolicy MapEntityPolicy(YamlEntityPolicy yaml, AuditPolicy parentPolicy)
    {
        var entity = new EntityPolicy
        {
            Enabled = yaml.Enabled ?? true,
            DisplayName = yaml.DisplayName,
            OnCreate = ParseCreateBehavior(yaml.OnCreate),
            OnUpdate = ParseUpdateBehavior(yaml.OnUpdate),
            OnDelete = ParseDeleteBehavior(yaml.OnDelete)
        };

        if (yaml.Fields != null)
            foreach (var (fieldName, fieldValue) in yaml.Fields)
                if (fieldValue is YamlFieldPolicy yamlField)
                    entity.Fields[fieldName] = MapFieldPolicy(yamlField, parentPolicy);

        if (yaml.References != null)
            foreach (var (refName, yamlRef) in yaml.References)
                entity.References[refName] = MapReferencePolicy(yamlRef, parentPolicy);

        if (yaml.Collections != null)
            foreach (var (collName, yamlColl) in yaml.Collections)
                entity.Collections[collName] = MapCollectionPolicy(yamlColl, parentPolicy);

        if (yaml.Access != null)
            entity.Access = MapEntityAccess(yaml.Access);

        return entity;
    }

    private FieldPolicy MapFieldPolicy(YamlFieldPolicy yaml, AuditPolicy parentPolicy)
    {
        var policy = new FieldPolicy
        {
            Action = ParseFieldAction(yaml.Action)
        };

        if (yaml.View != null)
            policy.View = new ViewSettings
            {
                Format = ParseFieldType(yaml.View.Format),
                Pattern = yaml.View.Pattern,
                Culture = yaml.View.Culture,
                RefName = yaml.View.RefName ?? true
            };

        if (yaml.Mask != null) policy.Mask = MapMaskSettings(yaml.Mask, parentPolicy);

        if (yaml.Hash != null) policy.Hash = MapHashSettings(yaml.Hash, parentPolicy);

        if (yaml.Encrypt != null) policy.Encrypt = MapEncryptSettings(yaml.Encrypt, parentPolicy);

        return policy;
    }

    private MaskSettings MapMaskSettings(YamlMaskSettings yaml, AuditPolicy parentPolicy)
    {
        var settings = new MaskSettings();

        if (!string.IsNullOrEmpty(yaml.Preset) && parentPolicy.MaskPresets.TryGetValue(yaml.Preset, out var preset))
        {
            settings.MaskChar = preset.MaskChar;
            settings.KeepLeft = preset.KeepLeft;
            settings.KeepRight = preset.KeepRight;
            settings.PreserveDomain = preset.PreserveDomain;
            settings.PreserveFormat = preset.PreserveFormat;
        }

        if (!string.IsNullOrEmpty(yaml.Char)) settings.MaskChar = yaml.Char[0];
        if (yaml.KeepLeft.HasValue) settings.KeepLeft = yaml.KeepLeft.Value;
        if (yaml.KeepRight.HasValue) settings.KeepRight = yaml.KeepRight.Value;
        if (yaml.PreserveDomain.HasValue) settings.PreserveDomain = yaml.PreserveDomain.Value;
        if (yaml.PreserveFormat.HasValue) settings.PreserveFormat = yaml.PreserveFormat.Value;
        if (!string.IsNullOrEmpty(yaml.Regex)) settings.Regex = yaml.Regex;
        if (!string.IsNullOrEmpty(yaml.Replace)) settings.Replace = yaml.Replace;

        return settings;
    }

    private HashSettings MapHashSettings(YamlHashSettings yaml, AuditPolicy parentPolicy)
    {
        var settings = new HashSettings();

        if (!string.IsNullOrEmpty(yaml.Preset) && parentPolicy.HashPresets.TryGetValue(yaml.Preset, out var preset))
        {
            settings.Algo = preset.Algo;
            settings.Salt = preset.Salt != null
                ? new SaltSettings { Strategy = preset.Salt.Strategy, Ref = preset.Salt.Ref }
                : null;
            settings.PepperRef = preset.PepperRef;
            settings.Encoding = preset.Encoding;
            settings.StoreRaw = preset.StoreRaw;
            settings.StoreHash = preset.StoreHash;
            settings.EqualityToken = preset.EqualityToken;
        }

        if (!string.IsNullOrEmpty(yaml.Algo)) settings.Algo = yaml.Algo;
        if (yaml.Salt != null)
            settings.Salt = new SaltSettings { Strategy = yaml.Salt.Strategy ?? "per-record", Ref = yaml.Salt.Ref };
        if (!string.IsNullOrEmpty(yaml.PepperRef)) settings.PepperRef = yaml.PepperRef;
        if (!string.IsNullOrEmpty(yaml.Encoding)) settings.Encoding = yaml.Encoding;
        if (yaml.StoreRaw.HasValue) settings.StoreRaw = yaml.StoreRaw.Value;
        if (yaml.StoreHash.HasValue) settings.StoreHash = yaml.StoreHash.Value;
        if (yaml.EqualityToken.HasValue) settings.EqualityToken = yaml.EqualityToken.Value;

        return settings;
    }

    private EncryptSettings MapEncryptSettings(YamlEncryptSettings yaml, AuditPolicy parentPolicy)
    {
        var settings = new EncryptSettings();

        if (!string.IsNullOrEmpty(yaml.Preset) && parentPolicy.EncryptPresets.TryGetValue(yaml.Preset, out var preset))
        {
            settings.Algo = preset.Algo;
            settings.KeyRef = preset.KeyRef;
            settings.Aad = preset.Aad;
            settings.Iv = new IvSettings
            { Strategy = preset.Iv.Strategy, Store = preset.Iv.Store, Length = preset.Iv.Length };
            settings.Encoding = preset.Encoding;
            settings.StoreRaw = preset.StoreRaw;
        }

        if (!string.IsNullOrEmpty(yaml.Algo)) settings.Algo = yaml.Algo;
        if (!string.IsNullOrEmpty(yaml.KeyRef)) settings.KeyRef = yaml.KeyRef;
        if (yaml.Aad != null && yaml.Aad.Any()) settings.Aad = yaml.Aad;
        if (yaml.Iv != null)
            settings.Iv = new IvSettings
            {
                Strategy = yaml.Iv.Strategy ?? "random",
                Store = yaml.Iv.Store ?? true,
                Length = yaml.Iv.Length ?? 12
            };
        if (!string.IsNullOrEmpty(yaml.Encoding)) settings.Encoding = yaml.Encoding;
        if (yaml.StoreRaw.HasValue) settings.StoreRaw = yaml.StoreRaw.Value;
        if (yaml.Rotate != null)
            settings.Rotate = new RotateSettings
            {
                Enabled = yaml.Rotate.Enabled ?? false,
                Policy = yaml.Rotate.Policy ?? "by-key-alias"
            };

        return settings;
    }

    private ReferencePolicy MapReferencePolicy(YamlReferencePolicy yaml, AuditPolicy parentPolicy)
    {
        var policy = new ReferencePolicy();

        if (!string.IsNullOrEmpty(yaml.Preset) &&
            parentPolicy.ReferencePresets.TryGetValue(yaml.Preset, out var preset))
        {
            policy.ShowKey = preset.ShowKey;
            policy.ShowName = preset.ShowName;
            policy.ViewTemplate = preset.ViewTemplate;
            policy.NameMaskPreset = preset.NameMaskPreset;
        }

        if (yaml.ShowKey.HasValue) policy.ShowKey = yaml.ShowKey.Value;
        if (yaml.ShowName.HasValue) policy.ShowName = yaml.ShowName.Value;
        if (!string.IsNullOrEmpty(yaml.ViewTemplate)) policy.ViewTemplate = yaml.ViewTemplate;
        if (!string.IsNullOrEmpty(yaml.NameSelector)) policy.NameSelector = yaml.NameSelector;
        if (yaml.NameResolve != null)
            policy.NameResolve = new NameResolveSettings
            {
                Stage = yaml.NameResolve.Stage ?? "normalization",
                Fallback = yaml.NameResolve.Fallback ?? "{key}",
                MaxLen = yaml.NameResolve.MaxLen ?? 256
            };
        if (!string.IsNullOrEmpty(yaml.NameMaskPreset)) policy.NameMaskPreset = yaml.NameMaskPreset;
        if (yaml.Key != null)
            policy.Key = new KeySensitivitySettings
            {
                TreatAsSensitive = yaml.Key.TreatAsSensitive ?? false,
                MaskPreset = yaml.Key.MaskPreset,
                HashPreset = yaml.Key.HashPreset
            };
        if (!string.IsNullOrEmpty(yaml.NullTransitions)) policy.NullTransitions = yaml.NullTransitions;
        if (!string.IsNullOrEmpty(yaml.ChangedAs)) policy.ChangedAs = yaml.ChangedAs;

        return policy;
    }

    private CollectionPolicy MapCollectionPolicy(YamlCollectionPolicy yaml, AuditPolicy parentPolicy)
    {
        var policy = new CollectionPolicy();

        if (!string.IsNullOrEmpty(yaml.Preset) &&
            parentPolicy.CollectionPresets.TryGetValue(yaml.Preset, out var preset))
        {
            policy.LogDeltas = preset.LogDeltas;
            policy.ShowKeys = preset.ShowKeys;
            policy.ShowNames = preset.ShowNames;
            policy.ItemViewTemplate = preset.ItemViewTemplate;
            policy.DeltaView.CollapseToCounters = preset.CollapseToCounters;
        }

        if (yaml.LogDeltas.HasValue) policy.LogDeltas = yaml.LogDeltas.Value;
        if (yaml.ShowKeys.HasValue) policy.ShowKeys = yaml.ShowKeys.Value;
        if (yaml.ShowNames.HasValue) policy.ShowNames = yaml.ShowNames.Value;
        if (!string.IsNullOrEmpty(yaml.ItemKeySelector)) policy.ItemKeySelector = yaml.ItemKeySelector;
        if (!string.IsNullOrEmpty(yaml.ItemNameSelector)) policy.ItemNameSelector = yaml.ItemNameSelector;
        if (!string.IsNullOrEmpty(yaml.ItemViewTemplate)) policy.ItemViewTemplate = yaml.ItemViewTemplate;

        if (yaml.DeltaView != null)
            policy.DeltaView = new DeltaViewSettings
            {
                AddedPrefix = yaml.DeltaView.AddedPrefix ?? "Добавлено:",
                RemovedPrefix = yaml.DeltaView.RemovedPrefix ?? "Удалено:",
                Joiner = yaml.DeltaView.Joiner ?? ", ",
                CollapseToCounters = yaml.DeltaView.CollapseToCounters ?? false
            };

        if (yaml.Limits != null)
            policy.Limits = new CollectionLimits
            {
                AddedMax = yaml.Limits.AddedMax ?? 200,
                RemovedMax = yaml.Limits.RemovedMax ?? 200
            };

        if (yaml.CountOnlyWhenLarge != null)
            policy.CountOnlyWhenLarge = new CountOnlySettings
            {
                Enabled = yaml.CountOnlyWhenLarge.Enabled ?? true,
                Threshold = yaml.CountOnlyWhenLarge.Threshold ?? 2000
            };

        if (yaml.TrackReordering.HasValue) policy.TrackReordering = yaml.TrackReordering.Value;
        if (!string.IsNullOrEmpty(yaml.IncludeOnCreate)) policy.IncludeOnCreate = yaml.IncludeOnCreate;
        if (!string.IsNullOrEmpty(yaml.IncludeOnDelete)) policy.IncludeOnDelete = yaml.IncludeOnDelete;
        if (yaml.MembershipKeysSensitive.HasValue) policy.MembershipKeysSensitive = yaml.MembershipKeysSensitive.Value;

        if (yaml.M2M != null)
            policy.M2M = new M2MSettings
            {
                JoinEntity = yaml.M2M.JoinEntity,
                JoinFields = yaml.M2M.JoinFields ?? new List<string>(),
                TreatJoinCudAsMembership = yaml.M2M.TreatJoinCudAsMembership ?? true
            };

        return policy;
    }

    private Dictionary<string, MaskPreset> MapMaskPresets(Dictionary<string, YamlMaskPreset>? yaml)
    {
        if (yaml == null) return new Dictionary<string, MaskPreset>();

        return yaml.ToDictionary(
            kvp => kvp.Key,
            kvp => new MaskPreset
            {
                MaskChar = !string.IsNullOrEmpty(kvp.Value.Char) ? kvp.Value.Char[0] : '*',
                KeepLeft = kvp.Value.KeepLeft ?? 0,
                KeepRight = kvp.Value.KeepRight ?? 0,
                PreserveDomain = kvp.Value.PreserveDomain ?? false,
                PreserveFormat = kvp.Value.PreserveFormat ?? true
            }
        );
    }

    private Dictionary<string, HashPreset> MapHashPresets(Dictionary<string, YamlHashPreset>? yaml)
    {
        if (yaml == null) return new Dictionary<string, HashPreset>();

        return yaml.ToDictionary(
            kvp => kvp.Key,
            kvp => new HashPreset
            {
                Algo = kvp.Value.Algo ?? "SHA-256",
                Salt = kvp.Value.Salt != null
                    ? new SaltSettings { Strategy = kvp.Value.Salt.Strategy ?? "per-record", Ref = kvp.Value.Salt.Ref }
                    : null,
                PepperRef = kvp.Value.PepperRef,
                Encoding = kvp.Value.Encoding ?? "base64",
                StoreRaw = kvp.Value.StoreRaw ?? false,
                StoreHash = kvp.Value.StoreHash ?? true,
                EqualityToken = kvp.Value.EqualityToken ?? false
            }
        );
    }

    private Dictionary<string, EncryptPreset> MapEncryptPresets(Dictionary<string, YamlEncryptPreset>? yaml)
    {
        if (yaml == null) return new Dictionary<string, EncryptPreset>();

        return yaml.ToDictionary(
            kvp => kvp.Key,
            kvp => new EncryptPreset
            {
                Algo = kvp.Value.Algo ?? "AES-256-GCM",
                KeyRef = kvp.Value.KeyRef,
                Aad = kvp.Value.Aad ?? new List<string>(),
                Iv = new IvSettings
                {
                    Strategy = kvp.Value.Iv?.Strategy ?? "random",
                    Store = kvp.Value.Iv?.Store ?? true,
                    Length = kvp.Value.Iv?.Length ?? 12
                },
                Encoding = kvp.Value.Encoding ?? "base64",
                StoreRaw = kvp.Value.StoreRaw ?? false
            }
        );
    }

    private Dictionary<string, ReferencePreset> MapReferencePresets(Dictionary<string, YamlReferencePreset>? yaml)
    {
        if (yaml == null) return new Dictionary<string, ReferencePreset>();

        return yaml.ToDictionary(
            kvp => kvp.Key,
            kvp => new ReferencePreset
            {
                ShowKey = kvp.Value.ShowKey ?? true,
                ShowName = kvp.Value.ShowName ?? true,
                ViewTemplate = kvp.Value.ViewTemplate ?? "{name} (ID={key})",
                NameMaskPreset = kvp.Value.NameMaskPreset
            }
        );
    }

    private Dictionary<string, CollectionPreset> MapCollectionPresets(Dictionary<string, YamlCollectionPreset>? yaml)
    {
        if (yaml == null) return new Dictionary<string, CollectionPreset>();

        return yaml.ToDictionary(
            kvp => kvp.Key,
            kvp => new CollectionPreset
            {
                LogDeltas = kvp.Value.LogDeltas ?? true,
                ShowKeys = kvp.Value.ShowKeys ?? false,
                ShowNames = kvp.Value.ShowNames ?? true,
                ItemViewTemplate = kvp.Value.ItemViewTemplate ?? "{name} (ID={key})",
                CollapseToCounters = kvp.Value.CollapseToCounters ?? false
            }
        );
    }

    private ReferenceDefaults MapReferenceDefaults(YamlReferenceDefaults yaml)
    {
        return new ReferenceDefaults
        {
            ShowKey = yaml.ShowKey ?? true,
            ShowName = yaml.ShowName ?? true,
            ViewTemplate = yaml.ViewTemplate ?? "{name} (ID={key})",
            NameResolve = new NameResolveSettings
            {
                Stage = yaml.NameResolve?.Stage ?? "normalization",
                Fallback = yaml.NameResolve?.Fallback ?? "{key}",
                MaxLen = yaml.NameResolve?.MaxLen ?? 256
            }
        };
    }

    private CollectionDefaults MapCollectionDefaults(YamlCollectionDefaults yaml)
    {
        return new CollectionDefaults
        {
            LogDeltas = yaml.LogDeltas ?? true,
            ShowKeys = yaml.ShowKeys ?? true,
            ShowNames = yaml.ShowNames ?? true,
            ItemViewTemplate = yaml.ItemViewTemplate ?? "{name} (ID={key})",
            Limits = new CollectionLimits
            {
                AddedMax = yaml.Limits?.AddedMax ?? 200,
                RemovedMax = yaml.Limits?.RemovedMax ?? 200
            }
        };
    }

    private AuditMode ParseAuditMode(string? value)
    {
        return value?.ToLower() switch
        {
            "whitelist" => AuditMode.Whitelist,
            "blacklist" => AuditMode.Blacklist,
            _ => AuditMode.Whitelist
        };
    }

    private CreateBehavior ParseCreateBehavior(string? value)
    {
        return value?.ToLower() switch
        {
            "eventonly" => CreateBehavior.EventOnly,
            "allfields" => CreateBehavior.AllFields,
            "nondefaultfields" => CreateBehavior.NonDefaultFields,
            _ => CreateBehavior.EventOnly
        };
    }

    private UpdateBehavior ParseUpdateBehavior(string? value)
    {
        return value?.ToLower() switch
        {
            "delta" => UpdateBehavior.Delta,
            "fullsnapshot" => UpdateBehavior.FullSnapshot,
            _ => UpdateBehavior.Delta
        };
    }

    private DeleteBehavior ParseDeleteBehavior(string? value)
    {
        return value?.ToLower() switch
        {
            "eventonly" => DeleteBehavior.EventOnly,
            "allfields" => DeleteBehavior.AllFields,
            _ => DeleteBehavior.EventOnly
        };
    }

    private FieldAction ParseFieldAction(string? value)
    {
        return value?.ToLower() switch
        {
            "include" => FieldAction.Include,
            "exclude" => FieldAction.Exclude,
            "mask" => FieldAction.Mask,
            "hash" => FieldAction.Hash,
            "encrypt" => FieldAction.Encrypt,
            _ => FieldAction.Include
        };
    }

    private FieldType? ParseFieldType(string? value)
    {
        return value?.ToLower() switch
        {
            "string" => FieldType.String,
            "number" => FieldType.Number,
            "boolean" => FieldType.Boolean,
            "datetime" => FieldType.DateTime,
            "date" => FieldType.Date,
            "money" => FieldType.Money,
            "enum" => FieldType.Enum,
            _ => null
        };
    }

    private UnauthorizedBehavior ParseUnauthorizedBehavior(string? value)
    {
        return value?.ToLower() switch
        {
            "filter" => UnauthorizedBehavior.Filter,
            "deny" => UnauthorizedBehavior.Deny,
            _ => UnauthorizedBehavior.Deny
        };
    }

    private AccessControl MapAccessControl(YamlAccessControl yaml)
    {
        return new AccessControl
        {
            Enabled = yaml.Enabled,
            UnauthorizedBehavior = ParseUnauthorizedBehavior(yaml.UnauthorizedBehavior),
            Roles = yaml.Roles?.ToDictionary(
                kvp => kvp.Key,
                kvp => new RoleDefinition
                {
                    Description = kvp.Value.Description,
                    AllowAll = kvp.Value.AllowAll
                }) ?? new Dictionary<string, RoleDefinition>(),
            Users = yaml.Users?.ToDictionary(
                kvp => kvp.Key,
                kvp => new UserRoleMapping
                {
                    Roles = kvp.Value.Roles ?? new List<string>()
                }) ?? new Dictionary<string, UserRoleMapping>(),
            DefaultRoles = yaml.DefaultRoles ?? new List<string>(),
            AllowAnonymous = yaml.AllowAnonymous,
            AnonymousRoles = yaml.AnonymousRoles ?? new List<string>()
        };
    }

    private EntityAccess MapEntityAccess(YamlEntityAccess yaml)
    {
        return new EntityAccess
        {
            AllowedRoles = yaml.AllowedRoles ?? new List<string>()
        };
    }
}