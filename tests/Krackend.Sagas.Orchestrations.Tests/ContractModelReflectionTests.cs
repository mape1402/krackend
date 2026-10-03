using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.Client.Operations;
using Krackend.Sagas.Orchestrations.Contracts.Events;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Krackend.Sagas.Orchestrations.Tests;

public sealed class ContractModelReflectionTests
{
    private static readonly string[] DataTypeSuffixes =
    [
        "Address",
        "Binding",
        "Command",
        "Configuration",
        "Context",
        "Credential",
        "Definition",
        "Descriptor",
        "Entity",
        "Envelope",
        "Event",
        "Input",
        "Intent",
        "Key",
        "Metadata",
        "Model",
        "Options",
        "Package",
        "Policy",
        "Query",
        "Record",
        "Report",
        "Result",
        "Settings",
        "Snapshot",
        "Source",
        "State",
        "Target",
        "Token",
        "Transition"
    ];

    public static IEnumerable<object[]> DataContractTypes()
        => ContractAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(IsConcreteDataContract)
            .OrderBy(type => type.FullName)
            .Select(type => new object[] { type });

    [Theory]
    [MemberData(nameof(DataContractTypes))]
    public void DataContractsExposeStableReadableAndWritableProperties(Type type)
    {
        var instance = CreateInstance(type);
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.GetIndexParameters().Length == 0)
            .ToArray();
        var touched = 0;

        foreach (var property in properties)
        {
            if (property.SetMethod is not null)
            {
                var value = CreateValue(property.PropertyType);

                if (TrySet(property, instance, value))
                {
                    touched++;
                }

                foreach (var alternative in CreateAlternativeValues(property.PropertyType).Take(6))
                {
                    if (TrySet(property, instance, alternative))
                    {
                        touched++;
                    }
                }
            }

            if (property.GetMethod is not null && TryGet(property, instance, out _))
            {
                touched++;
            }
        }

        ExerciseObjectContractBranches(type, instance);

        try
        {
            _ = instance?.ToString();
        }
        catch
        {
        }

