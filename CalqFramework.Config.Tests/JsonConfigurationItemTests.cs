using CalqFramework.Config.Json;

namespace CalqFramework.Config.Tests;

public class JsonConfigurationItemTests : IDisposable {
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public JsonConfigurationItemTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task ReloadAsync_PopulatesItemFromJsonFile() {
        var json = """{"Name":"hello","Value":42}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.SimpleConfig.default.json"), json);

        var item = new JsonConfigurationItem<SimpleConfig>(_tempDir, "default");
        await item.ReloadAsync();

        Assert.Equal("hello", item.Item.Name);
        Assert.Equal(42, item.Item.Value);
    }

    [Fact]
    public async Task ReloadAsync_MissingFile_IsNoOp() {
        var item = new JsonConfigurationItem<SimpleConfig>(_tempDir, "missing");
        await item.ReloadAsync();

        Assert.Equal("", item.Item.Name);
        Assert.Equal(0, item.Item.Value);
    }

    [Fact]
    public async Task ReloadAsync_FiresOnReloadedEvent() {
        var json = """{"Name":"test"}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.SimpleConfig.default.json"), json);

        var item = new JsonConfigurationItem<SimpleConfig>(_tempDir, "default");
        var fired = false;
        item.OnReloaded += () => fired = true;
        await item.ReloadAsync();

        Assert.True(fired);
    }

    [Fact]
    public async Task ReloadAsync_PopulatesFields() {
        var json = """{"Name":"fieldTest","Count":7}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.FieldConfig.default.json"), json);

        var item = new JsonConfigurationItem<FieldConfig>(_tempDir, "default");
        await item.ReloadAsync();

        Assert.Equal("fieldTest", item.Item.Name);
        Assert.Equal(7, item.Item.Count);
    }

    [Fact]
    public async Task AvailablePresets_DiscoversByGlob() {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.SimpleConfig.default.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.SimpleConfig.dark.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.SimpleConfig.light.json"), "{}");

        var item = new JsonConfigurationItem<SimpleConfig>(_tempDir, "default");
        var presets = item.AvailablePresets.OrderBy(x => x).ToList();

        Assert.Equal(3, presets.Count);
        Assert.Contains("dark", presets);
        Assert.Contains("default", presets);
        Assert.Contains("light", presets);
    }

    [Fact]
    public async Task ReloadAsync_CollectionAppendBehavior() {
        var item = new JsonConfigurationItem<CollectionConfig>(_tempDir, "default");
        item.Item.Tags.Add("existing");
        item.Item.Scores["old"] = 1;
        item.Item.Roles.Add("admin");

        var json = """{"Tags":["new"],"Scores":{"fresh":99},"Roles":["user"]}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.CollectionConfig.default.json"), json);
        await item.ReloadAsync();

        Assert.Contains("existing", item.Item.Tags);
        Assert.Contains("new", item.Item.Tags);
        Assert.Equal(1, item.Item.Scores["old"]);
        Assert.Equal(99, item.Item.Scores["fresh"]);
        Assert.Contains("admin", item.Item.Roles);
        Assert.Contains("user", item.Item.Roles);
    }

    [Fact]
    public async Task ReferenceIdentity_SameInstanceAfterReload() {
        var json = """{"Name":"v1"}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.SimpleConfig.default.json"), json);

        var item = new JsonConfigurationItem<SimpleConfig>(_tempDir, "default");
        var reference = item.Item;
        await item.ReloadAsync();

        Assert.Same(reference, item.Item);
        Assert.Equal("v1", reference.Name);
    }
}
