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
            var typeName = typeof(TItem).FullName;
            var pattern = $"{typeName}.*.json";
            if (!Directory.Exists(_configDir))
                return [];

            return Directory.EnumerateFiles(_configDir, pattern)
                .Select(f => {
                    var fileName = Path.GetFileName(f);
                    // Remove "{TypeName}." prefix and ".json" suffix
                    var presetName = fileName[(typeName!.Length + 1)..^5];
                    return presetName;
                });
        }
    }

    protected override async Task ReloadAsync(string preset) {
        var filePath = Path.Combine(_configDir, $"{typeof(TItem).FullName}.{preset}.json");
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

    private void PopulateItem(Stream stream) {
        var typeInfo = (JsonTypeInfo<TItem>)_jsonOptions.GetTypeInfo(typeof(TItem));

        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        foreach (var jsonProp in root.EnumerateObject()) {
            var propInfo = typeInfo.Properties.FirstOrDefault(p =>
                string.Equals(p.Name, jsonProp.Name, StringComparison.OrdinalIgnoreCase));

            if (propInfo is null) continue;

            var existingValue = propInfo.Get?.Invoke(Item);
            if (existingValue is not null && IsCollection(propInfo.PropertyType)) {
                AppendToCollection(existingValue, jsonProp.Value, propInfo.PropertyType);
            } else {
                var value = jsonProp.Value.Deserialize(propInfo.PropertyType, _jsonOptions);
                propInfo.Set!(Item, value);
            }
        }
    }

    private static bool IsCollection(Type type) =>
        type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

    private void AppendToCollection(object existing, JsonElement jsonElement, Type collectionType) {
        if (existing is IDictionary dict) {
            var deserialized = jsonElement.Deserialize(collectionType, _jsonOptions) as IDictionary;
            if (deserialized is not null) {
                foreach (DictionaryEntry entry in deserialized) {
                    dict[entry.Key] = entry.Value;
                }
            }
            return;
        }

        // List<T>, HashSet<T>, etc.
        var elementType = collectionType.GetGenericArguments().FirstOrDefault();
        if (elementType is null) return;

        var listType = typeof(List<>).MakeGenericType(elementType);
        var items = jsonElement.Deserialize(listType, _jsonOptions) as IList;
        if (items is null) return;

        var addMethod = collectionType.GetMethod("Add", [elementType])
            ?? collectionType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))
                .Select(i => i.GetMethod("Add"))
                .FirstOrDefault();

        if (addMethod is not null) {
            foreach (var item in items) {
                addMethod.Invoke(existing, [item]);
            }
        }
    }
}
