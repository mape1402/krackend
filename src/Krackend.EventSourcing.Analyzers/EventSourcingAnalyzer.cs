using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Krackend.EventSourcing.Analyzers;

/// <summary>
/// Reports diagnostics for common Krackend event sourcing configuration mistakes.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EventSourcingAnalyzer : DiagnosticAnalyzer
{
    private const string EventSchemaAttributeName = "Krackend.EventSourcing.Contracts.EventSchemaAttribute";
    private const string EventReducerInterfaceName = "Krackend.EventSourcing.Core.IEventReducer`2";

    private static readonly DiagnosticDescriptor DuplicateEventSchemaRule = new(
        "KES0001",
        "Event schema is duplicated",
        "Event schema '{0}' version '{1}' is also declared by '{2}'",
        "Krackend.EventSourcing.Schema",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    private static readonly DiagnosticDescriptor ReducerEventMissingSchemaRule = new(
        "KES0002",
        "Reducer event is missing EventSchema",
        "Reducer '{0}' handles event '{1}', but that event does not declare EventSchema",
        "Krackend.EventSourcing.Schema",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(DuplicateEventSchemaRule, ReducerEventMissingSchemaRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var eventSchemaAttribute = compilationContext.Compilation.GetTypeByMetadataName(EventSchemaAttributeName);
            var reducerInterface = compilationContext.Compilation.GetTypeByMetadataName(EventReducerInterfaceName);

            if (eventSchemaAttribute is null)
                return;

            var schemas = new ConcurrentDictionary<string, ConcurrentBag<INamedTypeSymbol>>();

            compilationContext.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(
                    symbolContext,
                    eventSchemaAttribute,
                    reducerInterface,
                    schemas),
                SymbolKind.NamedType);

            compilationContext.RegisterCompilationEndAction(
                compilationEndContext => ReportDuplicateSchemas(compilationEndContext, schemas));
        });
    }

    private static void AnalyzeNamedType(
        SymbolAnalysisContext context,
        INamedTypeSymbol eventSchemaAttribute,
        INamedTypeSymbol? reducerInterface,
        ConcurrentDictionary<string, ConcurrentBag<INamedTypeSymbol>> schemas)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        var schema = GetEventSchema(type, eventSchemaAttribute);

        if (schema is not null)
        {
            var key = $"{schema.Value.Name}|{schema.Value.Version}";
            schemas.GetOrAdd(key, _ => new ConcurrentBag<INamedTypeSymbol>()).Add(type);
        }

        if (reducerInterface is not null)
            AnalyzeReducerEventSchema(context, type, reducerInterface, eventSchemaAttribute);
    }

    private static void AnalyzeReducerEventSchema(
        SymbolAnalysisContext context,
        INamedTypeSymbol reducerType,
        INamedTypeSymbol reducerInterface,
        INamedTypeSymbol eventSchemaAttribute)
    {
        foreach (var implementedInterface in reducerType.AllInterfaces)
        {
            if (!SymbolEqualityComparer.Default.Equals(implementedInterface.OriginalDefinition, reducerInterface))
                continue;

            var eventType = implementedInterface.TypeArguments[1] as INamedTypeSymbol;

            if (eventType is null || GetEventSchema(eventType, eventSchemaAttribute) is not null)
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                ReducerEventMissingSchemaRule,
                reducerType.Locations.FirstOrDefault(),
                reducerType.Name,
                eventType.Name));
        }
    }

    private static void ReportDuplicateSchemas(
        CompilationAnalysisContext context,
        ConcurrentDictionary<string, ConcurrentBag<INamedTypeSymbol>> schemas)
    {
        foreach (var pair in schemas)
        {
            var types = GetDistinctTypes(pair.Value);

            if (types.Length <= 1)
                continue;

            var parts = pair.Key.Split('|');
            var otherTypes = string.Join(", ", types.Skip(1).Select(type => type.Name));

            context.ReportDiagnostic(Diagnostic.Create(
                DuplicateEventSchemaRule,
                types[0].Locations.FirstOrDefault(),
                parts[0],
                parts[1],
                otherTypes));
        }
    }

    private static EventSchema? GetEventSchema(INamedTypeSymbol type, INamedTypeSymbol eventSchemaAttribute)
    {
        foreach (var attribute in type.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, eventSchemaAttribute))
                continue;

            var schemaName = attribute.ConstructorArguments.Length > 0
                ? attribute.ConstructorArguments[0].Value as string
                : null;

            if (string.IsNullOrWhiteSpace(schemaName))
                return null;

            var schemaVersion = attribute.ConstructorArguments.Length > 1
                ? attribute.ConstructorArguments[1].Value as string
                : "1.0.0";

            var normalizedVersion = string.IsNullOrWhiteSpace(schemaVersion) ? "1.0.0" : schemaVersion;

            return new EventSchema(schemaName!, normalizedVersion!);
        }

        return null;
    }

    private static INamedTypeSymbol[] GetDistinctTypes(ConcurrentBag<INamedTypeSymbol> types)
    {
        var distinctTypes = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        foreach (var type in types)
        {
            if (!distinctTypes.Any(current => SymbolEqualityComparer.Default.Equals(current, type)))
                distinctTypes.Add(type);
        }

        return distinctTypes.ToArray();
    }

    private readonly struct EventSchema
    {
        public EventSchema(string name, string version)
        {
            Name = name;
            Version = version;
        }

        public string Name { get; }

        public string Version { get; }
    }
}
