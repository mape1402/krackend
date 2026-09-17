namespace Krackend.Sagas.Orchestrations.Tests.Application;

using FluentValidation;
using FluentValidation.Results;
using Pelican.Mediator;
using DesignValidationPipelineBehavior = Krackend.Sagas.Orchestrations.ControlPlane.Application.Design.ValidationPipelineBehavior<Krackend.Sagas.Orchestrations.Tests.Application.ValidationPipelineBehaviorTests.SampleRequest, string>;
using SecurityValidationPipelineBehavior = Krackend.Sagas.Orchestrations.ControlPlane.Application.Security.ValidationPipelineBehavior<Krackend.Sagas.Orchestrations.Tests.Application.ValidationPipelineBehaviorTests.SampleRequest, string>;

public sealed class ValidationPipelineBehaviorTests
{
    [Fact]
    public async Task DesignBehaviorCallsNextWhenNoValidatorsAreRegistered()
    {
        var behavior = new DesignValidationPipelineBehavior([]);
        var called = false;

        var result = await behavior.Handle(new SampleRequest("ok"), _ =>
        {
            called = true;
            return Task.FromResult("handled");
        }, CancellationToken.None);

        Assert.True(called);
        Assert.Equal("handled", result);
    }

    [Fact]
    public async Task DesignBehaviorAggregatesValidationFailuresAndDoesNotCallNext()
    {
        var behavior = new DesignValidationPipelineBehavior(
        [
            new RecordingValidator("Name", "Name is required."),
            new RecordingValidator("Key", "Key is required.")
        ]);
        var called = false;

        var exception = await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(new SampleRequest(""), _ =>
        {
            called = true;
            return Task.FromResult("handled");
        }, CancellationToken.None));

        Assert.False(called);
        Assert.Equal(["Name", "Key"], exception.Errors.Select(error => error.PropertyName).ToArray());
    }

    [Fact]
    public async Task SecurityBehaviorCallsNextWhenValidatorsSucceed()
    {
        var behavior = new SecurityValidationPipelineBehavior([new PassingValidator()]);
        var called = false;

        var result = await behavior.Handle(new SampleRequest("ok"), _ =>
        {
            called = true;
            return Task.FromResult("authorized");
        }, CancellationToken.None);

        Assert.True(called);
        Assert.Equal("authorized", result);
    }

    [Fact]
    public async Task SecurityBehaviorThrowsWhenAnyValidatorFails()
    {
        var behavior = new SecurityValidationPipelineBehavior([new PassingValidator(), new RecordingValidator("Role", "Role is required.")]);
        var called = false;

        var exception = await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(new SampleRequest("ok"), _ =>
        {
            called = true;
            return Task.FromResult("authorized");
        }, CancellationToken.None));

        Assert.False(called);
        Assert.Equal("Role", Assert.Single(exception.Errors).PropertyName);
    }

    public sealed record SampleRequest(string Value) : IRequest<string>;

    private sealed class PassingValidator : AbstractValidator<SampleRequest>
    {
    }

    private sealed class RecordingValidator : IValidator<SampleRequest>
    {
        private readonly string _propertyName;
        private readonly string _message;

        public RecordingValidator(string propertyName, string message)
        {
            _propertyName = propertyName;
            _message = message;
        }

        public ValidationResult Validate(SampleRequest instance)
            => new([new ValidationFailure(_propertyName, _message)]);

        public Task<ValidationResult> ValidateAsync(SampleRequest instance, CancellationToken cancellation = default)
            => Task.FromResult(Validate(instance));

        public ValidationResult Validate(IValidationContext context)
            => new([new ValidationFailure(_propertyName, _message)]);

        public Task<ValidationResult> ValidateAsync(IValidationContext context, CancellationToken cancellation = default)
            => Task.FromResult(Validate(context));

        public IValidatorDescriptor CreateDescriptor()
            => new InlineValidator<SampleRequest>().CreateDescriptor();

        public bool CanValidateInstancesOfType(Type type)
            => type == typeof(SampleRequest);
    }
}
