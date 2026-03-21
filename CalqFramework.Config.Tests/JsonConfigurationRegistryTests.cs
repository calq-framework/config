using CalqFramework.Config.Json;

namespace CalqFramework.Config.Tests;

public class JsonConfigurationRegistryTests : IDisposable {
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public JsonConfigurationRegistryTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task GetAsync_ReturnsSameInstance() {
        var registry = new JsonConfigurationRegistry(_tempDir);
        var first = await registry.GetAsync<SimpleConfig>();
        var second = await registry.GetAsync<SimpleConfig>();

        Assert.Same(first, second);
    }

    [Fact]
    public async Task LoadAsync_ReturnsSameAsGetAsync() {
        var registry = new JsonConfigurationRegistry(_tempDir);
        var loaded = await registry.LoadAsync<SimpleConfig>();
        var got = await registry.GetAsync<SimpleConfig>();

        Assert.Same(loaded, got);
    }

    [Fact]
    public async Task ReloadAsync_RefreshesItem() {
        var json = """{"Name":"v1","Value":1}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.SimpleConfig.default.json"), json);

        var registry = new JsonConfigurationRegistry(_tempDir);
        var config = await registry.GetAsync<SimpleConfig>();
        Assert.Equal("v1", config.Name);

        json = """{"Name":"v2","Value":2}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.SimpleConfig.default.json"), json);

        await registry.ReloadAsync<SimpleConfig>();
        Assert.Equal("v2", config.Name);
        Assert.Equal(2, config.Value);
    }

    [Fact]
    public void Constructor_CreatesDirectory() {
        var dir = Path.Combine(_tempDir, "subdir", "nested");
        _ = new JsonConfigurationRegistry(dir);

        Assert.True(Directory.Exists(dir));
    }

    [Fact]
    public async Task AvailablePresetGroups_ReturnsDistinctGroups() {
        var registry = new JsonConfigurationRegistry<MasterPreset>(_tempDir);
        await registry.GetAsync<UiConfig>();
        await registry.GetAsync<RegionConfig>();

        var groups = registry.AvailablePresetGroups.OrderBy(x => x).ToList();
        Assert.Contains("Theme", groups);
        Assert.Contains("Region", groups);
    }

    [Fact]
    public async Task ConcurrentReloads_ThreadSafe() {
        var json = """{"Name":"concurrent","Value":1}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.SimpleConfig.default.json"), json);

        var registry = new JsonConfigurationRegistry(_tempDir);
        var config = await registry.GetAsync<SimpleConfig>();

        var tasks = Enumerable.Range(0, 10).Select(_ => registry.ReloadAsync<SimpleConfig>());
        await Task.WhenAll(tasks);

        Assert.Equal("concurrent", config.Name);
    }
}
