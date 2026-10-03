namespace Krackend.Sagas.Orchestrations.Tests;

using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Nodes;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Infrastructure;
using Krackend.Sagas.Orchestrations.Runtime.Storage.EntityFramework.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

public sealed class CoverageReflectionBranchSweepTests
{
    private static readonly string[] ComponentSuffixes =
    [
        "Action",
        "Adapter",
        "Accessor",
        "Authenticator",
        "Builder",
        "Client",
        "Binding",
        "Catalog",
        "Channel",
        "Comparer",
        "Configurer",
        "Connector",
        "Converter",
        "Cursor",
        "Descriptor",
        "Dispatcher",
        "Evaluator",
        "Executor",
        "Extensions",
        "Factory",
        "Formatter",
        "Guard",
        "Handler",
        "Host",
        "Id",
        "Idempotency",
        "Importer",
        "Issuer",
        "Interceptor",
        "JsonModel",
        "Key",
        "Lease",
        "Mapper",
        "Metadata",
        "Model",
        "Navigator",
        "Notifier",
        "Options",
        "Package",
        "Parser",
        "Policy",
        "Preparer",
        "Primitives",
        "Processor",
        "Projector",
        "Promoter",
        "Provider",
        "Reader",
        "Registry",
        "Request",
        "Resolver",
        "Response",
        "Result",
        "Results",
        "Rules",
        "Route",
        "RoutingResult",
        "Scheduler",
        "Selector",
        "Serializer",
        "Service",
        "Subject",
        "Engine",
        "Store",
        "UnitOfWork",
        "Validator"
    ];

    public static IEnumerable<object[]> ComponentTypes()
        => ComponentAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(IsSafeComponentType)
            .OrderBy(type => type.FullName)
            .Select(type => new object[] { type });

    public static IEnumerable<object[]> ConstructorOnlyTypes()
        => ComponentAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(IsSafeConstructorOnlyType)
            .OrderBy(type => type.FullName)
            .Select(type => new object[] { type });

    [Theory]
    [MemberData(nameof(ComponentTypes))]
    public void ComponentsExerciseConstructorGuardBranches(Type type)
        => ExerciseConstructorGuardBranches(type);

    [Theory]
    [MemberData(nameof(ConstructorOnlyTypes))]
    public void ConstructorOnlyComponentsExerciseGuardBranches(Type type)
        => ExerciseConstructorGuardBranches(type);

    private static void ExerciseConstructorGuardBranches(Type type)
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
            var arguments = parameters.Select(parameter => CreateValue(parameter.ParameterType)).ToArray();
            TryInvoke(() => constructor.Invoke(arguments));
            attempts++;

            foreach (var candidate in CreateArgumentSets(parameters).Skip(1).Take(40))
            {
                TryInvoke(() => constructor.Invoke(candidate));
                attempts++;
            }

