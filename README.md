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
| **Preset / Profile System** | ✅ Built-in | ❌ Manual |
| **Cascading Preset Reloads** | ✅ Automatic | ❌ |
| **Reference-Stable Reloads** | ✅ Same object instance | ❌ New snapshot |
| **Save Back to File** | ✅ | ❌ |
| **Collection Replace on Reload** | ✅ | ❌ |
| **Preset Discovery** | ✅ Glob-based | ❌ |
| **Thread-Safe Registry** | ✅ ConcurrentDictionary | ✅ DI-based |
| **Change Notifications** | ✅ OnReloaded event | ✅ IOptionsMonitor |
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

### 1. Application Setup & Initialization

*How to bootstrap the configuration registry and start loading configuration.*

#### How to Use the Configuration Framework

The configuration framework requires minimal setup. Here's a complete working example:

**Complete configuration application:**

```csharp
using CalqFramework.Config.Json;

// Create a registry (auto-resolves config directory)
var registry = new JsonConfigurationRegistry();

// Load a configuration POCO
var settings = await registry.GetAsync<AppSettings>();

Console.WriteLine($"Host: {settings.Host}");
Console.WriteLine($"Port: {settings.Port}");

// Modify and save
settings.Port = 9090;
```

**What `JsonConfigurationRegistry` does automatically:**
- Resolves the configuration directory (app base `config/` folder, or `%APPDATA%/<processName>/`)
- Creates the directory if it doesn't exist
- Discovers JSON files matching the naming convention `{FullTypeName}.{preset}.json`
- Deserializes JSON into your POCO, preserving the same object reference across reloads

**Custom configuration directory:**

```csharp
var registry = new JsonConfigurationRegistry("/etc/myapp/config");
```

**Key points:**
- `GetAsync<T>()` returns the same instance on repeated calls (singleton per type)
- `LoadAsync<T>()` is an alias for `GetAsync<T>()`
- The configuration directory is created automatically if it doesn't exist
- Missing JSON files are treated as no-ops — the POCO retains its default values

