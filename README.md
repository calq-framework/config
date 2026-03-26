[![NuGet Version](https://img.shields.io/nuget/v/CalqFramework.Config?color=508cf0)](https://www.nuget.org/packages/CalqFramework.Config)
[![NuGet Downloads](https://img.shields.io/nuget/dt/CalqFramework.Config?color=508cf0)](https://www.nuget.org/packages/CalqFramework.Config)
[![REUSE status](https://api.reuse.software/badge/github.com/calq-framework/config)](https://api.reuse.software/info/github.com/calq-framework/config)

# Calq Config
Calq Config is a POCO-first configuration framework for .NET. Define plain C# classes, and Calq Config handles persistence, preset switching, and live reloads automatically — with stable object references that always reflect the latest state.  
No manual serialization, no string-based key lookups, no boilerplate.

## POCO-First Configuration for .NET
Calq Config treats your C# classes as the single source of truth. Properties and fields become configuration entries, presets become named file variants, and the framework keeps everything in sync — including cascading reloads across preset groups.

## How Calq Config Stacks Up

### Calq Config vs. Microsoft.Extensions.Configuration
| Feature | Calq Config | Microsoft.Extensions.Configuration |
| :--- | :--- | :--- |
| **Config Objects** | Mutable POCO Singletons | Immutable POCOs (via IOptions binding) |
| **Live Reload** | ✅ | ✅ |
| **Named Presets** | ✅ (automatic) | ✅ (manual) |
| **Preset Groups** | ✅ (master preset cascading) | ❌ |
| **Preset Switching at Runtime** | ✅ | ❌ |
| **Save Back to File** | ✅ | ❌ |
| **Save Back to File by JSONPath** | ✅ | ❌ |
| **Field Support** | ✅ | ❌ |
| **Learning Curve** | Low | Moderate |

### Code Comparison

### Calq Config
```csharp
using CalqFramework.Config.Json;

var registry = new JsonConfigurationRegistry();
var ui = await registry.GetAsync<UiConfig>();

Console.WriteLine(ui.Title);    // direct property access
Console.WriteLine(ui.DarkMode); // always current after reloads
```

### Microsoft.Extensions.Configuration
```csharp
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var ui = configuration.GetSection("UiConfig").Get<UiConfig>();

Console.WriteLine(ui.Title);
Console.WriteLine(ui.DarkMode);
```

## Usage

### 1. Application Setup & Initialization

*How to bootstrap the configuration registry and start loading configuration.*

#### How to Set Up JsonConfigurationRegistry

`JsonConfigurationRegistry` is the main entry point. It manages a directory of JSON files, one per configuration type per preset.

```csharp
using CalqFramework.Config.Json;

// Default directory resolution:
// 1. {AppContext.BaseDirectory}/config (if it exists)
// 2. {AppData}/{ProcessName}
var registry = new JsonConfigurationRegistry();

// Or specify a directory explicitly
var registry = new JsonConfigurationRegistry("/path/to/config");
```

**Key points:**
- The directory is created automatically if it doesn't exist
- File naming convention: `{FullTypeName}.{preset}.json` (e.g., `MyApp.UiConfig.dark.json`)
- `JsonConfigurationRegistry` (non-generic) disables preset group logic — all items use the `"default"` preset unless switched manually
- `JsonConfigurationRegistry<TPreset>` enables preset group cascading from a master preset POCO

See also: [How to Use Preset Groups with a Master Preset](#how-to-use-preset-groups-with-a-master-preset)

---

### 2. Configuration Items

*How to define, load, save, and reload configuration.*

#### How to Define Configuration POCOs

Configuration types are plain C# classes with a parameterless constructor. Properties and fields with public getters/setters become configuration entries.

```csharp
class AppSettings {
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 8080;
    public bool EnableSsl { get; set; } = false;
    public List<string> AllowedOrigins { get; set; } = new();
}
```

**Supported member types:**
- Properties with public get/set
- Public fields
- Collections: `List<T>`, `HashSet<T>`, `Dictionary<TKey, TValue>`, and other `ICollection<T>` implementations
- Any type serializable by Newtonsoft.Json

**Key points:**
- Default values in the class definition serve as fallbacks when the JSON file is missing or incomplete
- Original casing is preserved in JSON (no camelCase transformation)
- Case-insensitive matching is used when reading JSON back into the POCO

#### How to Load and Access Configuration

Use `GetAsync<T>()` to load a configuration item. The first call deserializes from disk; subsequent calls return the same instance.

```csharp
var registry = new JsonConfigurationRegistry();

AppSettings settings = await registry.GetAsync<AppSettings>();
Console.WriteLine(settings.Host); // "localhost" or value from JSON file

// Same instance every time
AppSettings same = await registry.GetAsync<AppSettings>();
Assert.Same(settings, same);
```

**`LoadAsync<T>()` is an alias for `GetAsync<T>()`** — both return the same singleton instance.

**Key points:**
- The returned object reference is stable — it survives reloads and always reflects the latest state
- If no JSON file exists, the POCO retains its default values
- Thread-safe via `ConcurrentDictionary` internally

#### How to Save Configuration

`SaveAsync<T>()` serializes the current POCO state to its JSON file.

```csharp
var registry = new JsonConfigurationRegistry();
AppSettings settings = await registry.GetAsync<AppSettings>();

settings.Host = "example.com";
settings.Port = 443;
await registry.SaveAsync<AppSettings>();
// Writes to: {configDir}/MyApp.AppSettings.default.json
```

#### How to Reload Configuration

`ReloadAsync<T>()` re-reads the JSON file and populates the existing POCO instance in-place.

```csharp
var registry = new JsonConfigurationRegistry();
AppSettings settings = await registry.GetAsync<AppSettings>();
AppSettings reference = settings; // hold a reference

// External process edits the JSON file...

await registry.ReloadAsync<AppSettings>();

// Same object, updated values
Assert.Same(reference, settings);
Console.WriteLine(settings.Host); // reflects the file change
```

**Collection reload behavior:**
- Collections are cleared and repopulated (not replaced) — the same `List<T>`, `Dictionary<TKey, TValue>`, or `HashSet<T>` instance is reused
- Items removed from the JSON file are removed from the collection
- Items added to the JSON file are added to the collection

**Reload events:**

```csharp
var item = new JsonConfigurationItem<AppSettings>("/path/to/config", "default");
item.OnReloaded += () => Console.WriteLine("Config reloaded");
await item.ReloadAsync();
```

See also: [How to Use Partial Updates](#how-to-use-partial-updates)

---

### 3. Presets

*How to manage named configuration variants.*

#### How to Work with Presets

Each configuration type can have multiple named presets, stored as separate JSON files. The default preset is `"default"`.

**File layout:**
```
config/
  MyApp.UiConfig.default.json
  MyApp.UiConfig.dark.json
  MyApp.UiConfig.light.json
```

**Discovering available presets:**

```csharp
var item = new JsonConfigurationItem<UiConfig>("/path/to/config", "default");
IEnumerable<string> presets = item.AvailablePresets;
// ["default", "dark", "light"]
```

**Switching presets:**

```csharp
var item = new JsonConfigurationItem<UiConfig>("/path/to/config", "default");
await item.ReloadAsync();

// Switch to dark preset — reloads from MyApp.UiConfig.dark.json
item.Preset = "dark";
Console.WriteLine(item.Item.DarkMode); // true
```

**Key points:**
- Setting `Preset` triggers an automatic reload from the new preset's file
- If the target preset file doesn't exist, the current POCO state is saved to it first (clone behavior), then reloaded
- Available presets are discovered by globbing `{TypeName}.*.json` in the config directory

---

### 4. Preset Groups & Master Preset

*How to orchestrate multiple configuration types from a single master preset.*

#### How to Use Preset Groups with a Master Preset

Use `JsonConfigurationRegistry<TPreset>` with `[PresetGroup]` attributes to let a master preset control which preset file is loaded for each configuration type.

**Define a master preset:**

```csharp
class MasterPreset {
    public string Theme { get; set; } = "default";
    public string Region { get; set; } = "default";
}
```

**Tag configuration types with `[PresetGroup]`:**

```csharp
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
config/
  MyApp.MasterPreset.default.json    → {"Theme":"dark","Region":"us"}
  MyApp.UiConfig.dark.json           → {"Title":"Dark UI","FontSize":14,"DarkMode":true}
  MyApp.UiConfig.light.json          → {"Title":"Light UI","FontSize":12,"DarkMode":false}
  MyApp.RegionConfig.us.json         → {"Language":"en","Currency":"USD"}
  MyApp.RegionConfig.eu.json         → {"Language":"de","Currency":"EUR"}
```

**Usage:**

```csharp
var registry = new JsonConfigurationRegistry<MasterPreset>("/path/to/config");

UiConfig ui = await registry.GetAsync<UiConfig>();
// Loaded from UiConfig.dark.json (because MasterPreset.Theme == "dark")

RegionConfig region = await registry.GetAsync<RegionConfig>();
// Loaded from RegionConfig.us.json (because MasterPreset.Region == "us")
```

**Cascading reloads:**

`ReloadAllAsync()` reloads the master preset first, then cascades to all child items — switching their preset files if the master's values changed.

```csharp
// External edit changes MasterPreset.default.json: Theme = "light"

await registry.ReloadAllAsync();

// ui is now populated from UiConfig.light.json
Console.WriteLine(ui.Title);    // "Light UI"
Console.WriteLine(ui.DarkMode); // false
```

**Querying preset groups:**

```csharp
IEnumerable<string> groups = registry.AvailablePresetGroups;
// ["Theme", "Region"]

IEnumerable<string> themePresets = registry.GetAvailablePresets("Theme");
// ["dark", "light"]
```

**Key points:**
- `[PresetGroup("PropertyName")]` maps a configuration type to a property on the master preset POCO
- The master preset itself always uses the `"default"` preset
- `ReloadAllAsync()` reloads the master first, then cascades — if a child's resolved preset changed, it switches automatically
- Configuration types without `[PresetGroup]` always use the `"default"` preset

See also: [How to Set Up JsonConfigurationRegistry](#how-to-set-up-jsonconfigurationregistry)

---

### 5. Partial Updates

*How to modify individual values without full serialization round-trips.*

#### How to Use Partial Updates

`SetByPathAsync` modifies a single value in the JSON file by dot-separated path, then reloads the POCO to stay in sync.

```csharp
var registry = new JsonConfigurationRegistry();
AppSettings settings = await registry.GetAsync<AppSettings>();

// Update a single value — writes to file and reloads
await registry.SetByPathAsync<AppSettings>("Host", "example.com");
await registry.SetByPathAsync<AppSettings>("Port", "443");

Console.WriteLine(settings.Host); // "example.com"
Console.WriteLine(settings.Port); // 443
```

**Nested paths:**

```csharp
await registry.SetByPathAsync<AppSettings>("Nested.DeepValue", "42");
// Creates intermediate objects if they don't exist in the JSON
```

**Key points:**
- Type preservation: if the existing JSON value is an integer, the new string value is parsed as integer
- Missing path segments are created automatically
- The POCO is reloaded after the file write to stay in sync
- Useful for CLI tools or APIs that need to set individual config values without loading the full object

---

### 6. Extensibility

*How to implement custom storage backends.*

#### How to Create a Custom Backend

The JSON implementation is one backend. You can create others (database, remote API, YAML, etc.) by extending `ConfigurationItemBase<T>` and `ConfigurationRegistryBase<T>`.

**Custom configuration item:**

```csharp
using CalqFramework.Config;

class DatabaseConfigurationItem<TItem> : ConfigurationItemBase<TItem> where TItem : class, new() {
    private readonly string _connectionString;

    public DatabaseConfigurationItem(string connectionString, string preset) : base(preset) {
        _connectionString = connectionString;
    }

    public override IEnumerable<string> AvailablePresets {
        get {
            // Query database for available presets
            return QueryPresets(_connectionString, typeof(TItem).FullName!);
        }
    }

    protected override async Task ReloadAsync(string preset) {
        string json = await ReadFromDatabase(_connectionString, typeof(TItem).FullName!, preset);
        // Deserialize json into Item (populate properties/fields)
        RaiseOnReloaded();
    }

    public override async Task SaveAsync() {
        string json = SerializeItem();
        await WriteToDatabase(_connectionString, typeof(TItem).FullName!, Preset, json);
    }

    public override async Task SetByPathAsync(string jsonPath, string value) {
        // Implement partial update logic for your backend
        await Task.CompletedTask;
    }

    protected override bool PresetExists(string preset) {
        return CheckPresetExists(_connectionString, typeof(TItem).FullName!, preset);
    }

    // ... database helper methods
}
```

**Custom configuration registry:**

```csharp
class DatabaseConfigurationRegistry<TPreset> : ConfigurationRegistryBase<TPreset> where TPreset : class, new() {
    private readonly string _connectionString;

    public DatabaseConfigurationRegistry(string connectionString) {
        _connectionString = connectionString;
        Initialize(); // required — bootstraps the master preset item
    }

    protected override IConfigurationItem<TItem> CreateItem<TItem>(string preset) =>
        new DatabaseConfigurationItem<TItem>(_connectionString, preset);
}
```

**Key points:**
- `ConfigurationItemBase<T>` handles preset group attribute caching, preset switching logic, and the `OnReloaded` event
- `ConfigurationRegistryBase<T>` handles the item dictionary, master preset cascading, and `ReloadAllAsync` orchestration
- Call `Initialize()` in your registry constructor — it creates the master preset item
- Your backend only needs to implement the storage operations: read, write, list presets, check existence

## Quick Start

```bash
dotnet new console -n QuickStart
cd QuickStart
dotnet add package CalqFramework.Config
```

Replace `Program.cs` with:

```csharp
using CalqFramework.Config.Json;

var registry = new JsonConfigurationRegistry();

var settings = await registry.GetAsync<AppSettings>();
Console.WriteLine($"Host: {settings.Host}, Port: {settings.Port}");

settings.Host = "example.com";
settings.Port = 443;
await registry.SaveAsync<AppSettings>();

Console.WriteLine($"Host: {settings.Host}, Port: {settings.Port}");

class AppSettings {
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 8080;
    public bool EnableSsl { get; set; } = false;
}
```

```bash
dotnet run
```

## License
Calq Config is dual-licensed under GNU AGPLv3 and the Calq Commercial License.
