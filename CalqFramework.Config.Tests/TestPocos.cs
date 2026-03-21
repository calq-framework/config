namespace CalqFramework.Config.Tests;

public class MasterPreset {
    public string Theme { get; set; } = "default";
    public string Region { get; set; } = "default";
}

[PresetGroup("Theme")]
public class UiConfig {
    public string Title { get; set; } = "";
    public int FontSize { get; set; }
    public bool DarkMode { get; set; }
}

[PresetGroup("Region")]
public class RegionConfig {
    public string Language { get; set; } = "";
    public string Currency { get; set; } = "";
}

public class SimpleConfig {
    public string Name { get; set; } = "";
    public int Value { get; set; }
}

public class CollectionConfig {
    public List<string> Tags { get; set; } = [];
    public Dictionary<string, int> Scores { get; set; } = [];
    public HashSet<string> Roles { get; set; } = [];
}

public class FieldConfig {
#pragma warning disable CA1051
    public int Count;
    public string Name = "";
#pragma warning restore CA1051
}
