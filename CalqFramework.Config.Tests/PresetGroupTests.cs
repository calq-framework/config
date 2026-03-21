using CalqFramework.Config.Json;

namespace CalqFramework.Config.Tests;

public class PresetGroupTests : IDisposable {
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public PresetGroupTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task ReloadAllAsync_PropagatesPresetChange() {
        // Master preset points Theme to "dark"
        var masterJson = """{"Theme":"dark","Region":"us"}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.MasterPreset.default.json"), masterJson);

        // UiConfig for "dark" preset
        var uiJson = """{"Title":"Dark UI","FontSize":14,"DarkMode":true}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.UiConfig.dark.json"), uiJson);

        // RegionConfig for "us" preset
        var regionJson = """{"Language":"en","Currency":"USD"}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.RegionConfig.us.json"), regionJson);

        var registry = new JsonConfigurationRegistry<MasterPreset>(_tempDir);
        var ui = await registry.GetAsync<UiConfig>();
        var region = await registry.GetAsync<RegionConfig>();

        await registry.ReloadAllAsync();

        Assert.Equal("Dark UI", ui.Title);
        Assert.True(ui.DarkMode);
        Assert.Equal("en", region.Language);
        Assert.Equal("USD", region.Currency);
    }

    [Fact]
    public async Task ReloadAllAsync_SwitchesPresetWhenMasterChanges() {
        // Initial master: Theme = "light"
        var masterJson = """{"Theme":"light","Region":"default"}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.MasterPreset.default.json"), masterJson);

        var lightUi = """{"Title":"Light UI","FontSize":12,"DarkMode":false}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.UiConfig.light.json"), lightUi);

        var darkUi = """{"Title":"Dark UI","FontSize":14,"DarkMode":true}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.UiConfig.dark.json"), darkUi);

        var registry = new JsonConfigurationRegistry<MasterPreset>(_tempDir);
        var ui = await registry.GetAsync<UiConfig>();
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
        var item1 = new JsonConfigurationItem<UiConfig>(_tempDir, "default");
        var item2 = new JsonConfigurationItem<UiConfig>(_tempDir, "default");

        Assert.Equal("Theme", item1.PresetGroup);
        Assert.Equal("Theme", item2.PresetGroup);
    }

    [Fact]
    public void NoPresetGroupAttribute_ReturnsNull() {
        var item = new JsonConfigurationItem<SimpleConfig>(_tempDir, "default");
        Assert.Null(item.PresetGroup);
    }
}
