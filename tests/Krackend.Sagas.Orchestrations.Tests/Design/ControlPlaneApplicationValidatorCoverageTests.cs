namespace Krackend.Sagas.Orchestrations.Tests.Design;

using FluentValidation;
using FluentValidation.Results;
using Krackend.Sagas.Orchestrations.Abstractions.Primitives;
using Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;
using System.Reflection;

public sealed class ControlPlaneApplicationValidatorCoverageTests
{
    [Fact]
    public void PublicValidatorsHandleIncompleteRequestsWithoutThrowing()
    {
        var validatorTypes = typeof(CreateOrchestrationDefinitionCommand).Assembly
            .GetTypes()
            .Where(type =>
                type is { IsClass: true, IsAbstract: false } &&
                type.IsPublic &&
                typeof(IValidator).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(validatorTypes);

        var results = new List<ValidationResult>();
        foreach (var validatorType in validatorTypes)
        {
            var requestType = ResolveValidatedType(validatorType);
            if (requestType is null)
            {
                continue;
            }

            var request = CreateIncompleteRequest(requestType);
            var validator = (IValidator)Activator.CreateInstance(validatorType)!;
            var context = (IValidationContext)Activator.CreateInstance(
                typeof(ValidationContext<>).MakeGenericType(requestType),
                request)!;

            results.Add(validator.Validate(context));
        }

        Assert.Equal(validatorTypes.Length, results.Count);
        Assert.Contains(results, result => !result.IsValid);
    }

    private static Type? ResolveValidatedType(Type validatorType)
        => validatorType
            .GetInterfaces()
            .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IValidator<>))
            .Select(type => type.GetGenericArguments()[0])
            .SingleOrDefault();

    private static object CreateIncompleteRequest(Type requestType)
    {
        var constructor = requestType
            .GetConstructors()
            .OrderByDescending(constructor => constructor.GetParameters().Length)
            .FirstOrDefault();

        if (constructor is null)
        {
            return Activator.CreateInstance(requestType)!;
        }

        var values = constructor
            .GetParameters()
            .Select(parameter => CreateIncompleteValue(parameter.ParameterType))
            .ToArray();

        return constructor.Invoke(values);
    }

    private static object? CreateIncompleteValue(Type type)
    {
        var nullableUnderlying = Nullable.GetUnderlyingType(type);
        if (nullableUnderlying is not null)
        {
            return null;
        }

        if (type == typeof(string))
        {
            return string.Empty;
        }

        if (type == typeof(int))
        {
            return -1;
        }

        if (type == typeof(bool))
        {
            return false;
        }

        if (type.IsEnum)
        {
            return Enum.ToObject(type, 999);
        }

        if (type == typeof(SemanticVersion))
        {
            return new SemanticVersion(0, 0, 0);
        }

        if (type == typeof(Duration))
        {
            return Duration.FromSeconds(0);
        }

        if (type == typeof(Expression))
        {
            return new Expression(string.Empty);
        }

        if (type.IsArray)
        {
            return Array.CreateInstance(type.GetElementType()!, 0);
        }

        if (type.IsGenericType &&
            type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return Array.CreateInstance(type.GetGenericArguments()[0], 0);
        }

        if (type.GetConstructor(Type.EmptyTypes) is not null)
        {
            return Activator.CreateInstance(type);
        }

        return null;
    }
}
