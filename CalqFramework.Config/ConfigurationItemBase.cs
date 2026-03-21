using System.Collections.Concurrent;

namespace CalqFramework.Config;

/// <summary>
///     Abstract base for configuration items. Handles preset group attribute caching,
///     item instantiation, and preset-change-triggered reloads.
/// </summary>
public abstract class ConfigurationItemBase<TItem> : IConfigurationItem<TItem> where TItem : class, new() {
    private static readonly ConcurrentDictionary<Type, string?> PresetGroupCache = new();

    private string _preset;

    protected ConfigurationItemBase(string preset) {
        _preset = preset;
        Item = new TItem();
        PresetGroup = PresetGroupCache.GetOrAdd(typeof(TItem), static t =>
            t.GetCustomAttributes(typeof(PresetGroupAttribute), false)
                .OfType<PresetGroupAttribute>()
                .FirstOrDefault()?.PropertyName);
    }

    public TItem Item { get; }
    public string? PresetGroup { get; }

    public string Preset {
        get => _preset;
        set {
            if (_preset == value) return;
            _preset = value;
            ReloadAsync(value).GetAwaiter().GetResult();
        }
    }

    public abstract IEnumerable<string> AvailablePresets { get; }

    public event Action? OnReloaded;

    public Task ReloadAsync() => ReloadAsync(_preset);

    protected abstract Task ReloadAsync(string preset);

    protected void RaiseOnReloaded() => OnReloaded?.Invoke();
}
