namespace CalqFramework.Config;

/// <summary>
///     Abstract registry base. Manages a ConcurrentDictionary of configuration items,
///     bootstraps the master preset, and orchestrates cascading reloads.
/// </summary>
public abstract class ConfigurationRegistryBase<TPreset> : IConfigurationRegistry where TPreset : class, new() {
    private static readonly ConcurrentDictionary<string, Func<object, object?>> s_presetGroupAccessorCache = new();

    private readonly ConcurrentDictionary<Type, IConfigurationItem> _items = new();

    public IEnumerable<string> AvailablePresetGroups =>
        _items.Values.Select(x => x.PresetGroup)
            .Where(x => x is not null)
            .Distinct()!;

    public IEnumerable<string> GetAvailablePresets(string presetGroup) =>
        _items.Values.Where(x => x.PresetGroup == presetGroup)
            .SelectMany(x => x.AvailablePresets)
            .Distinct();

    public async Task<TItem> GetAsync<TItem>() where TItem : class, new() {
        IConfigurationItem item = await GetOrCreateItemAsync<TItem>();
        return ((IConfigurationItem<TItem>)item).Item;
    }

    public async Task<TItem> LoadAsync<TItem>() where TItem : class, new() =>
        await GetAsync<TItem>();

    public async Task ReloadAsync<TItem>() where TItem : class, new() {
        if (_items.TryGetValue(typeof(TItem), out IConfigurationItem? item)) {
            await item.ReloadAsync();
        }
    }

    public async Task SaveAsync<TItem>() where TItem : class, new() {
        IConfigurationItem item = await GetOrCreateItemAsync<TItem>();
        await item.SaveAsync();
    }

    public async Task ReloadAllAsync() {
        // 1. Reload master preset first
        IConfigurationItem masterItem = _items[typeof(TPreset)];
        await masterItem.ReloadAsync();
        TPreset masterPoco = ((IConfigurationItem<TPreset>)masterItem).Item;

        // 2. Cascade to all child items
        foreach (KeyValuePair<Type, IConfigurationItem> kvp in _items) {
            if (kvp.Key == typeof(TPreset)) {
                continue;
            }

            IConfigurationItem childItem = kvp.Value;
            if (childItem.PresetGroup is not null) {
                string resolvedPreset = ResolvePresetGroupValue(masterPoco, childItem.PresetGroup);
                if (childItem.Preset != resolvedPreset) {
                    childItem.Preset = resolvedPreset;
                    continue;
                }
            }

            await childItem.ReloadAsync();
        }
    }

    protected void Initialize() {
        IConfigurationItem<TPreset> masterItem = CreateItem<TPreset>("default");
        _items[typeof(TPreset)] = masterItem;
    }

    protected abstract IConfigurationItem<TItem> CreateItem<TItem>(string preset) where TItem : class, new();

    private async Task<IConfigurationItem> GetOrCreateItemAsync<TItem>() where TItem : class, new() {
        if (_items.TryGetValue(typeof(TItem), out IConfigurationItem? existing)) {
            return existing;
        }

        TPreset masterPoco = ((IConfigurationItem<TPreset>)_items[typeof(TPreset)]).Item;
        IConfigurationItem<TItem> tempItem = CreateItem<TItem>("default");
        string preset = tempItem.PresetGroup is not null ? ResolvePresetGroupValue(masterPoco, tempItem.PresetGroup) : "default";

        IConfigurationItem<TItem> item = CreateItem<TItem>(preset);
        await item.ReloadAsync();
        item = (IConfigurationItem<TItem>)_items.GetOrAdd(typeof(TItem), item);
        return item;
    }

    private static string ResolvePresetGroupValue(TPreset masterPoco, string propertyName) {
        Func<object, object?> accessor = s_presetGroupAccessorCache.GetOrAdd(
            propertyName,
            static (name, presetType) => {
                PropertyInfo? prop = presetType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (prop is not null) {
                    return obj => prop.GetValue(obj);
                }

                FieldInfo? field = presetType.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (field is not null) {
                    return obj => field.GetValue(obj);
                }

                throw new InvalidOperationException($"PresetGroup property or field '{name}' not found on {presetType.Name}.");
            },
            typeof(TPreset));

        return accessor(masterPoco)
            ?.ToString() ?? "default";
    }
}
