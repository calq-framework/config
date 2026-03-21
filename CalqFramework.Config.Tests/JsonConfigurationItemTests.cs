using System.Text.Json;
using CalqFramework.Config.Json;

namespace CalqFramework.Config.Tests;

public class JsonConfigurationItemTests : IDisposable {
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        Guid.NewGuid()
            .ToString());

    public JsonConfigurationItemTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() {
        if (Directory.Exists(_tempDir)) {
            Directory.Delete(_tempDir, true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ReloadAsync_PopulatesItemFromJsonFile() {
        string json = """{"Name":"hello","Value":42}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.SimpleConfig.default.json"), json);

        JsonConfigurationItem<SimpleConfig> item = new(_tempDir, "default");
        await item.ReloadAsync();

        Assert.Equal("hello", item.Item.Name);
        Assert.Equal(42, item.Item.Value);
    }

    [Fact]
    public async Task ReloadAsync_MissingFile_IsNoOp() {
        JsonConfigurationItem<SimpleConfig> item = new(_tempDir, "missing");
        await item.ReloadAsync();

        Assert.Equal("", item.Item.Name);
        Assert.Equal(0, item.Item.Value);
    }

    [Fact]
    public async Task ReloadAsync_FiresOnReloadedEvent() {
        string json = """{"Name":"test"}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.SimpleConfig.default.json"), json);

        JsonConfigurationItem<SimpleConfig> item = new(_tempDir, "default");
        bool fired = false;
        item.OnReloaded += () => fired = true;
        await item.ReloadAsync();

        Assert.True(fired);
    }

    [Fact]
    public async Task ReloadAsync_PopulatesFields() {
        string json = """{"Name":"fieldTest","Count":7}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.FieldConfig.default.json"), json);

        JsonConfigurationItem<FieldConfig> item = new(_tempDir, "default");
        await item.ReloadAsync();

        Assert.Equal("fieldTest", item.Item.Name);
        Assert.Equal(7, item.Item.Count);
    }

    [Fact]
    public async Task AvailablePresets_DiscoversByGlob() {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.SimpleConfig.default.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.SimpleConfig.dark.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.SimpleConfig.light.json"), "{}");

        JsonConfigurationItem<SimpleConfig> item = new(_tempDir, "default");
        var presets = item.AvailablePresets.OrderBy(x => x)
            .ToList();

        Assert.Equal(3, presets.Count);
        Assert.Contains("dark", presets);
        Assert.Contains("default", presets);
        Assert.Contains("light", presets);
    }

    [Fact]
    public async Task ReloadAsync_CollectionReplaceBehavior() {
        JsonConfigurationItem<CollectionConfig> item = new(_tempDir, "default");
        item.Item.Tags.Add("existing");
        item.Item.Scores["old"] = 1;
        item.Item.Roles.Add("admin");

        string json = """{"Tags":["new"],"Scores":{"fresh":99},"Roles":["user"]}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.CollectionConfig.default.json"), json);
        await item.ReloadAsync();

        Assert.Single(item.Item.Tags);
        Assert.Contains("new", item.Item.Tags);
        Assert.DoesNotContain("existing", item.Item.Tags);
        Assert.Single(item.Item.Scores);
        Assert.Equal(99, item.Item.Scores["fresh"]);
        Assert.False(item.Item.Scores.ContainsKey("old"));
        Assert.Single(item.Item.Roles);
        Assert.Contains("user", item.Item.Roles);
        Assert.DoesNotContain("admin", item.Item.Roles);
    }

    [Fact]
    public async Task ReferenceIdentity_SameInstanceAfterReload() {
        string json = """{"Name":"v1"}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.SimpleConfig.default.json"), json);

        JsonConfigurationItem<SimpleConfig> item = new(_tempDir, "default");
        SimpleConfig reference = item.Item;
        await item.ReloadAsync();

        Assert.Same(reference, item.Item);
        Assert.Equal("v1", reference.Name);
    }

    [Fact]
    public async Task SaveAsync_PersistsListWithItemRemoved() {
        JsonConfigurationItem<CollectionConfig> item = new(_tempDir, "default");
        item.Item.Tags.AddRange(["alpha", "beta", "gamma"]);

        item.Item.Tags.Remove("beta");
        await item.SaveAsync();

        string json = await File.ReadAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.CollectionConfig.default.json"));
        CollectionConfig? reloaded = JsonSerializer.Deserialize<CollectionConfig>(json);

        Assert.Equal(2, reloaded!.Tags.Count);
        Assert.Contains("alpha", reloaded.Tags);
        Assert.Contains("gamma", reloaded.Tags);
        Assert.DoesNotContain("beta", reloaded.Tags);
    }

    [Fact]
    public async Task ReloadAsync_ExternalRemovalReflected() {
        // Start with 3 tags
        string json = """{"Tags":["alpha","beta","gamma"]}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.CollectionConfig.default.json"), json);

        JsonConfigurationItem<CollectionConfig> item = new(_tempDir, "default");
        await item.ReloadAsync();
        Assert.Equal(3, item.Item.Tags.Count);

        // External edit removes "beta"
        json = """{"Tags":["alpha","gamma"]}""";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "CalqFramework.Config.Tests.CollectionConfig.default.json"), json);

        await item.ReloadAsync();

        Assert.Equal(2, item.Item.Tags.Count);
        Assert.Contains("alpha", item.Item.Tags);
        Assert.Contains("gamma", item.Item.Tags);
        Assert.DoesNotContain("beta", item.Item.Tags);
    }
}