See also: [How to Reload Configuration](#how-to-reload-configuration), [How to Save Configuration](#how-to-save-configuration)

#### How to Define Configuration Classes

Configuration classes are plain POCOs. No base class, interface, or attribute is required for basic usage.

```csharp
class AppSettings {
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 8080;
    public bool EnableSsl { get; set; } = false;
    public List<string> AllowedOrigins { get; set; } = new();
}
```

**Corresponding JSON file** (`MyApp.AppSettings.default.json`):
```json
{
    "Host": "example.com",
    "Port": 443,
    "EnableSsl": true,
    "AllowedOrigins": ["https://example.com", "https://api.example.com"]
}
```

**Fields are also supported:**

```csharp
class FieldConfig {
    public int Count;
    public string Name = "";
}
```

**Supported types:**

Type conversion is handled by Newtonsoft.Json, supporting:
- Primitives (`bool`, `byte`, `sbyte`, `char`, `decimal`, `double`, `float`, `int`, `uint`, `long`, `ulong`, `short`, `ushort`), `string`, `DateTime`
- Enums (case-insensitive matching)
- Nullable versions of all the above
- Collections: `List<T>`, `HashSet<T>`, `Dictionary<TKey, TValue>`, and other `IEnumerable<T>` types
- Nested objects (deserialized recursively)
- Any type serializable by Newtonsoft.Json

**Key points:**
- Property name matching is case-insensitive
- Both properties and fields are included
- Collections are fully replaced on reload (not merged)
- POCOs must have a parameterless constructor

See also: [How to Use Preset Groups](#how-to-use-preset-groups)

---

### 2. File Naming & Preset System

*How configuration files are discovered and how presets work.*

#### How File Naming Works

JSON configuration files follow a strict naming convention:

```
{FullTypeName}.{preset}.json
```

**Examples:**
```
MyApp.AppSettings.default.json
MyApp.DatabaseConfig.default.json
MyApp.DatabaseConfig.production.json
MyApp.DatabaseConfig.staging.json
```

- `FullTypeName` is the fully qualified .NET type name (namespace + class name)
- `preset` is a string identifier (defaults to `"default"`)
- All files live in a single flat directory

**Preset discovery:**

The framework discovers available presets by globbing the configuration directory:

```csharp
var registry = new JsonConfigurationRegistry("/etc/myapp/config");
var item = new JsonConfigurationItem<AppSettings>("/etc/myapp/config", "default");

// Discovers: ["default", "production", "staging"]
var presets = item.AvailablePresets;
```

#### How to Use Preset Groups

Preset groups link configuration types to a master preset POCO, enabling automatic preset switching when the master changes.

**Define a master preset:**

```csharp
class MasterPreset {
    public string Theme { get; set; } = "default";
    public string Region { get; set; } = "default";
}
```

**Link configuration types to preset groups:**

```csharp
using CalqFramework.Config;

[PresetGroup("Theme")]
class UiConfig {
    public string Title { get; set; } = "";
    public int FontSize { get; set; }
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
UiConfig.dark.json                 → {"Title":"Dark UI","FontSize":14,"DarkMode":true}
UiConfig.light.json                → {"Title":"Light UI","FontSize":12,"DarkMode":false}
RegionConfig.us.json               → {"Language":"en","Currency":"USD"}
RegionConfig.eu.json               → {"Language":"de","Currency":"EUR"}
```

**Usage:**

```csharp
using CalqFramework.Config.Json;

var registry = new JsonConfigurationRegistry<MasterPreset>("/etc/myapp/config");

var ui = await registry.GetAsync<UiConfig>();       // Loads UiConfig.dark.json
var region = await registry.GetAsync<RegionConfig>(); // Loads RegionConfig.us.json

Console.WriteLine(ui.Title);        // "Dark UI"
Console.WriteLine(region.Currency);  // "USD"
```

**How cascading works:**

When you call `ReloadAllAsync()`, the registry:
1. Reloads the master preset first
2. Reads each child item's `[PresetGroup]` attribute
3. Resolves the current value of that property on the master POCO
4. If the resolved preset changed, switches the child to the new preset file
5. If unchanged, reloads the child from its current preset file

```csharp
// Master changes from Theme="dark" to Theme="light"
await registry.ReloadAllAsync();

// ui is the SAME object reference, now populated with light preset values
Console.WriteLine(ui.Title);     // "Light UI"
Console.WriteLine(ui.DarkMode);  // false
```

**Key points:**
- `[PresetGroup("PropertyName")]` links a config type to a property on the master preset POCO
- The master preset is always loaded with preset `"default"`
- `ReloadAllAsync()` cascades changes from master to all children
- Use `JsonConfigurationRegistry` (without type parameter) to disable preset logic entirely

See also: [How to Reload Configuration](#how-to-reload-configuration), [How to Query Available Presets](#how-to-query-available-presets)

---

### 3. Reload & Persistence

*How to reload configuration from disk and save changes back.*

#### How to Reload Configuration

The registry provides multiple reload strategies.

**Reload a specific type:**

```csharp
// External process updates the JSON file...
await registry.ReloadAsync<AppSettings>();

// Same object reference, new values
Console.WriteLine(settings.Host);
```

**Reload everything (with cascading):**

```csharp
await registry.ReloadAllAsync();
```

`ReloadAllAsync()` reloads the master preset first, then cascades to all registered items. If a child's preset group value changed on the master, the child switches to the new preset file automatically.

**Reference identity:**

Reloads populate the existing object in-place. Any reference you hold remains valid:

```csharp
var settings = await registry.GetAsync<AppSettings>();
var reference = settings;

// External edit changes the file...
await registry.ReloadAsync<AppSettings>();

Assert.Same(reference, settings);  // Same object, updated values
```

**Collection behavior on reload:**

Collections are fully replaced, not merged:

```csharp
// Before reload: Tags = ["alpha", "beta", "gamma"]
// JSON file now: {"Tags": ["alpha", "gamma"]}
await registry.ReloadAsync<CollectionConfig>();
// After reload: Tags = ["alpha", "gamma"] — "beta" is gone
```

This applies to `List<T>`, `HashSet<T>`, `Dictionary<TKey, TValue>`, and other collection types.

**Missing files:**

If the JSON file doesn't exist, reload is a no-op — the POCO retains its current values and the `OnReloaded` event still fires.

See also: [How to Listen for Changes](#how-to-listen-for-changes), [How to Use Preset Groups](#how-to-use-preset-groups)

#### How to Save Configuration

Use `SaveAsync()` on a `JsonConfigurationItem` to persist the current POCO state back to its JSON file.

```csharp
var item = new JsonConfigurationItem<AppSettings>("/etc/myapp/config", "default");
await item.ReloadAsync();

item.Item.Port = 9090;
await item.SaveAsync();
```

The file is written using Newtonsoft.Json serialization with the same options used for deserialization (case-insensitive, fields included).

**Key points:**
- `SaveAsync()` overwrites the entire file with the current POCO state
- The file path follows the same naming convention: `{FullTypeName}.{preset}.json`
- Save is available on `JsonConfigurationItem<T>`, not directly on the registry

See also: [How to Reload Configuration](#how-to-reload-configuration)

---

### 4. Change Notifications

*How to react when configuration changes.*

#### How to Listen for Changes

Every configuration item exposes an `OnReloaded` event that fires after each reload.

```csharp
var item = new JsonConfigurationItem<AppSettings>("/etc/myapp/config", "default");

item.OnReloaded += () => {
    Console.WriteLine($"Config reloaded! New port: {item.Item.Port}");
};

await item.ReloadAsync();  // Fires OnReloaded
```

**Key points:**
- `OnReloaded` fires after every `ReloadAsync()` call, even if the file is missing
- The event fires after the POCO has been fully populated
- Multiple handlers can be registered
- The event is available on both `JsonConfigurationItem<T>` and through the `IConfigurationItem` interface

See also: [How to Reload Configuration](#how-to-reload-configuration)

---

### 5. Preset Discovery & Querying

*How to discover what presets and preset groups are available.*

#### How to Query Available Presets

The registry exposes metadata about registered preset groups and their available presets.

**List preset groups:**

```csharp
var registry = new JsonConfigurationRegistry<MasterPreset>("/etc/myapp/config");
await registry.GetAsync<UiConfig>();
await registry.GetAsync<RegionConfig>();

// Returns: ["Theme", "Region"]
var groups = registry.AvailablePresetGroups;
```

**List presets for a group:**

```csharp
// Returns: ["dark", "light"] (discovered from UiConfig.*.json files)
var themePresets = registry.GetAvailablePresets("Theme");
```

**Item-level preset discovery:**

```csharp
var item = new JsonConfigurationItem<UiConfig>("/etc/myapp/config", "default");

// Returns: ["default", "dark", "light"]
var presets = item.AvailablePresets;
```

**Key points:**
- `AvailablePresetGroups` returns distinct group names from all registered items that have `[PresetGroup]`
- `GetAvailablePresets()` discovers presets by globbing JSON files matching the type's naming pattern
- Preset groups are only populated after `GetAsync<T>()` registers the item

See also: [How to Use Preset Groups](#how-to-use-preset-groups)

---

### 6. Configuration & Customization

*How to extend the framework with custom storage backends.*

#### How to Implement a Custom Storage Backend

The framework is designed around two abstract base classes that can be extended for any storage format.

**Custom configuration item:**

Extend `ConfigurationItemBase<TItem>` to implement a new storage backend:

```csharp
using CalqFramework.Config;

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
```

**Custom configuration registry:**

Extend `ConfigurationRegistryBase<TPreset>` to wire up your custom item type:

```csharp
using CalqFramework.Config;

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
- `ConfigurationItemBase<TItem>` handles preset group caching, the `OnReloaded` event, and preset-change-triggered reloads
- `ConfigurationRegistryBase<TPreset>` handles the item dictionary, master preset bootstrapping, and cascading reload orchestration
- Call `RaiseOnReloaded()` at the end of your `ReloadAsync` implementation
- Call `Initialize()` in your registry constructor to bootstrap the master preset
- Preserve reference identity by populating the existing `Item` instance rather than replacing it

See also: [How to Use the Configuration Framework](#how-to-use-the-configuration-framework)

#### How to Configure Directory Resolution

`JsonConfigurationRegistry` resolves the configuration directory automatically when no path is provided:

1. First checks for a `config/` directory relative to `AppContext.BaseDirectory`
2. If not found, falls back to `%APPDATA%/<processName>/` (or platform equivalent)

**Override with explicit path:**

```csharp
// Absolute path
var registry = new JsonConfigurationRegistry("/etc/myapp/config");

// Relative path
var registry = new JsonConfigurationRegistry("./config");
```

**Key points:**
- The directory is created automatically if it doesn't exist
- `AppContext.BaseDirectory` is typically the directory containing the application binary
- On Windows, `%APPDATA%` resolves to `C:\Users\<user>\AppData\Roaming`
- On Linux/macOS, the equivalent is `~/.config` (via `Environment.SpecialFolder.ApplicationData`)

See also: [How to Use the Configuration Framework](#how-to-use-the-configuration-framework)

---

### 7. Thread Safety & Concurrency

*How the framework handles concurrent access.*

#### How Thread Safety Works

The framework is designed for concurrent access:

- `ConfigurationRegistryBase` stores items in a `ConcurrentDictionary<Type, IConfigurationItem>`
- `GetAsync<T>()` uses `GetOrAdd` for safe concurrent registration
- `JsonConfigurationItem` locks on the `Item` instance during JSON population
- Preset group attribute lookups are cached in a static `ConcurrentDictionary`
- Multiple concurrent `ReloadAsync` calls on the same item are safe

```csharp
var registry = new JsonConfigurationRegistry("/etc/myapp/config");
var settings = await registry.GetAsync<AppSettings>();

// Safe: concurrent reloads
var tasks = Enumerable.Range(0, 10)
    .Select(_ => registry.ReloadAsync<AppSettings>());
await Task.WhenAll(tasks);
```

**Key points:**
- All registry operations are thread-safe
- Reload populates the existing object under a lock
- Concurrent `GetAsync<T>()` calls for the same type return the same instance

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
