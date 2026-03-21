using System.Collections;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace CalqFramework.Config.Json;

/// <summary>
///     JSON file-backed configuration item.
///     Naming convention: {configDir}/{typeof(TItem).FullName}.{preset}.json
/// </summary>
public class JsonConfigurationItem<TItem> : ConfigurationItemBase<TItem> where TItem : class, new() {
    private readonly string _configDir;
    private readonly JsonSerializerSettings _jsonSettings;

    public JsonConfigurationItem(string configDir, string preset) : base(preset) {
        _configDir = configDir;
        _jsonSettings = new JsonSerializerSettings {
            ContractResolver = new DefaultContractResolver {
                NamingStrategy = null // preserve original casing
            }
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
                    string presetName = fileName.Substring(typeName!.Length + 1, fileName.Length - typeName.Length - 1 - 5);
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

        string json;
        using (var reader = new StreamReader(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))) {
            json = await reader.ReadToEndAsync();
        }

        lock (Item) {
            PopulateItem(json);
        }

        RaiseOnReloaded();
    }

    public override async Task SaveAsync() {
        string filePath = GetFilePath(Preset);
        string json = JsonConvert.SerializeObject(Item, Formatting.None, _jsonSettings);
        await File.WriteAllTextAsync(filePath, json);
    }

    private string GetFilePath(string preset) =>
        Path.Combine(_configDir, $"{typeof(TItem).FullName}.{preset}.json");

    private void PopulateItem(string json) {
        JObject obj = JObject.Parse(json);

        foreach (JProperty jsonProp in obj.Properties()) {
            // Try property first (case-insensitive)
            PropertyInfo? prop = typeof(TItem).GetProperty(jsonProp.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (prop is not null && prop.CanWrite && prop.CanRead) {
                object? existingValue = prop.GetValue(Item);
                if (existingValue is not null && IsCollection(prop.PropertyType)) {
                    ReplaceCollection(existingValue, jsonProp.Value, prop.PropertyType);
                } else {
                    object? value = jsonProp.Value.ToObject(prop.PropertyType);
                    prop.SetValue(Item, value);
                }

                continue;
            }

            // Try field (case-insensitive)
            FieldInfo? field = typeof(TItem).GetFields(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(f => string.Equals(f.Name, jsonProp.Name, StringComparison.OrdinalIgnoreCase));

            if (field is not null) {
                object? existingValue = field.GetValue(Item);
                if (existingValue is not null && IsCollection(field.FieldType)) {
                    ReplaceCollection(existingValue, jsonProp.Value, field.FieldType);
                } else {
                    object? value = jsonProp.Value.ToObject(field.FieldType);
                    field.SetValue(Item, value);
                }
            }
        }
    }

    private static bool IsCollection(Type type) =>
        type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

    private static void ReplaceCollection(object existing, JToken jsonToken, Type collectionType) {
        if (existing is IDictionary dict) {
            dict.Clear();
            var deserialized = jsonToken.ToObject(collectionType) as IDictionary;
            if (deserialized is not null) {
                foreach (DictionaryEntry entry in deserialized) {
                    dict[entry.Key] = entry.Value;
                }
            }

            return;
        }

        Type? elementType = collectionType.GetGenericArguments()
            .FirstOrDefault();
        if (elementType is null) {
            return;
        }

        // Clear existing collection
        MethodInfo? clearMethod = collectionType.GetMethod("Clear") ?? collectionType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))
            .Select(i => i.GetMethod("Clear"))
            .FirstOrDefault();
        clearMethod?.Invoke(existing, null);

        Type listType = typeof(List<>).MakeGenericType(elementType);
        if (jsonToken.ToObject(listType) is not IList items) {
            return;
        }

        MethodInfo? addMethod = collectionType.GetMethod(
            "Add",
            [
                elementType
            ]) ?? collectionType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))
            .Select(i => i.GetMethod("Add"))
            .FirstOrDefault();

        if (addMethod is not null) {
            foreach (object? item in items) {
                addMethod.Invoke(
                    existing,
                    new[] {
                        item
                    });
            }
        }
    }
}
