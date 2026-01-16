using ChangeLogMonitor.Configuration.Models;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ChangeLogMonitor.Configuration.Providers;

public class YamlAuditPolicyProvider : IAuditPolicyProvider
{
    private readonly string _configFilePath;
    private readonly IDeserializer _deserializer;

    public YamlAuditPolicyProvider(string configFilePath)
    {
        _configFilePath = configFilePath ?? throw new ArgumentNullException(nameof(configFilePath));

        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }
    
    public YamlAuditPolicyRoot Load()
    {
        if (!File.Exists(_configFilePath))
            throw new FileNotFoundException($"Configuration file not found: {_configFilePath}");

        var yamlContent = File.ReadAllText(_configFilePath);

        try
        {
            var root = _deserializer.Deserialize<YamlAuditPolicyRoot>(yamlContent);

            if (root?.AuditPolicy == null)
                throw new InvalidOperationException("Invalid YAML structure: 'auditPolicy' root element not found");
            
            NormalizeFieldPolicies(root.AuditPolicy);

            return root;
        }
        catch (YamlException ex)
        {
            throw new InvalidOperationException($"Failed to parse YAML file: {ex.Message}", ex);
        }
    }

    public async Task<YamlAuditPolicyRoot> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_configFilePath))
            throw new FileNotFoundException($"Configuration file not found: {_configFilePath}");

        var yamlContent = await File.ReadAllTextAsync(_configFilePath, cancellationToken);

        try
        {
            var root = _deserializer.Deserialize<YamlAuditPolicyRoot>(yamlContent);

            if (root?.AuditPolicy == null)
                throw new InvalidOperationException("Invalid YAML structure: 'auditPolicy' root element not found");
            
            NormalizeFieldPolicies(root.AuditPolicy);

            return root;
        }
        catch (YamlException ex)
        {
            throw new InvalidOperationException($"Failed to parse YAML file: {ex.Message}", ex);
        }
    }
    
    private void NormalizeFieldPolicies(YamlAuditPolicy policy)
    {
        if (policy.Entities == null) return;

        foreach (var entity in policy.Entities.Values)
        {
            if (entity.Fields == null) continue;

            var normalizedFields = new Dictionary<string, object>();

            foreach (var (fieldName, fieldValue) in entity.Fields)
                if (fieldValue is string action)
                {
                    normalizedFields[fieldName] = new YamlFieldPolicy
                    {
                        Action = action
                    };
                }
                else if (fieldValue is Dictionary<object, object> dict)
                {
                    var fieldPolicy = ConvertToFieldPolicy(dict);
                    normalizedFields[fieldName] = fieldPolicy;
                }
                else
                {
                    normalizedFields[fieldName] = fieldValue;
                }

            entity.Fields = normalizedFields;
        }
    }
    
    private YamlFieldPolicy ConvertToFieldPolicy(Dictionary<object, object> dict)
    {
        var policy = new YamlFieldPolicy();

        foreach (var (key, value) in dict)
        {
            var keyStr = key.ToString()?.ToLower();

            switch (keyStr)
            {
                case "action":
                    policy.Action = value?.ToString();
                    break;
                case "view":
                    if (value is Dictionary<object, object> viewDict) policy.View = ConvertToViewSettings(viewDict);
                    break;
                case "mask":
                    if (value is Dictionary<object, object> maskDict) policy.Mask = ConvertToMaskSettings(maskDict);
                    break;
                case "hash":
                    if (value is Dictionary<object, object> hashDict) policy.Hash = ConvertToHashSettings(hashDict);
                    break;
                case "encrypt":
                    if (value is Dictionary<object, object> encryptDict)
                        policy.Encrypt = ConvertToEncryptSettings(encryptDict);
                    break;
            }
        }

        return policy;
    }

    private YamlViewSettings ConvertToViewSettings(Dictionary<object, object> dict)
    {
        var settings = new YamlViewSettings();
        if (dict.TryGetValue("format", out var format)) settings.Format = format?.ToString();
        if (dict.TryGetValue("pattern", out var pattern)) settings.Pattern = pattern?.ToString();
        if (dict.TryGetValue("culture", out var culture)) settings.Culture = culture?.ToString();
        if (dict.TryGetValue("refName", out var refName) && bool.TryParse(refName?.ToString(), out var rn))
            settings.RefName = rn;
        return settings;
    }

    private YamlMaskSettings ConvertToMaskSettings(Dictionary<object, object> dict)
    {
        var settings = new YamlMaskSettings();
        if (dict.TryGetValue("preset", out var preset)) settings.Preset = preset?.ToString();
        if (dict.TryGetValue("char", out var ch)) settings.Char = ch?.ToString();
        if (dict.TryGetValue("keepLeft", out var kl) && int.TryParse(kl?.ToString(), out var keepLeft))
            settings.KeepLeft = keepLeft;
        if (dict.TryGetValue("keepRight", out var kr) && int.TryParse(kr?.ToString(), out var keepRight))
            settings.KeepRight = keepRight;
        if (dict.TryGetValue("preserveDomain", out var pd) && bool.TryParse(pd?.ToString(), out var preserveDomain))
            settings.PreserveDomain = preserveDomain;
        if (dict.TryGetValue("preserveFormat", out var pf) && bool.TryParse(pf?.ToString(), out var preserveFormat))
            settings.PreserveFormat = preserveFormat;
        if (dict.TryGetValue("regex", out var regex)) settings.Regex = regex?.ToString();
        if (dict.TryGetValue("replace", out var replace)) settings.Replace = replace?.ToString();
        return settings;
    }

    private YamlHashSettings ConvertToHashSettings(Dictionary<object, object> dict)
    {
        var settings = new YamlHashSettings();
        if (dict.TryGetValue("preset", out var preset)) settings.Preset = preset?.ToString();
        if (dict.TryGetValue("algo", out var algo)) settings.Algo = algo?.ToString();
        if (dict.TryGetValue("pepperRef", out var pepperRef)) settings.PepperRef = pepperRef?.ToString();
        if (dict.TryGetValue("encoding", out var encoding)) settings.Encoding = encoding?.ToString();
        if (dict.TryGetValue("storeRaw", out var sr) && bool.TryParse(sr?.ToString(), out var storeRaw))
            settings.StoreRaw = storeRaw;
        if (dict.TryGetValue("storeHash", out var sh) && bool.TryParse(sh?.ToString(), out var storeHash))
            settings.StoreHash = storeHash;
        if (dict.TryGetValue("equalityToken", out var et) && bool.TryParse(et?.ToString(), out var equalityToken))
            settings.EqualityToken = equalityToken;
        return settings;
    }

    private YamlEncryptSettings ConvertToEncryptSettings(Dictionary<object, object> dict)
    {
        var settings = new YamlEncryptSettings();
        if (dict.TryGetValue("preset", out var preset)) settings.Preset = preset?.ToString();
        if (dict.TryGetValue("algo", out var algo)) settings.Algo = algo?.ToString();
        if (dict.TryGetValue("keyRef", out var keyRef)) settings.KeyRef = keyRef?.ToString();
        if (dict.TryGetValue("encoding", out var encoding)) settings.Encoding = encoding?.ToString();
        if (dict.TryGetValue("storeRaw", out var sr) && bool.TryParse(sr?.ToString(), out var storeRaw))
            settings.StoreRaw = storeRaw;
        return settings;
    }
}

public interface IAuditPolicyProvider
{
    YamlAuditPolicyRoot Load();
    Task<YamlAuditPolicyRoot> LoadAsync(CancellationToken cancellationToken = default);
}