namespace CalqFramework.Config;

/// <summary>
///     Abstract base for configuration items. Handles preset group attribute caching,
///     item instantiation, and preset-change-triggered reloads.
/// </summary>
public abstract class ConfigurationItemBase<TItem>(string preset) : IConfigurationItem<TItem> where TItem : class, new() {
    private static readonly ConcurrentDictionary<Type, string?> s_presetGroupCache = new();

    private string _preset = preset;

    public TItem Item { get; } = new();

    public string? PresetGroup { get; } = s_presetGroupCache.GetOrAdd(
        typeof(TItem),
        static t => t.GetCustomAttributes(typeof(PresetGroupAttribute), false)
            .OfType<PresetGroupAttribute>()
            .FirstOrDefault()
            ?.PropertyName);

    public string Preset {
        get => _preset;
        set {
            if (_preset == value) {
                return;
            }

            string oldPreset = _preset;
            _preset = value;
            if (!PresetExists(value)) {
                // Clone current POCO state to the new preset
                SaveAsync()
                    .GetAwaiter()
                    .GetResult();
            }

            ReloadAsync(value)
                .GetAwaiter()
                .GetResult();
        }
    }

    public abstract IEnumerable<string> AvailablePresets { get; }

    public event Action? OnReloaded;

    public Task ReloadAsync() => ReloadAsync(_preset);

    public abstract Task SaveAsync();

    public abstract Task SetByPathAsync(string jsonPath, string value);

    protected abstract Task ReloadAsync(string preset);

    protected abstract bool PresetExists(string preset);

    protected void RaiseOnReloaded() => OnReloaded?.Invoke();
}
