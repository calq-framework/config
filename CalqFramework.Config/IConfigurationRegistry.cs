namespace CalqFramework.Config;

/// <summary>
///     Registry for managing configuration items with preset-based orchestration.
/// </summary>
public interface IConfigurationRegistry {
    IEnumerable<string> AvailablePresetGroups { get; }
    IEnumerable<string> GetAvailablePresets(string presetGroup);
    Task<TItem> GetAsync<TItem>() where TItem : class, new();
    Task<TItem> LoadAsync<TItem>() where TItem : class, new();
    Task ReloadAsync<TItem>() where TItem : class, new();
    Task ReloadAllAsync();
}
