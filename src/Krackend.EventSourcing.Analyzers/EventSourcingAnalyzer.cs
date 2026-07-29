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
    private const string StateSchemaAttributeName = "Krackend.EventSourcing.Contracts.StateSchemaAttribute";
    private const string EventReducerInterfaceName = "Krackend.EventSourcing.Core.IEventReducer`2";
    private const string InitialStateFactoryInterfaceName = "Krackend.EventSourcing.Core.IInitialStateFactory`1";

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

    private static readonly DiagnosticDescriptor DuplicateStateSchemaRule = new(
        "KES0003",
        "State schema is duplicated",
        "State schema '{0}' version '{1}' is also declared by '{2}'",
        "Krackend.EventSourcing.Schema",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    private static readonly DiagnosticDescriptor ReducerStateMissingSchemaRule = new(
        "KES0004",
        "Reducer state is missing StateSchema",
        "Reducer '{0}' handles state '{1}', but that state does not declare StateSchema",
        "Krackend.EventSourcing.Schema",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InitialStateFactoryStateMissingSchemaRule = new(
        "KES0005",
        "Initial state factory state is missing StateSchema",
        "Initial state factory '{0}' creates state '{1}', but that state does not declare StateSchema",
        "Krackend.EventSourcing.Schema",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(
            DuplicateEventSchemaRule,
            ReducerEventMissingSchemaRule,
            DuplicateStateSchemaRule,
            ReducerStateMissingSchemaRule,
            InitialStateFactoryStateMissingSchemaRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var eventSchemaAttribute = compilationContext.Compilation.GetTypeByMetadataName(EventSchemaAttributeName);
            var stateSchemaAttribute = compilationContext.Compilation.GetTypeByMetadataName(StateSchemaAttributeName);
            var reducerInterface = compilationContext.Compilation.GetTypeByMetadataName(EventReducerInterfaceName);
            var initialStateFactoryInterface = compilationContext.Compilation.GetTypeByMetadataName(InitialStateFactoryInterfaceName);

            if (eventSchemaAttribute is null && stateSchemaAttribute is null)
                return;

            var eventSchemas = new ConcurrentDictionary<string, ConcurrentBag<INamedTypeSymbol>>();
            var stateSchemas = new ConcurrentDictionary<string, ConcurrentBag<INamedTypeSymbol>>();

            compilationContext.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(
                    symbolContext,
                    eventSchemaAttribute,
                    stateSchemaAttribute,
                    reducerInterface,
                    initialStateFactoryInterface,
                    eventSchemas,
                    stateSchemas),
                SymbolKind.NamedType);

            compilationContext.RegisterCompilationEndAction(
                compilationEndContext =>
                {
                    ReportDuplicateSchemas(compilationEndContext, eventSchemas, DuplicateEventSchemaRule);
                    ReportDuplicateSchemas(compilationEndContext, stateSchemas, DuplicateStateSchemaRule);
                });
        });
    }

    private static void AnalyzeNamedType(
        SymbolAnalysisContext context,
        INamedTypeSymbol? eventSchemaAttribute,
        INamedTypeSymbol? stateSchemaAttribute,
        INamedTypeSymbol? reducerInterface,
        INamedTypeSymbol? initialStateFactoryInterface,
        ConcurrentDictionary<string, ConcurrentBag<INamedTypeSymbol>> eventSchemas,
        ConcurrentDictionary<string, ConcurrentBag<INamedTypeSymbol>> stateSchemas)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        var eventSchema = eventSchemaAttribute is null ? null : GetSchema(type, eventSchemaAttribute);

        if (eventSchema is not null)
        {
            var key = $"{eventSchema.Value.Name}|{eventSchema.Value.Version}";
            eventSchemas.GetOrAdd(key, _ => new ConcurrentBag<INamedTypeSymbol>()).Add(type);
        }

        var stateSchema = stateSchemaAttribute is null ? null : GetSchema(type, stateSchemaAttribute);

        if (stateSchema is not null)
        {
            var key = $"{stateSchema.Value.Name}|{stateSchema.Value.Version}";
            stateSchemas.GetOrAdd(key, _ => new ConcurrentBag<INamedTypeSymbol>()).Add(type);
        }

        if (reducerInterface is not null && eventSchemaAttribute is not null)
            AnalyzeReducerSchemas(context, type, reducerInterface, eventSchemaAttribute, stateSchemaAttribute);

        if (initialStateFactoryInterface is not null && stateSchemaAttribute is not null)
            AnalyzeInitialStateFactorySchema(context, type, initialStateFactoryInterface, stateSchemaAttribute);
    }

    private static void AnalyzeReducerSchemas(
        SymbolAnalysisContext context,
        INamedTypeSymbol reducerType,
        INamedTypeSymbol reducerInterface,
        INamedTypeSymbol eventSchemaAttribute,
        INamedTypeSymbol? stateSchemaAttribute)
    {
        foreach (var implementedInterface in reducerType.AllInterfaces)
        {
            if (!SymbolEqualityComparer.Default.Equals(implementedInterface.OriginalDefinition, reducerInterface))
                continue;

            var stateType = implementedInterface.TypeArguments[0] as INamedTypeSymbol;
            var eventType = implementedInterface.TypeArguments[1] as INamedTypeSymbol;

            if (stateType is not null &&
                stateSchemaAttribute is not null &&
                GetSchema(stateType, stateSchemaAttribute) is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ReducerStateMissingSchemaRule,
                    reducerType.Locations.FirstOrDefault(),
                    reducerType.Name,
                    stateType.Name));
            }

            if (eventType is not null && GetSchema(eventType, eventSchemaAttribute) is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ReducerEventMissingSchemaRule,
                    reducerType.Locations.FirstOrDefault(),
                    reducerType.Name,
                    eventType.Name));
            }
        }
    }

    private static void AnalyzeInitialStateFactorySchema(
        SymbolAnalysisContext context,
        INamedTypeSymbol factoryType,
        INamedTypeSymbol initialStateFactoryInterface,
        INamedTypeSymbol stateSchemaAttribute)
    {
        foreach (var implementedInterface in factoryType.AllInterfaces)
        {
            if (!SymbolEqualityComparer.Default.Equals(implementedInterface.OriginalDefinition, initialStateFactoryInterface))
                continue;

            var stateType = implementedInterface.TypeArguments[0] as INamedTypeSymbol;

            if (stateType is null || GetSchema(stateType, stateSchemaAttribute) is not null)
                continue;

            context.ReportDiagnostic(Diagnostic.Create(
                InitialStateFactoryStateMissingSchemaRule,
                factoryType.Locations.FirstOrDefault(),
                factoryType.Name,
                stateType.Name));
        }
    }

    private static void ReportDuplicateSchemas(
        CompilationAnalysisContext context,
        ConcurrentDictionary<string, ConcurrentBag<INamedTypeSymbol>> schemas,
        DiagnosticDescriptor rule)
    {
        foreach (var pair in schemas)
        {
            var types = GetDistinctTypes(pair.Value);

            if (types.Length <= 1)
                continue;

            var parts = pair.Key.Split('|');
            var otherTypes = string.Join(", ", types.Skip(1).Select(type => type.Name));

            context.ReportDiagnostic(Diagnostic.Create(
                rule,
                types[0].Locations.FirstOrDefault(),
                parts[0],
                parts[1],
                otherTypes));
        }
    }

    private static Schema? GetSchema(INamedTypeSymbol type, INamedTypeSymbol schemaAttribute)
    {
        foreach (var attribute in type.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, schemaAttribute))
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

            return new Schema(schemaName!, normalizedVersion!);
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

    private readonly struct Schema
    {
        public Schema(string name, string version)
        {
            Name = name;
            Version = version;
        }

        public string Name { get; }

        public string Version { get; }
    }
}
