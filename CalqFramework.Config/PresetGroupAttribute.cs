namespace CalqFramework.Config;

/// <summary>
///     Specifies the preset group property name on the master preset POCO
///     whose value determines which preset file to load for this configuration type.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PresetGroupAttribute(string propertyName) : Attribute {
    public string PropertyName { get; } = propertyName;
}
