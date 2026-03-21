namespace CalqFramework.Config.Json;

/// <summary>
///     JSON file-backed configuration registry with cross-platform directory resolution.
/// </summary>
public class JsonConfigurationRegistry<TPreset> : ConfigurationRegistryBase<TPreset> where TPreset : class, new() {
    private readonly string _configDir;

    public JsonConfigurationRegistry(string configDir) {
        _configDir = configDir;
        Directory.CreateDirectory(_configDir);
        Initialize();
    }

    public JsonConfigurationRegistry() : this(ResolveDefaultConfigDir()) { }

    public string ConfigDir => _configDir;

    protected override IConfigurationItem<TItem> CreateItem<TItem>(string preset) =>
        new JsonConfigurationItem<TItem>(_configDir, preset);

    private static string ResolveDefaultConfigDir() {
        var baseDir = Path.Combine(AppContext.BaseDirectory, "config");
        if (Directory.Exists(baseDir))
            return baseDir;

        var processName = Environment.ProcessPath is not null
            ? Path.GetFileNameWithoutExtension(Environment.ProcessPath)
            : "app";
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, processName!);
    }
}

/// <summary>
///     Parameterless JSON registry variant that disables preset logic.
/// </summary>
public class JsonConfigurationRegistry : JsonConfigurationRegistry<EmptyPreset> {
    public JsonConfigurationRegistry(string configDir) : base(configDir) { }
    public JsonConfigurationRegistry() : base() { }
}
