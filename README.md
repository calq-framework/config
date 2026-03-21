[![NuGet Version](https://img.shields.io/nuget/v/CalqFramework.Config?color=508cf0)](https://www.nuget.org/packages/CalqFramework.Config)
[![NuGet Downloads](https://img.shields.io/nuget/dt/CalqFramework.Config?color=508cf0)](https://www.nuget.org/packages/CalqFramework.Config)
[![REUSE status](https://api.reuse.software/badge/github.com/calq-framework/config)](https://api.reuse.software/info/github.com/calq-framework/config)

# Calq Config
Calq Config is a preset-driven configuration framework for .NET. Define a master preset, tag your config classes, and Calq Config automatically switches all dependent configurations when the active preset changes.  
No boilerplate, no manual wiring — just POCOs, JSON files, and cascading preset orchestration.

## Cascading Preset Configuration for .NET
Calq Config introduces a master preset model where a single POCO controls which preset files are loaded across all configuration types. Change the master, call `ReloadAllAsync()`, and every tagged configuration switches automatically — with reference-stable reloads that keep your object references intact.

## How It Compares

### Calq Config vs. Microsoft.Extensions.Configuration
| Feature | Calq Config | Microsoft.Extensions.Configuration |
| :--- | :--- | :--- |
| **Configuration Definition** | Auto-mapped from POCOs | Builder + Bind |
| **Persistence Format** | JSON (extensible) | JSON, XML, INI, Environment, CLI |
| **Thread-Safe Registry** | ✅ ConcurrentDictionary | ✅ DI-based |
| **Change Notifications** | ✅ OnReloaded event | ✅ IOptionsMonitor |
| **Preset / Profile System** | ✅ Built-in | ❌ Manual |
| **Cascading Preset Reloads** | ✅ Automatic | ❌ |
| **Reference-Stable Reloads** | ✅ Same object instance | ❌ New snapshot |
| **Save Back to File** | ✅ | ❌ |
| **Collection Replace on Reload** | ✅ | ❌ |
| **Preset Discovery** | ✅ Glob-based | ❌ |
| **Learning Curve** | Low | Moderate |

### Code Comparison

### Calq Config
```csharp
using CalqFramework.Config.Json;

var registry = new JsonConfigurationRegistry();
var db = await registry.GetAsync<DatabaseConfig>();

Console.WriteLine($"Host: {db.Host}, Port: {db.Port}");
```

### Microsoft.Extensions.Configuration
```csharp
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json")
    .Build();

var db = new DatabaseConfig();
config.GetSection("DatabaseConfig").Bind(db);

Console.WriteLine($"Host: {db.Host}, Port: {db.Port}");
```

## Usage

### 1. Registry & Loading

*How to bootstrap the registry and load configuration.*

#### How to Use the Configuration Framework

```csharp
using CalqFramework.Config.Json;

var registry = new JsonConfigurationRegistry();
var settings = await registry.GetAsync<AppSettings>();
```

**What `JsonConfigurationRegistry` does automatically:**
- Resolves the configuration directory (app base `config/` folder, or `ApplicationData/<processName>/`)
- Creates the directory if it doesn't exist
- Discovers JSON files matching `{FullTypeName}.{preset}.json`
- Deserializes into your POCO, preserving the same object reference across reloads

**Custom configuration directory:**

```csharp
var registry = new JsonConfigurationRegistry("/etc/myapp/config");
```

**Key points:**
- `GetAsync<T>()` returns the same instance on repeated calls (singleton per type)
- `LoadAsync<T>()` is an alias for `GetAsync<T>()`
- Missing JSON files are no-ops — the POCO retains its default values
- Both properties and fields are included; property name matching is case-insensitive
- Any type serializable by Newtonsoft.Json is supported
- Collections are fully replaced on reload (not merged)

See also: [How to Use Preset Groups](#how-to-use-preset-groups), [How to Reload Configuration](#how-to-reload-configuration)

---

### 2. File Naming & Preset Groups

*How configuration files are discovered and how presets cascade.*

#### How File Naming Works

JSON configuration files follow the naming convention:

```
{FullTypeName}.{preset}.json
```

```
MyApp.AppSettings.default.json
MyApp.DatabaseConfig.production.json
```

The preset defaults to `"default"`. Available presets are discovered by globbing the configuration directory.

#### How to Use Preset Groups

Preset groups link configuration types to a master preset POCO, enabling automatic preset switching when the master changes.

```csharp
class MasterPreset {
    public string Theme { get; set; } = "default";
    public string Region { get; set; } = "default";
}

[PresetGroup("Theme")]
class UiConfig {
    public string Title { get; set; } = "";
    public bool DarkMode { get; set; }
}

[PresetGroup("Region")]
class RegionConfig {
    public string Language { get; set; } = "";
    public string Currency { get; set; } = "";
}
```

**File layout:**
```
MasterPreset.default.json          → {"Theme":"dark","Region":"us"}
UiConfig.dark.json                 → {"Title":"Dark UI","DarkMode":true}
UiConfig.light.json                → {"Title":"Light UI","DarkMode":false}
RegionConfig.us.json               → {"Language":"en","Currency":"USD"}
RegionConfig.eu.json               → {"Language":"de","Currency":"EUR"}
```

**Usage:**

```csharp
var registry = new JsonConfigurationRegistry<MasterPreset>("/etc/myapp/config");

var ui = await registry.GetAsync<UiConfig>();       // Loads UiConfig.dark.json
var region = await registry.GetAsync<RegionConfig>(); // Loads RegionConfig.us.json
```

When you call `ReloadAllAsync()`, the registry reloads the master preset first, then cascades to all children — switching each child to the preset file matching the master's current value for its `[PresetGroup]` property. The object references remain stable.

```csharp
// Master changes from Theme="dark" to Theme="light"
await registry.ReloadAllAsync();

// ui is the SAME object reference, now populated with light preset values
Console.WriteLine(ui.Title);     // "Light UI"
```

**Key points:**
- The master preset is always loaded with preset `"default"`
- Use `JsonConfigurationRegistry` (without type parameter) to disable preset logic entirely
- `AvailablePresetGroups` and `GetAvailablePresets(group)` expose discovery metadata

See also: [How to Reload Configuration](#how-to-reload-configuration)

---

### 3. Reload, Persistence & Change Notifications

*How to reload from disk, save changes back, and react to reloads.*

#### How to Reload Configuration

```csharp
await registry.ReloadAsync<AppSettings>();   // Reload a specific type
await registry.ReloadAllAsync();             // Reload master + cascade to all children
```

Reloads populate the existing object in-place — any reference you hold remains valid. Collections are fully replaced, not merged. Missing files are no-ops (the `OnReloaded` event still fires).

#### How to Save Configuration

```csharp
var item = new JsonConfigurationItem<AppSettings>("/etc/myapp/config", "default");
await item.ReloadAsync();

item.Item.Port = 9090;
await item.SaveAsync();  // Overwrites {FullTypeName}.{preset}.json
```

Save is available on `JsonConfigurationItem<T>`, not directly on the registry.

#### How to Listen for Changes

```csharp
item.OnReloaded += () => {
    Console.WriteLine($"Config reloaded! New port: {item.Item.Port}");
};
```

`OnReloaded` fires after every `ReloadAsync()` call, even if the file is missing.

See also: [How to Use Preset Groups](#how-to-use-preset-groups)

---

### 4. Customization

*How to extend the framework with custom storage backends.*

#### How to Implement a Custom Storage Backend

Extend `ConfigurationItemBase<TItem>` and `ConfigurationRegistryBase<TPreset>` to implement a new storage format:

```csharp
class YamlConfigurationItem<TItem> : ConfigurationItemBase<TItem> where TItem : class, new() {
    private readonly string _configDir;
    
    public YamlConfigurationItem(string configDir, string preset) : base(preset) {
        _configDir = configDir;
    }
    
    public override IEnumerable<string> AvailablePresets {
        get {
            string pattern = $"{typeof(TItem).FullName}.*.yaml";
            return Directory.EnumerateFiles(_configDir, pattern)
                .Select(f => {
                    string fileName = Path.GetFileName(f);
                    string typeName = typeof(TItem).FullName!;
                    return fileName[(typeName.Length + 1)..^5];
                });
        }
    }
    
    protected override async Task ReloadAsync(string preset) {
        string filePath = Path.Combine(_configDir, $"{typeof(TItem).FullName}.{preset}.yaml");
        if (!File.Exists(filePath)) {
            RaiseOnReloaded();
            return;
        }
        
        // Your YAML deserialization logic here
        // Populate Item properties in-place to preserve reference identity
        
        RaiseOnReloaded();
    }
    
    public override async Task SaveAsync() {
        string filePath = Path.Combine(_configDir, $"{typeof(TItem).FullName}.{Preset}.yaml");
        // Your YAML serialization logic here
        await File.WriteAllTextAsync(filePath, yaml);
    }
}

class YamlConfigurationRegistry<TPreset> : ConfigurationRegistryBase<TPreset> 
    where TPreset : class, new() {
    
    public YamlConfigurationRegistry(string configDir) {
        ConfigDir = configDir;
        Directory.CreateDirectory(ConfigDir);
        Initialize();
    }
    
    public string ConfigDir { get; }
    
    protected override IConfigurationItem<TItem> CreateItem<TItem>(string preset) =>
        new YamlConfigurationItem<TItem>(ConfigDir, preset);
}
```

**Key points:**
- `ConfigurationItemBase<TItem>` handles preset group caching, `OnReloaded`, and preset-change-triggered reloads
- `ConfigurationRegistryBase<TPreset>` handles the item dictionary, master preset bootstrapping, and cascading reload orchestration
- Call `RaiseOnReloaded()` at the end of `ReloadAsync`; call `Initialize()` in your registry constructor
- Preserve reference identity by populating the existing `Item` instance rather than replacing it

See also: [How to Use the Configuration Framework](#how-to-use-the-configuration-framework)

## Quick Start

```bash
git clone --branch latest https://github.com/calq-framework/config docs/config
dotnet new console -n QuickStart
cd QuickStart
dotnet add package CalqFramework.Config
```

**Program.cs:**
```csharp
using CalqFramework.Config.Json;

var registry = new JsonConfigurationRegistry("./config");

var settings = await registry.GetAsync<AppSettings>();
Console.WriteLine($"Host: {settings.Host}, Port: {settings.Port}");

class AppSettings {
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 8080;
}
```

**Create a config file** (`config/QuickStart.AppSettings.default.json`):
```json
{"Host": "example.com", "Port": 443}
```

```bash
dotnet run
```

## License
Calq Config is dual-licensed under GNU AGPLv3 and the Calq Commercial License.
