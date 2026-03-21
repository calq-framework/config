using System.Collections;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace CalqFramework.Config.Json;

/// <summary>
///     JSON file-backed configuration item.
///     Naming convention: {configDir}/{typeof(TItem).FullName}.{preset}.json
/// </summary>
public class JsonConfigurationItem<TItem> : ConfigurationItemBase<TItem> where TItem : class, new() {
    private readonly string _configDir;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonConfigurationItem(string configDir, string preset) : base(preset) {
        _configDir = configDir;
        _jsonOptions = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
    }

    public override IEnumerable<string> AvailablePresets {
        get {
            string? typeName = typeof(TItem).FullName;
            string pattern = $"{typeName}.*.json";
            if (!Directory.Exists(_configDir)) {
                return [];
            }

            return Directory.EnumerateFiles(_configDir, pattern)
                .Select(f => {
                    string fileName = Path.GetFileName(f);
                    // Remove "{TypeName}." prefix and ".json" suffix
                    string presetName = fileName[(typeName!.Length + 1)..^5];
                    return presetName;
                });
        }
    }

    protected override async Task ReloadAsync(string preset) {
        string filePath = GetFilePath(preset);
        if (!File.Exists(filePath)) {
            RaiseOnReloaded();
            return;
        }

        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        lock (Item) {
            PopulateItem(stream);
        }

        RaiseOnReloaded();
    }

    public override async Task SaveAsync() {
        string filePath = GetFilePath(Preset);
        string json = JsonSerializer.Serialize(Item, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    private string GetFilePath(string preset) =>
        Path.Combine(_configDir, $"{typeof(TItem).FullName}.{preset}.json");

    private void PopulateItem(Stream stream) {
        var typeInfo = (JsonTypeInfo<TItem>)_jsonOptions.GetTypeInfo(typeof(TItem));

        using var doc = JsonDocument.Parse(stream);
        JsonElement root = doc.RootElement;

        foreach (JsonProperty jsonProp in root.EnumerateObject()) {
            JsonPropertyInfo? propInfo = typeInfo.Properties.FirstOrDefault(p => string.Equals(p.Name, jsonProp.Name, StringComparison.OrdinalIgnoreCase));

            if (propInfo is null) {
                continue;
            }

            object? existingValue = propInfo.Get?.Invoke(Item);
            if (existingValue is not null && IsCollection(propInfo.PropertyType)) {
                ReplaceCollection(existingValue, jsonProp.Value, propInfo.PropertyType);
            } else {
                object? value = jsonProp.Value.Deserialize(propInfo.PropertyType, _jsonOptions);
                propInfo.Set!(Item, value);
            }
        }
    }

    private static bool IsCollection(Type type) =>
        type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

    private void ReplaceCollection(object existing, JsonElement jsonElement, Type collectionType) {
        if (existing is IDictionary dict) {
            dict.Clear();
            var deserialized = jsonElement.Deserialize(collectionType, _jsonOptions) as IDictionary;
            if (deserialized is not null) {
                foreach (DictionaryEntry entry in deserialized) {
                    dict[entry.Key] = entry.Value;
                }
            }

            return;
        }

        // List<T>, HashSet<T>, etc.
        Type? elementType = collectionType.GetGenericArguments()
            .FirstOrDefault();
        if (elementType is null) {
            return;
        }

        // Clear existing collection
        System.Reflection.MethodInfo? clearMethod = collectionType.GetMethod("Clear") ?? collectionType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))
            .Select(i => i.GetMethod("Clear"))
            .FirstOrDefault();
        clearMethod?.Invoke(existing, null);

        Type listType = typeof(List<>).MakeGenericType(elementType);
        if (jsonElement.Deserialize(listType, _jsonOptions) is not IList items) {
            return;
        }

        System.Reflection.MethodInfo? addMethod = collectionType.GetMethod("Add", [elementType]) ?? collectionType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))
            .Select(i => i.GetMethod("Add"))
            .FirstOrDefault();

        if (addMethod is not null) {
            foreach (object? item in items) {
                addMethod.Invoke(existing, [item]);
            }
        }
    }
}