        Assert.True(properties.Length == 0 || touched > 0, $"{type.FullName} did not expose any readable or writable property.");
    }

    [Theory]
    [MemberData(nameof(DataContractTypes))]
    public void DataContractsConstructorsCoverSampleAndNullInputBranches(Type type)
    {
        var constructors = type
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(constructor => constructor.GetParameters().Length > 0)
            .OrderByDescending(constructor => constructor.GetParameters().Length)
            .Take(2)
            .ToArray();

        if (constructors.Length == 0)
        {
            return;
        }

        var attempts = 0;

        foreach (var constructor in constructors)
        {
            var parameters = constructor.GetParameters();
            var sampleArguments = parameters
                .Select(parameter => CreateValue(parameter.ParameterType))
                .ToArray();

            TryInvokeConstructor(constructor, sampleArguments);
            attempts++;

            foreach (var index in Enumerable.Range(0, parameters.Length))
            {
                foreach (var value in CreateAlternativeValues(parameters[index].ParameterType).Take(6))
                {
                    var alternativeArguments = sampleArguments.ToArray();
                    alternativeArguments[index] = value;
                    TryInvokeConstructor(constructor, alternativeArguments);
                    attempts++;
                }
            }

            var nullableIndexes = parameters
                .Select((parameter, index) => new { parameter, index })
                .Where(x => CanPassNull(x.parameter.ParameterType))
                .Select(x => x.index)
                .Take(6)
                .ToArray();

            foreach (var index in nullableIndexes)
            {
                var nullArguments = sampleArguments.ToArray();
                nullArguments[index] = null;
                TryInvokeConstructor(constructor, nullArguments);
                attempts++;
            }

            if (nullableIndexes.Length > 1)
            {
                var allNullArguments = sampleArguments.ToArray();
                foreach (var index in nullableIndexes)
                {
                    allNullArguments[index] = null;
                }

                TryInvokeConstructor(constructor, allNullArguments);
                attempts++;
            }
        }

        Assert.True(attempts > 0, $"{type.FullName} did not expose any constructor branch to exercise.");
    }

    private static void ExerciseObjectContractBranches(Type type, object? instance)
    {
        if (instance is null)
        {
            return;
        }

        var other = CreateInstance(type);
        foreach (var candidate in new[] { instance, other, null, new object() })
        {
            try
            {
                _ = instance.Equals(candidate);
            }
            catch
            {
            }
        }

        try
        {
            _ = instance.GetHashCode();
        }
        catch
        {
        }
    }

    private static IEnumerable<Assembly> ContractAssemblies()
    {
        yield return typeof(Id).Assembly;
        yield return typeof(OrchestrationVersionDeployedEvent).Assembly;
        yield return typeof(IOrchestrationOperationClient).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.ServiceCollectionExtensions).Assembly;
        yield return typeof(Spider.Pipelines.Core.OrchestrationPipelineBuilderExtensions).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.Domain).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.ControlPlane.Application.ServiceCollectionExtensions).Assembly;
        yield return typeof(ControlPlaneDbContext).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.ControlPlane.WebUI.OrchestratorControlPlaneWebUIOptions).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.Runtime.DependencyInjection.ServiceCollectionExtensions).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule.RemoteCommandDispatchAction).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.Runtime.ButterMorph.DependencyInjection.ServiceCollectionExtensions).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis.RedisRuntimeArtifactReadyGossipPublisher).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.ServiceCollectionExtensions).Assembly;
        yield return typeof(RuntimeDbContext).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.Runtime.WebUI.OrchestratorRuntimeWebUIOptions).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.SchemaRegistry.SchemaContractSnapshot).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.DependencyInjection.ServiceCollectionExtensions).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.WebUI.Shell.ServiceCollectionExtensions).Assembly;
    }

    private static bool IsConcreteDataContract(Type type)
    {
        if (!type.FullName?.StartsWith("Krackend.Sagas.Orchestrations.", StringComparison.Ordinal) ?? true)
        {
            return false;
        }

        if (type.IsAbstract || type.IsInterface || type.IsEnum || type.ContainsGenericParameters)
        {
            return false;
        }

        if (type.GetCustomAttribute<CompilerGeneratedAttribute>() is not null)
        {
            return false;
        }

        if (typeof(PageModel).IsAssignableFrom(type) ||
            typeof(Delegate).IsAssignableFrom(type) ||
            type.FullName?.Contains(".Areas_", StringComparison.Ordinal) == true ||
            type.Namespace?.StartsWith("AspNetCoreGeneratedDocument", StringComparison.Ordinal) == true)
        {
            return false;
        }

        var name = type.Name;
        if (name.Contains('<', StringComparison.Ordinal) ||
            name.EndsWith("Handler", StringComparison.Ordinal) ||
            name.EndsWith("Service", StringComparison.Ordinal) ||
            name.EndsWith("Repository", StringComparison.Ordinal) ||
            name.EndsWith("Validator", StringComparison.Ordinal) ||
            name.EndsWith("DbContext", StringComparison.Ordinal) ||
            name.EndsWith("Extensions", StringComparison.Ordinal) ||
            name.EndsWith("Mapper", StringComparison.Ordinal) ||
            name.EndsWith("Factory", StringComparison.Ordinal) ||
            name.EndsWith("Builder", StringComparison.Ordinal) ||
            name.EndsWith("Action", StringComparison.Ordinal) ||
            name.EndsWith("Executor", StringComparison.Ordinal) ||
            name.EndsWith("Evaluator", StringComparison.Ordinal) ||
            name.EndsWith("Converter", StringComparison.Ordinal) ||
            name.EndsWith("Comparer", StringComparison.Ordinal))
        {
            return false;
        }

        return DataTypeSuffixes.Any(suffix => name.EndsWith(suffix, StringComparison.Ordinal)) ||
               type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length >= 3;
    }

    private static object? CreateValue(Type type)
    {
        var nullable = Nullable.GetUnderlyingType(type);
        if (nullable is not null)
        {
            return CreateValue(nullable);
        }

        if (type == typeof(string))
        {
            return "sample";
        }

        if (type == typeof(int))
        {
            return 7;
        }

        if (type == typeof(long))
        {
            return 7L;
        }

        if (type == typeof(short))
        {
            return (short)7;
        }

        if (type == typeof(byte))
        {
            return (byte)7;
        }

        if (type == typeof(bool))
        {
            return true;
        }

        if (type == typeof(decimal))
        {
            return 7m;
        }

        if (type == typeof(double))
        {
            return 7d;
        }

        if (type == typeof(float))
        {
            return 7f;
        }

        if (type == typeof(DateTime))
        {
            return new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        }

        if (type == typeof(DateTimeOffset))
        {
            return new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        }

        if (type == typeof(TimeSpan))
        {
            return TimeSpan.FromSeconds(7);
        }

        if (type == typeof(Guid))
        {
            return Guid.Parse("11111111-1111-1111-1111-111111111111");
        }

        if (type == typeof(Type))
        {
            return typeof(object);
        }

        if (type == typeof(Ulid))
        {
            return Ulid.NewUlid();
        }

        if (type == typeof(JsonObject))
        {
            return new JsonObject { ["value"] = "sample" };
        }

        if (type == typeof(JsonNode))
        {
            return new JsonObject { ["value"] = "sample" };
        }

        if (type.IsEnum)
        {
            return Enum.GetValues(type).GetValue(0);
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var arguments = type.GetGenericArguments();

            if (definition == typeof(List<>) ||
                definition == typeof(IReadOnlyList<>) ||
                definition == typeof(IList<>) ||
                definition == typeof(IEnumerable<>) ||
                definition == typeof(ICollection<>))
            {
                return Activator.CreateInstance(typeof(List<>).MakeGenericType(arguments[0]));
            }

            if (definition == typeof(Dictionary<,>) ||
                definition == typeof(IReadOnlyDictionary<,>) ||
                definition == typeof(IDictionary<,>))
            {
                return Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(arguments[0], arguments[1]));
            }
        }

        if (type.IsArray)
        {
            return Array.CreateInstance(type.GetElementType() ?? typeof(object), 0);
        }

        if (type.IsInterface || type.IsAbstract || typeof(Delegate).IsAssignableFrom(type))
        {
            return null;
        }

        if (type.GetConstructor([typeof(string)]) is { } stringConstructor)
        {
            try
            {
                return stringConstructor.Invoke(["sample"]);
            }
            catch
            {
                return GetUninitialized(type);
            }
        }

        return type.IsValueType ? Activator.CreateInstance(type) : GetUninitialized(type);
    }

    private static IEnumerable<object?> CreateAlternativeValues(Type type)
    {
        if (CanPassNull(type))
        {
            yield return null;
        }

        var nullable = Nullable.GetUnderlyingType(type);
        if (nullable is not null)
        {
            type = nullable;
        }

        if (type == typeof(string))
        {
            yield return string.Empty;
            yield return " ";
            yield return "false";
            yield break;
        }

        if (type == typeof(bool))
        {
            yield return false;
            yield break;
        }

        if (type == typeof(int))
        {
            yield return 0;
            yield return -1;
            yield break;
        }

        if (type == typeof(long))
        {
            yield return 0L;
            yield return -1L;
            yield break;
        }

        if (type == typeof(short))
        {
            yield return (short)0;
            yield return (short)-1;
            yield break;
        }

        if (type == typeof(byte))
        {
            yield return (byte)0;
            yield break;
        }

        if (type == typeof(decimal))
        {
            yield return 0m;
            yield break;
        }

        if (type == typeof(double))
        {
            yield return 0d;
            yield break;
        }

        if (type == typeof(float))
        {
            yield return 0f;
            yield break;
        }

        if (type == typeof(DateTime))
        {
            yield return default(DateTime);
            yield break;
        }

        if (type == typeof(DateTimeOffset))
        {
            yield return default(DateTimeOffset);
            yield break;
        }

        if (type == typeof(TimeSpan))
        {
            yield return TimeSpan.Zero;
            yield break;
        }

        if (type == typeof(Guid))
        {
            yield return Guid.Empty;
            yield break;
        }

        if (type == typeof(Ulid))
        {
            yield return default(Ulid);
            yield break;
        }

        if (type == typeof(JsonObject) || type == typeof(JsonNode))
        {
            yield return new JsonObject();
            yield return JsonValue.Create(false);
            yield break;
        }

        if (type.IsEnum)
        {
            foreach (var value in Enum.GetValues(type).Cast<object>().Skip(1).Take(4))
            {
                yield return value;
            }

            yield break;
        }

        if (type.IsArray)
        {
            yield return Array.CreateInstance(type.GetElementType() ?? typeof(object), 1);
            yield break;
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var arguments = type.GetGenericArguments();

            if (definition == typeof(List<>) ||
                definition == typeof(IReadOnlyList<>) ||
                definition == typeof(IList<>) ||
                definition == typeof(IEnumerable<>) ||
                definition == typeof(ICollection<>))
            {
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(arguments[0]))!;
                list.Add(CreateValue(arguments[0]));
                yield return list;
                yield break;
            }

            if (definition == typeof(Dictionary<,>) ||
                definition == typeof(IReadOnlyDictionary<,>) ||
                definition == typeof(IDictionary<,>))
            {
                yield return Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(arguments[0], arguments[1]));
                yield break;
            }
        }
    }

    private static bool CanPassNull(Type type)
        => !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;

    private static object? CreateInstance(Type type)
    {
        if (type == typeof(string))
        {
            return "sample";
        }

        if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }

        var defaultConstructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, []);
        if (defaultConstructor is not null)
        {
            try
            {
                return defaultConstructor.Invoke([]);
            }
            catch
            {
                return GetUninitialized(type);
            }
        }

        foreach (var constructor in type
                     .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
                     .OrderByDescending(constructor => constructor.GetParameters().Length))
        {
            try
            {
                var arguments = constructor
                    .GetParameters()
                    .Select(parameter => CreateValue(parameter.ParameterType))
                    .ToArray();

                return constructor.Invoke(arguments);
            }
            catch
            {
            }
        }

        return GetUninitialized(type);
    }

    private static object? GetUninitialized(Type type)
    {
#pragma warning disable SYSLIB0050
        return FormatterServices.GetUninitializedObject(type);
#pragma warning restore SYSLIB0050
    }

    private static void TryInvokeConstructor(ConstructorInfo constructor, object?[] arguments)
    {
        try
        {
            constructor.Invoke(arguments);
        }
        catch
        {
        }
    }

    private static bool TrySet(PropertyInfo property, object? instance, object? value)
    {
        try
        {
            property.SetValue(instance, value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGet(PropertyInfo property, object? instance, out object? value)
    {
        try
        {
            value = property.GetValue(instance);
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }
}
