namespace CalqFramework.Config.Json;

/// <summary>
///     JSON file-backed configuration registry with cross-platform directory resolution.
/// </summary>
public class JsonConfigurationRegistry<TPreset> : ConfigurationRegistryBase<TPreset> where TPreset : class, new() {
    public JsonConfigurationRegistry(string configDir) {
        ConfigDir = configDir;
        Directory.CreateDirectory(ConfigDir);
        Initialize();
    }

    public JsonConfigurationRegistry() : this(ResolveDefaultConfigDir()) { }

    public string ConfigDir { get; }

    protected override IConfigurationItem<TItem> CreateItem<TItem>(string preset) =>
        new JsonConfigurationItem<TItem>(ConfigDir, preset);

    private static string ResolveDefaultConfigDir() {
        string baseDir = Path.Combine(AppContext.BaseDirectory, "config");
        if (Directory.Exists(baseDir)) {
            return baseDir;
        }

        string processName = Environment.ProcessPath is not null ? Path.GetFileNameWithoutExtension(Environment.ProcessPath) : "app";
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, processName!);
    }
}

/// <summary>
///     Parameterless JSON registry variant that disables preset logic.
/// </summary>
public class JsonConfigurationRegistry : JsonConfigurationRegistry<EmptyPreset> {
    public JsonConfigurationRegistry(string configDir) : base(configDir) { }
    public JsonConfigurationRegistry() { }
}