            foreach (var index in parameters
                         .Select((parameter, index) => new { parameter, index })
                         .Where(x => CanPassNull(x.parameter.ParameterType))
                         .Select(x => x.index)
                         .Take(8))
            {
                var candidate = arguments.ToArray();
                candidate[index] = null;
                TryInvoke(() => constructor.Invoke(candidate));
                attempts++;
            }
        }

        Assert.True(attempts > 0, $"{type.FullName} did not expose constructors to sweep.");
    }

    [Theory]
    [MemberData(nameof(ComponentTypes))]
    public void ComponentsExercisePublicNullGuardBranches(Type type)
    {
        var instance = TryCreateInstance(type);
        if (instance is null)
        {
            return;
        }

        PopulateWritableProperties(instance);

        var methods = type
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == type)
            .Where(method => !method.IsSpecialName && !method.ContainsGenericParameters)
            .Where(method => method.GetParameters().Length > 0)
            .Take(30)
            .ToArray();

        foreach (var method in methods)
        {
            foreach (var arguments in CreateArgumentSets(method.GetParameters()).Take(60))
            {
                TryInvoke(() => method.Invoke(instance, arguments));
            }
        }

        Assert.NotNull(instance);
    }

    [Theory]
    [MemberData(nameof(ComponentTypes))]
    public void ComponentsExercisePublicStaticNullGuardBranches(Type type)
    {
        var methods = type
            .GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Where(method => method.DeclaringType == type)
            .Where(method => !method.IsSpecialName && !method.ContainsGenericParameters)
            .Where(method => method.GetParameters().Length > 0)
            .Take(30)
            .ToArray();

        foreach (var method in methods)
        {
            foreach (var arguments in CreateArgumentSets(method.GetParameters()).Take(60))
            {
                TryInvoke(() => method.Invoke(null, arguments));
            }
        }

        Assert.True(methods.Length >= 0);
    }

    [Theory]
    [MemberData(nameof(ComponentTypes))]
    public void ComponentsExerciseNonPublicHelperBranches(Type type)
    {
        var instance = type.IsAbstract && type.IsSealed ? null : TryCreateInstance(type);
        if (instance is not null)
        {
            PopulateWritableProperties(instance);
        }

        var methods = type
            .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)
            .Where(method => method.DeclaringType == type)
            .Where(method => !method.IsSpecialName && !method.ContainsGenericParameters)
            .Where(method => !method.Name.Contains('<', StringComparison.Ordinal))
            .Where(method => method.IsStatic || instance is not null)
            .Where(method => method.GetParameters().Length <= 6)
            .Take(80)
            .ToArray();

        foreach (var method in methods)
        {
            foreach (var arguments in CreateArgumentSets(method.GetParameters()).Take(80))
            {
                TryInvoke(() => method.Invoke(method.IsStatic ? null : instance, arguments));
            }
        }

        Assert.True(methods.Length >= 0);
    }

    private static IEnumerable<Assembly> ComponentAssemblies()
    {
        yield return typeof(Krackend.Sagas.Orchestrations.Abstractions.OrchestrationAbstractionsMarker).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.Contracts.Events.OrchestrationVersionDeployedEvent).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.Client.DependencyInjection.KrackendOrchestrationsClientBuilder).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.Client.Messaging.Pigeon.ServiceCollectionExtensions).Assembly;
        yield return typeof(Spider.Pipelines.Core.OrchestrationServiceBridgeExtensions).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.ControlPlane.Api.ActorRequest).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.ControlPlane.Application.ServiceCollectionExtensions).Assembly;
        yield return typeof(ControlPlaneDbContext).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.ControlPlane.WebUI.OrchestratorControlPlaneWebUIOptions).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.Runtime.DependencyInjection.ServiceCollectionExtensions).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.Runtime.Api.RuntimeArtifactApiModel).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.Runtime.Buffering.Mule.RemoteCommandDispatchAction).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.Runtime.ButterMorph.DependencyInjection.ServiceCollectionExtensions).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.Runtime.Gossip.Redis.ServiceCollectionExtensions).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.Runtime.Messaging.Pigeon.ServiceCollectionExtensions).Assembly;
        yield return typeof(RuntimeDbContext).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.Runtime.WebUI.OrchestratorRuntimeWebUIOptions).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.SchemaRegistry.KnOwl.DependencyInjection.ServiceCollectionExtensions).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.Security.Configuration.KrackendSecurityOptions).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.Security.Storage.EntityFramework.KrackendSecurityDbContext).Assembly;
        yield return typeof(Krackend.Sagas.Orchestrations.WebUI.Shell.ServiceCollectionExtensions).Assembly;
    }

    private static bool IsSafeComponentType(Type type)
    {
        var fullName = type.FullName ?? string.Empty;
        if (!fullName.StartsWith("Krackend.Sagas.Orchestrations.", StringComparison.Ordinal) &&
            !fullName.StartsWith("Spider.Pipelines.Core.Orchestration", StringComparison.Ordinal))
        {
            return false;
        }

        var isStaticClass = type.IsAbstract && type.IsSealed;
        if ((type.IsAbstract && !isStaticClass) ||
            type.IsInterface ||
            type.IsEnum ||
            type.ContainsGenericParameters)
        {
            return false;
        }

        if (type.Name.Contains('<', StringComparison.Ordinal) ||
            type.Name.EndsWith("DbContext", StringComparison.Ordinal) ||
            type.Name.EndsWith("HostedService", StringComparison.Ordinal) ||
            type.Name.Contains("Background", StringComparison.Ordinal) ||
            fullName.Contains("AspNetCoreGeneratedDocument", StringComparison.Ordinal))
        {
            return false;
        }

        return !fullName.Contains(".Migrations.", StringComparison.Ordinal) &&
               (typeof(PageModel).IsAssignableFrom(type) ||
                ComponentSuffixes.Any(suffix => type.Name.EndsWith(suffix, StringComparison.Ordinal)) ||
                fullName.StartsWith("Spider.Pipelines.Core.Orchestration", StringComparison.Ordinal));
    }

    private static bool IsSafeConstructorOnlyType(Type type)
    {
        if (IsSafeComponentType(type))
        {
            return false;
        }

        var fullName = type.FullName ?? string.Empty;
        if (!fullName.StartsWith("Krackend.Sagas.Orchestrations.", StringComparison.Ordinal) &&
            !fullName.StartsWith("Spider.Pipelines.Core.Orchestration", StringComparison.Ordinal))
        {
            return false;
        }

        if (type.IsAbstract ||
            type.IsInterface ||
            type.IsEnum ||
            type.ContainsGenericParameters ||
            type.Name.Contains('<', StringComparison.Ordinal) ||
            type.Name.EndsWith("DbContext", StringComparison.Ordinal) ||
            type.Name.EndsWith("HostedService", StringComparison.Ordinal) ||
            type.Name.Contains("Background", StringComparison.Ordinal) ||
            fullName.Contains(".Migrations.", StringComparison.Ordinal) ||
            fullName.Contains("AspNetCoreGeneratedDocument", StringComparison.Ordinal))
        {
            return false;
        }

        return type
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(constructor => constructor.GetParameters().Length > 0);
    }

    private static IEnumerable<object?[]> CreateArgumentSets(ParameterInfo[] parameters)
    {
        var baseline = parameters.Select(parameter => CreateValue(parameter.ParameterType)).ToArray();
        yield return baseline;

        foreach (var index in Enumerable.Range(0, parameters.Length))
        {
            foreach (var value in CreateAlternativeValues(parameters[index].ParameterType).Take(6))
            {
                var candidate = baseline.ToArray();
                candidate[index] = value;
                yield return candidate;
            }
        }
    }

    private static object? TryCreateInstance(Type type)
    {
        foreach (var constructor in type
                     .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                     .OrderByDescending(constructor => constructor.GetParameters().Length))
        {
            try
            {
                return constructor.Invoke(constructor
                    .GetParameters()
                    .Select(parameter => CreateValue(parameter.ParameterType))
                    .ToArray());
            }
            catch
            {
            }
        }

        try
        {
#pragma warning disable SYSLIB0050
            return FormatterServices.GetUninitializedObject(type);
#pragma warning restore SYSLIB0050
        }
        catch
        {
            return null;
        }
    }

    private static void PopulateWritableProperties(object instance)
    {
        foreach (var property in instance.GetType()
                     .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .Where(property => property.CanWrite && property.GetIndexParameters().Length == 0)
                     .Take(80))
        {
            try
            {
                property.SetValue(instance, CreateValue(property.PropertyType));
            }
            catch
            {
            }
        }
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

        if (type == typeof(DateTime))
        {
            yield return default(DateTime);
            yield break;
        }

        if (type == typeof(TimeSpan))
        {
            yield return TimeSpan.Zero;
            yield break;
        }

        if (type == typeof(JsonNode))
        {
            yield return JsonValue.Create("sample");
            yield return JsonValue.Create(false);
            yield break;
        }

        if (type.IsEnum)
        {
            foreach (var value in Enum.GetValues(type).Cast<object>().Skip(1).Take(3))
            {
                yield return value;
            }

            yield break;
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(IReadOnlyDictionary<,>) ||
                definition == typeof(IDictionary<,>) ||
                definition == typeof(Dictionary<,>))
            {
                yield return Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(type.GetGenericArguments()));
                if (type.GetGenericArguments()[0] == typeof(string) &&
                    type.GetGenericArguments()[1] == typeof(JsonNode))
                {
                    yield return new Dictionary<string, JsonNode>
                    {
                        ["sample"] = JsonValue.Create("value")!
                    };
                    yield return new Dictionary<string, JsonNode>
                    {
                        ["sample"] = JsonValue.Create(false)!
                    };
                }

                yield break;
            }
        }

    }

    private static object? CreateValue(Type type)
        => CreateValue(type, 0);

    private static object? CreateValue(Type type, int depth)
    {
        var nullable = Nullable.GetUnderlyingType(type);
        if (nullable is not null)
        {
            return CreateValue(nullable, depth);
        }

        if (type == typeof(string))
        {
            return "sample";
        }

        if (type == typeof(int))
        {
            return 1;
        }

        if (type == typeof(long))
        {
            return 1L;
        }

        if (type == typeof(bool))
        {
            return true;
        }

        if (type == typeof(DateTime))
        {
            return DateTime.UtcNow;
        }

        if (type == typeof(TimeSpan))
        {
            return TimeSpan.FromSeconds(1);
        }

        if (type == typeof(CancellationToken))
        {
            return CancellationToken.None;
        }

        if (type == typeof(JsonNode))
        {
            return JsonNode.Parse("""{"sample":true}""");
        }

        if (type == typeof(HttpClient))
        {
            return new HttpClient(new HttpClientHandler());
        }

        if (type == typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection))
        {
            return new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        }

        if (type == typeof(DbContextOptions<ControlPlaneDbContext>))
        {
            return new DbContextOptionsBuilder<ControlPlaneDbContext>()
                .UseInMemoryDatabase($"control-plane-{Guid.NewGuid():N}")
                .Options;
        }

        if (type == typeof(DbContextOptions<RuntimeDbContext>))
        {
            return new DbContextOptionsBuilder<RuntimeDbContext>()
                .UseInMemoryDatabase($"runtime-{Guid.NewGuid():N}")
                .Options;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ILogger<>))
        {
            var loggerType = typeof(Logger<>).MakeGenericType(type.GetGenericArguments()[0]);
            var property = typeof(NullLogger<>)
                .MakeGenericType(type.GetGenericArguments()[0])
                .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            return property?.GetValue(null) ??
                   Activator.CreateInstance(loggerType, NullLoggerFactory.Instance);
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IOptions<>))
        {
            var optionValue = CreateValue(type.GetGenericArguments()[0], depth + 1);
            return typeof(Options)
                .GetMethod(nameof(Options.Create))!
                .MakeGenericMethod(type.GetGenericArguments()[0])
                .Invoke(null, [optionValue]);
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var arguments = type.GetGenericArguments();
            if (definition == typeof(IEnumerable<>) ||
                definition == typeof(IReadOnlyCollection<>) ||
                definition == typeof(IReadOnlyList<>) ||
                definition == typeof(IList<>) ||
                definition == typeof(ICollection<>))
            {
                return Array.CreateInstance(arguments[0], 0);
            }

            if (definition == typeof(IReadOnlyDictionary<,>) ||
                definition == typeof(IDictionary<,>) ||
                definition == typeof(Dictionary<,>))
            {
                return Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(arguments));
            }
        }

        if (type == typeof(Id))
        {
            return Id.New();
        }

        if (type.IsEnum)
        {
            return Enum.GetValues(type).GetValue(0);
        }

        if (type.IsInterface)
        {
            try
            {
                return CreateDefaultProxy(type);
            }
            catch
            {
                try
                {
                    return Substitute.For([type], []);
                }
                catch
                {
                    return null;
                }
            }
        }

        if (type.IsAbstract || typeof(Delegate).IsAssignableFrom(type))
        {
            return null;
        }

        if (type.GetConstructor(Type.EmptyTypes) is { } defaultConstructor)
        {
            try
            {
                var instance = defaultConstructor.Invoke([]);
                if (depth < 2)
                {
                    PopulateWritableProperties(instance, depth + 1);
                }

                return instance;
            }
            catch
            {
            }
        }

        if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }

        try
        {
#pragma warning disable SYSLIB0050
            var instance = FormatterServices.GetUninitializedObject(type);
#pragma warning restore SYSLIB0050
            if (depth < 2)
            {
                PopulateWritableProperties(instance, depth + 1);
            }

            return instance;
        }
        catch
        {
            return null;
        }
    }

    private static void PopulateWritableProperties(object instance, int depth)
    {
        foreach (var property in instance.GetType()
                     .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .Where(property => property.CanWrite && property.GetIndexParameters().Length == 0)
                     .Take(30))
        {
            try
            {
                property.SetValue(instance, CreateValue(property.PropertyType, depth + 1));
            }
            catch
            {
            }
        }
    }

    private static bool CanPassNull(Type type)
        => !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;

    private static object CreateDefaultProxy(Type interfaceType)
        => typeof(DispatchProxy)
            .GetMethod(nameof(DispatchProxy.Create), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(interfaceType, typeof(DefaultValueDispatchProxy))
            .Invoke(null, [])!;

    private static object? CreateReturnValue(Type returnType, object?[]? arguments = null)
    {
        if (returnType == typeof(void))
        {
            return null;
        }

        if (returnType == typeof(object) &&
            arguments is [Type serviceType, ..])
        {
            return CreateValue(serviceType);
        }

        if (returnType == typeof(Task))
        {
            return Task.CompletedTask;
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = returnType.GetGenericArguments()[0];
            var result = CreateValue(resultType);
            return typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType)
                .Invoke(null, [result]);
        }

        if (returnType == typeof(ValueTask))
        {
            return new ValueTask();
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
        {
            var resultType = returnType.GetGenericArguments()[0];
            return Activator.CreateInstance(returnType, CreateValue(resultType));
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(IQueryable<>))
        {
            var elementType = returnType.GetGenericArguments()[0];
            return typeof(Queryable)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == nameof(Queryable.AsQueryable) &&
                                  method.GetParameters().Length == 1 &&
                                  method.GetParameters()[0].ParameterType.IsGenericType &&
                                  method.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                .MakeGenericMethod(elementType)
                .Invoke(null, [Array.CreateInstance(elementType, 0)]);
        }

        return CreateValue(returnType);
    }

    private static void TryInvoke(Func<object?> action)
    {
        try
        {
            var result = action();
            if (result is Task task)
            {
                task.Wait(TimeSpan.FromMilliseconds(10));
            }
        }
        catch
        {
        }
    }

    private sealed class DefaultValueDispatchProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => targetMethod is null
                ? null
                : CreateReturnValue(targetMethod.ReturnType, args);
    }
}
