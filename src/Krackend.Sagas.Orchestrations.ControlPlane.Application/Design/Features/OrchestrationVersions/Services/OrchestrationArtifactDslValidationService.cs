namespace Krackend.Sagas.Orchestrations.ControlPlane.Application.Design;

using System.Text.Json;
using global::ButterMorph.Abstractions;
using global::ButterMorph.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TransformationConfigurations;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.TriggerChannels;
using Krackend.Sagas.Orchestrations.ControlPlane.Design.Core.ValidationConfigurations;

/// <summary>
/// Validates ButterMorph DSL stored in orchestration versions before artifact publication.
/// </summary>
public sealed class OrchestrationArtifactDslValidationService : IOrchestrationArtifactDslValidationService
{
    private readonly IDslParser _dslParser;
    private readonly ITransformationSemanticAnalyzer _semanticAnalyzer;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestrationArtifactDslValidationService"/> class.
    /// </summary>
    /// <param name="dslParser">ButterMorph DSL parser.</param>
    /// <param name="semanticAnalyzer">ButterMorph semantic analyzer.</param>
    public OrchestrationArtifactDslValidationService(
        IDslParser dslParser,
        ITransformationSemanticAnalyzer semanticAnalyzer)
    {
        _dslParser = dslParser ?? throw new ArgumentNullException(nameof(dslParser));
        _semanticAnalyzer = semanticAnalyzer ?? throw new ArgumentNullException(nameof(semanticAnalyzer));
    }

    /// <inheritdoc />
    public void Validate(OrchestrationVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);

        foreach (var trigger in version.TriggerBindings.Where(x => x.IsEnabled))
        {
            ValidateTrigger(trigger);
        }

        foreach (var stage in version.StageDefinitions.OrderBy(x => x.Order))
        {
            foreach (var task in stage.TaskDefinitions.Where(x => x.IsEnabled).OrderBy(x => x.Order))
            {
                ValidateTask(stage, task);
            }
        }
    }

    private void ValidateTrigger(TriggerBinding trigger)
    {
        if (trigger.TriggerChannel is EventTriggerChannel eventChannel && eventChannel.HasValidation)
        {
            ValidateValidation(
                eventChannel.Validation,
                $"trigger:{trigger.Key}:event-validation");
        }
    }

    private void ValidateTask(StageDefinition stage, TaskDefinition task)
    {
        if (task.HasTransformation)
        {
            ValidateTransformation(
                task.Transformation,
                $"stage:{stage.Key}:task:{task.Key}:transformation");
        }

        if (task.Configuration is MessagingTaskConfiguration messaging)
        {
            if (messaging.HasRequestValidation)
            {
                ValidateValidation(
                    messaging.RequestValidation,
                    $"stage:{stage.Key}:task:{task.Key}:request-validation");
            }

            if (messaging.HasResponseValidation)
            {
                ValidateValidation(
                    messaging.ResponseValidation,
                    $"stage:{stage.Key}:task:{task.Key}:response-validation");
            }
        }

        ValidateCompensation(stage, task);
    }

    private void ValidateCompensation(StageDefinition stage, TaskDefinition task)
    {
        var compensation = task.CompensationDefinition;
        if (compensation is null)
        {
            return;
        }

        if (compensation.HasTransformation)
        {
            ValidateTransformation(
                compensation.Transformation,
                $"stage:{stage.Key}:task:{task.Key}:compensation-transformation");
        }

        if (compensation.Configuration is not MessagingTaskConfiguration messaging)
        {
            return;
        }

        if (messaging.HasRequestValidation)
        {
            ValidateValidation(
                messaging.RequestValidation,
                $"stage:{stage.Key}:task:{task.Key}:compensation-request-validation");
        }

        if (messaging.HasResponseValidation)
        {
            ValidateValidation(
                messaging.ResponseValidation,
                $"stage:{stage.Key}:task:{task.Key}:compensation-response-validation");
        }
    }

    private void ValidateTransformation(TransformationDefinition transformation, string path)
    {
        if (transformation?.Configuration is not DslTransformationConfiguration configuration)
        {
            throw new OrchestrationArtifactDslValidationException(
                path,
                $"Transformation '{path}' must use ButterMorph DSL configuration.");
        }

        if (string.IsNullOrWhiteSpace(configuration.Dsl))
        {
            throw new OrchestrationArtifactDslValidationException(
                path,
                $"Transformation '{path}' is enabled but does not contain DSL.");
        }

        Analyze(configuration.Dsl, path);
    }

    private void ValidateValidation(ValidationDefinition validation, string path)
    {
        if (validation?.Configuration is not DslValidationConfiguration configuration)
        {
            throw new OrchestrationArtifactDslValidationException(
                path,
                $"Validation '{path}' must use ButterMorph DSL configuration.");
        }

        if (string.IsNullOrWhiteSpace(configuration.Dsl))
        {
            throw new OrchestrationArtifactDslValidationException(
                path,
                $"Validation '{path}' is enabled but does not contain DSL.");
        }

        Analyze(configuration.Dsl, path);
    }

    private void Analyze(string dsl, string path)
    {
        try
        {
            var document = _dslParser.Parse(new DslDefinition { Content = dsl });
            if (document is not ITransformationDocument transformationDocument)
            {
                throw new OrchestrationArtifactDslValidationException(
                    path,
                    $"DSL at '{path}' is not a transformation document.");
            }

            var result = _semanticAnalyzer.Analyze(transformationDocument);
            if (!result.Succeeded)
            {
                throw new OrchestrationArtifactDslValidationException(
                    path,
                    $"DSL at '{path}' has semantic errors.",
                    SerializeDiagnostics(result.Diagnostics));
            }
        }
        catch (OrchestrationArtifactDslValidationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new OrchestrationArtifactDslValidationException(
                path,
                $"DSL at '{path}' could not be parsed or analyzed: {exception.Message}",
                SerializeException(exception));
        }
    }

    private static string SerializeDiagnostics(IReadOnlyCollection<DiagnosticEntry> diagnostics)
        => JsonSerializer.Serialize((diagnostics ?? Array.Empty<DiagnosticEntry>()).Select(x => new
        {
            x.Code,
            x.Message,
            x.Path,
            x.Severity
        }));

    private static string SerializeException(Exception exception)
        => JsonSerializer.Serialize(new
        {
            ExceptionType = exception.GetType().FullName,
            exception.Message
        });
}
