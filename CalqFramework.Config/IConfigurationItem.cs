namespace CalqFramework.Config;

/// <summary>
///     Non-generic configuration item interface for internal registry storage.
/// </summary>
public interface IConfigurationItem {
    string? PresetGroup { get; }
    string Preset { get; set; }
    IEnumerable<string> AvailablePresets { get; }
    Task ReloadAsync();
    event Action? OnReloaded;
}

/// <summary>
///     Strongly-typed, covariant configuration item exposing the deserialized POCO.
/// </summary>
public interface IConfigurationItem<out TItem> : IConfigurationItem where TItem : class, new() {
    TItem Item { get; }
}
