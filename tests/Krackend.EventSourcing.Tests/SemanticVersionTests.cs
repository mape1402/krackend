using Krackend.EventSourcing.Contracts;
using Krackend.EventSourcing.Diagnostics;
using Krackend.EventSourcing.Registry;
using System.Text.Json;

namespace Krackend.EventSourcing.Tests;

public sealed class SemanticVersionTests
{
    [Fact]
    public void Constructor_sets_properties()
    {
        var version = new SemanticVersion(1, 2, 3);

        Assert.Equal(1, version.Major);
        Assert.Equal(2, version.Minor);
        Assert.Equal(3, version.Patch);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, -1)]
    public void Constructor_rejects_negative_components(int major, int minor, int patch)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SemanticVersion(major, minor, patch));
    }

    [Theory]
    [InlineData("1.2.3")]
    [InlineData("10.20.30")]
    public void TryParse_parses_valid_versions(string input)
    {
        var result = SemanticVersion.TryParse(input, out var version);

        Assert.True(result);
        Assert.NotEqual(default, version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    public void TryParse_rejects_invalid_versions(string input)
    {
        Assert.False(SemanticVersion.TryParse(input, out _));
    }

    [Fact]
    public void Parse_throws_for_invalid_version()
    {
        Assert.Throws<FormatException>(() => SemanticVersion.Parse("abc"));
    }

    [Fact]
    public void Default_is_1_0_0()
    {
        Assert.Equal(new SemanticVersion(1, 0, 0), SemanticVersion.Default);
    }

    [Fact]
    public void Operators_compare_versions()
    {
        var first = new SemanticVersion(1, 0, 0);
        var second = new SemanticVersion(1, 0, 1);

        Assert.True(first < second);
        Assert.True(second > first);
        Assert.True(first <= second);
        Assert.True(second >= first);
        Assert.True(first == new SemanticVersion(1, 0, 0));
        Assert.True(first != second);
    }

    [Fact]
    public void Implicit_conversions_use_string_representation()
    {
        SemanticVersion version = "2.3.4";
        string versionString = version;

        Assert.Equal("2.3.4", versionString);
    }

    [Fact]
    public void Json_converter_uses_string_representation()
    {
        var options = new JsonSerializerOptions
        {
            Converters = { new SemanticVersionJsonConverter() }
        };

        var json = JsonSerializer.Serialize(SemanticVersion.Default, options);
        var version = JsonSerializer.Deserialize<SemanticVersion>("\"2.3.4\"", options);

        Assert.Equal("\"1.0.0\"", json);
        Assert.Equal(new SemanticVersion(2, 3, 4), version);
    }

    [Fact]
    public void Event_type_registry_uses_event_schema_attribute()
    {
        var registry = new EventTypeRegistry()
            .Register<VersionedEvent>();

        var registration = registry.GetRegistration(typeof(VersionedEvent));

        Assert.Equal(new SemanticVersion(2, 3, 4), registration.EventSchemaVersion);
        Assert.Equal(typeof(VersionedEvent), registry.Resolve("VersionedEvent", "2.3.4"));
    }

    [Fact]
    public void Event_type_registry_resolves_each_schema_for_same_event_type()
    {
        var registry = new EventTypeRegistry()
            .Register<LegacyBalanceMoved>("BalanceMoved", "1.0.0")
            .Register<BalanceMoved>("BalanceMoved", "1.1.0");

        Assert.Equal(typeof(LegacyBalanceMoved), registry.Resolve("BalanceMoved", "1.0.0"));
        Assert.Equal(typeof(BalanceMoved), registry.Resolve("BalanceMoved", "1.1.0"));
    }

    [Fact]
    public void Event_type_registry_requires_schema_attribute_or_explicit_name()
    {
        var registry = new EventTypeRegistry();

        var exception = Assert.Throws<EventSchemaMissingException>(() => registry.Register<UnattributedEvent>());

        Assert.Equal(typeof(UnattributedEvent), exception.EventClrType);
    }

    [Fact]
    public void Event_type_registry_rejects_duplicate_schema_for_different_types()
    {
        var registry = new EventTypeRegistry()
            .Register<BalanceMoved>("BalanceMoved", "1.1.0");

        var exception = Assert.Throws<DuplicateEventSchemaException>(() => registry.Register<DuplicateBalanceMoved>("BalanceMoved", "1.1.0"));

        Assert.Contains("already registered", exception.Message);
        Assert.Equal("BalanceMoved", exception.EventType);
        Assert.Equal(new SemanticVersion(1, 1, 0), exception.EventSchemaVersion);
        Assert.Equal(typeof(BalanceMoved), exception.RegisteredClrType);
        Assert.Equal(typeof(DuplicateBalanceMoved), exception.DuplicateClrType);
    }

    [Fact]
    public void Event_type_registry_throws_typed_exception_when_clr_type_is_not_registered()
    {
        var registry = new EventTypeRegistry();

        var exception = Assert.Throws<EventTypeNotRegisteredException>(() => registry.GetRegistration(typeof(BalanceMoved)));

        Assert.Equal(typeof(BalanceMoved), exception.EventClrType);
    }

    [Fact]
    public void Event_type_registry_throws_typed_exception_when_schema_is_not_registered()
    {
        var registry = new EventTypeRegistry();

        var exception = Assert.Throws<EventTypeNotRegisteredException>(() => registry.Resolve("BalanceMoved", "9.9.9"));

        Assert.Equal("BalanceMoved", exception.EventType);
        Assert.Equal(new SemanticVersion(9, 9, 9), exception.EventSchemaVersion);
    }

    [Fact]
    public void State_schema_attribute_parses_schema_version()
    {
        var attribute = new StateSchemaAttribute("CustomerState", "2.1.0");

        Assert.Equal("CustomerState", attribute.Name);
        Assert.Equal(new SemanticVersion(2, 1, 0), attribute.Version);
    }

    [Fact]
    public void State_schema_attribute_uses_default_schema_version()
    {
        var attribute = new StateSchemaAttribute("CustomerState");

        Assert.Equal(SemanticVersion.Default, attribute.Version);
    }

    [EventSchema("VersionedEvent", "2.3.4")]
    private sealed record VersionedEvent;

    private sealed record LegacyBalanceMoved;

    private sealed record BalanceMoved;

    private sealed record DuplicateBalanceMoved;

    private sealed record UnattributedEvent;
}
