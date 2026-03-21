using System.Collections.Concurrent;
using System.Reflection;

namespace CalqFramework.Config;

/// <summary>
///     Abstract registry base. Manages a ConcurrentDictionary of configuration items,
///     bootstraps the master preset, and orchestrates cascading reloads.
/// </summary>
public abstract class ConfigurationRegistryBase<TPreset> : IConfigurationRegistry where TPreset : class, new() {
    private static readonly ConcurrentDictionary<string, Func<object, object?>> PresetGroupAccessorCache = new();

    private readonly ConcurrentDictionary<Type, IConfigurationItem> _items = new();

    protected ConfigurationRegistryBase() { }

    protected void Initialize() {
        var masterItem = CreateItem<TPreset>("default");
        _items[typeof(TPreset)] = masterItem;
    }

    public IEnumerable<string> AvailablePresetGroups =>
        _items.Values
            .Select(x => x.PresetGroup)
            .Where(x => x is not null)
            .Distinct()!;

    public IEnumerable<string> GetAvailablePresets(string presetGroup) =>
        _items.Values
            .Where(x => x.PresetGroup == presetGroup)
            .SelectMany(x => x.AvailablePresets)
            .Distinct();

    public async Task<TItem> GetAsync<TItem>() where TItem : class, new() {
        var item = await GetOrCreateItemAsync<TItem>();
        return ((IConfigurationItem<TItem>)item).Item;
    }

    public async Task<TItem> LoadAsync<TItem>() where TItem : class, new() =>
        await GetAsync<TItem>();

    public async Task ReloadAsync<TItem>() where TItem : class, new() {
        if (_items.TryGetValue(typeof(TItem), out var item)) {
            await item.ReloadAsync();
        }
    }

    public async Task ReloadAllAsync() {
        // 1. Reload master preset first
        var masterItem = _items[typeof(TPreset)];
        await masterItem.ReloadAsync();
        var masterPoco = ((IConfigurationItem<TPreset>)masterItem).Item;

        // 2. Cascade to all child items
        foreach (var kvp in _items) {
            if (kvp.Key == typeof(TPreset)) continue;

            var childItem = kvp.Value;
            if (childItem.PresetGroup is not null) {
                var resolvedPreset = ResolvePresetGroupValue(masterPoco, childItem.PresetGroup);
                if (childItem.Preset != resolvedPreset) {
                    childItem.Preset = resolvedPreset;
                    continue;
                }
            }

            await childItem.ReloadAsync();
        }
    }

    protected abstract IConfigurationItem<TItem> CreateItem<TItem>(string preset) where TItem : class, new();

    private async Task<IConfigurationItem> GetOrCreateItemAsync<TItem>() where TItem : class, new() {
        if (_items.TryGetValue(typeof(TItem), out var existing))
            return existing;

        var masterPoco = ((IConfigurationItem<TPreset>)_items[typeof(TPreset)]).Item;
        var tempItem = CreateItem<TItem>("default");
        var preset = tempItem.PresetGroup is not null
            ? ResolvePresetGroupValue(masterPoco, tempItem.PresetGroup)
            : "default";

        var item = CreateItem<TItem>(preset);
        await item.ReloadAsync();
        item = (IConfigurationItem<TItem>)_items.GetOrAdd(typeof(TItem), item);
        return item;
    }

    private static string ResolvePresetGroupValue(TPreset masterPoco, string propertyName) {
        var accessor = PresetGroupAccessorCache.GetOrAdd(propertyName, static (name, presetType) => {
            var prop = presetType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop is not null) {
                return obj => prop.GetValue(obj);
            }

            var field = presetType.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field is not null) {
                return obj => field.GetValue(obj);
            }

            throw new InvalidOperationException($"PresetGroup property or field '{name}' not found on {presetType.Name}.");
        }, typeof(TPreset));

        return accessor(masterPoco)?.ToString() ?? "default";
    }
}
