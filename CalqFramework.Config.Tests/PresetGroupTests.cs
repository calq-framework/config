using CalqFramework.Config.Json;

namespace CalqFramework.Config.Tests;

public class PresetGroupTests : IDisposable {
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        Guid.NewGuid()
            .ToString());

    public PresetGroupTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() {
        if (Directory.Exists(_tempDir)) {
            Directory.Delete(_tempDir, true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ReloadAllAsync_PropagatesPresetChange() {
        // Master preset points Theme to "dark"
        string masterJson = """{"Theme":"dark","Region":"us"}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.MasterPreset.default.json"), masterJson);

        // UiConfig for "dark" preset
        string uiJson = """{"Title":"Dark UI","FontSize":14,"DarkMode":true}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.UiConfig.dark.json"), uiJson);

        // RegionConfig for "us" preset
        string regionJson = """{"Language":"en","Currency":"USD"}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.RegionConfig.us.json"), regionJson);

        JsonConfigurationRegistry<MasterPreset> registry = new(_tempDir);
        UiConfig ui = await registry.GetAsync<UiConfig>();
        RegionConfig region = await registry.GetAsync<RegionConfig>();

        await registry.ReloadAllAsync();

        Assert.Equal("Dark UI", ui.Title);
        Assert.True(ui.DarkMode);
        Assert.Equal("en", region.Language);
        Assert.Equal("USD", region.Currency);
    }

    [Fact]
    public async Task ReloadAllAsync_SwitchesPresetWhenMasterChanges() {
        // Initial master: Theme = "light"
        string masterJson = """{"Theme":"light","Region":"default"}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.MasterPreset.default.json"), masterJson);

        string lightUi = """{"Title":"Light UI","FontSize":12,"DarkMode":false}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.UiConfig.light.json"), lightUi);

        string darkUi = """{"Title":"Dark UI","FontSize":14,"DarkMode":true}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.UiConfig.dark.json"), darkUi);

        JsonConfigurationRegistry<MasterPreset> registry = new(_tempDir);
        UiConfig ui = await registry.GetAsync<UiConfig>();
        await registry.ReloadAllAsync();

        Assert.Equal("Light UI", ui.Title);
        Assert.False(ui.DarkMode);

        // Now switch master to dark
        masterJson = """{"Theme":"dark","Region":"default"}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.MasterPreset.default.json"), masterJson);

        await registry.ReloadAllAsync();

        // Same reference, but now populated with dark preset
        Assert.Equal("Dark UI", ui.Title);
        Assert.True(ui.DarkMode);
    }

    [Fact]
    public void PresetGroupAttribute_CachedOnType() {
        JsonConfigurationItem<UiConfig> item1 = new(_tempDir, "default");
        JsonConfigurationItem<UiConfig> item2 = new(_tempDir, "default");

        Assert.Equal("Theme", item1.PresetGroup);
        Assert.Equal("Theme", item2.PresetGroup);
    }

    [Fact]
    public void NoPresetGroupAttribute_ReturnsNull() {
        JsonConfigurationItem<SimpleConfig> item = new(_tempDir, "default");
        Assert.Null(item.PresetGroup);
    }
}
