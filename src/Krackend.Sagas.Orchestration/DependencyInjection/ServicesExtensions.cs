namespace Microsoft.Extensions.DependencyInjection
{
    using Krackend.Sagas.Orchestration.Controller.Configuration.Builders;
    using Krackend.Sagas.Orchestration.Controller.Configuration.Metadata;
    using Krackend.Sagas.Orchestration.Controller.Dispatching;
    using Krackend.Sagas.Orchestration.Controller.Tracking;
    using Krackend.Sagas.Orchestration.Core;
    using Krackend.Sagas.Orchestration.Worker;
    using Krackend.Sagas.Orchestration.Worker.Pipelines;
    using Microsoft.AspNetCore.Builder;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Producing;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Provides extension methods for registering orchestration services and error code classification in the dependency injection container.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class ServicesExtensions
    {
        private static bool _isCoreAdded = false;
        private static Dictionary<Type, int> _errorCodeBindings = new();

        /// <summary>
        /// Registers the orchestration controller and related services in the dependency injection container.
        /// </summary>
        /// <param name="services">The service collection to add orchestration services to.</param>
        /// <returns>An <see cref="IOrchestrationServiceBuilder"/> for further configuration.</returns>
        public static IOrchestrationServiceBuilder AddOrchestrationController(this IServiceCollection services)
        {
            services.AddCore();
            services.AddSingleton<ISagaIdGenerator, DefaultSagaIdGenerator>();
            services.AddSingleton<IOrchestratorBuilder, OrchestratorBuilder>();
            services.AddScoped<IDispatcher, Dispatcher>();
            services.AddScoped<IStateManager, StateManager>();
            services.AddSingleton<IStateMachineManager, StateMachineManager>();

            services.AddSingleton<IRoadmapManager, RoadmapManager>();

            return new OrchestrationServiceBuilder(services);
        }

        /// <summary>
        /// Registers an orchestration implementation as a singleton in the dependency injection container.
        /// </summary>
        /// <typeparam name="TOrchestration">The orchestration implementation type.</typeparam>
        /// <param name="builder">The orchestration service builder.</param>
        /// <returns>The same <see cref="IOrchestrationServiceBuilder"/> for chaining.</returns>
        public static IOrchestrationServiceBuilder AddOrchestration<TOrchestration>(this IOrchestrationServiceBuilder builder)
            where TOrchestration : class, IOrchestration
        {
            builder.Services.AddSingleton<IOrchestration, TOrchestration>();
            return builder;
        }

        /// <summary>
        /// Configures the application to use the orchestration controller, starting all registered orchestrations.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The same <see cref="IApplicationBuilder"/> for chaining.</returns>
        public static IApplicationBuilder UseOrchestrationController(this IApplicationBuilder app)
        {
            var orchestrations = app.ApplicationServices.GetServices<IOrchestration>();
            var orchestratorBuilder = app.ApplicationServices.GetRequiredService<IOrchestratorBuilder>();
            orchestratorBuilder.Start(orchestrations);

            return app;
        }

        /// <summary>
        /// Registers core orchestration worker services, including failure, success, and exception evaluator services, in the dependency injection container.
        /// </summary>
        /// <param name="services">The service collection to add orchestration worker services to.</param>
        /// <returns>An <see cref="IOrchestrationServiceBuilder"/> for further configuration.</returns>
        public static IOrchestrationServiceBuilder AddOrchestrationWorker(this IServiceCollection services)
        {
            services.AddCore();

            services.AddScoped<IFailureService, FailureService>();
            services.AddScoped<ISuccessService, SuccessService>();
            services.AddScoped<IExceptionEvaluator, ExceptionEvaluator>();

            services.AddSingleton(new ErrorCodeClasificator(type =>
            {
                if (_errorCodeBindings.TryGetValue(type, out var errorCode))
                {
                    return errorCode;
                }

                return 0; // Default error code if not found
            }));

            return new OrchestrationServiceBuilder(services);
        }

        /// <summary>
        /// Registers a custom error code clasificator delegate for exception type classification.
        /// </summary>
        /// <param name="builder">The orchestration service builder.</param>
        /// <param name="clasificator">The error code clasificator delegate to register.</param>
        /// <returns>The same <see cref="IOrchestrationServiceBuilder"/> for chaining.</returns>
        public static IOrchestrationServiceBuilder AddErrorCodeClasificator(this IOrchestrationServiceBuilder builder, ErrorCodeClasificator clasificator)
        {
            builder.Services.AddSingleton(clasificator);
            return builder;
        }

        /// <summary>
        /// Registers an error code binding for a specific exception type.
        /// </summary>
        /// <typeparam name="TException">The exception type to bind the error code to.</typeparam>
        /// <param name="builder">The orchestration service builder.</param>
        /// <param name="errorCode">The error code to associate with the exception type.</param>
        /// <returns>The same <see cref="IOrchestrationServiceBuilder"/> for chaining.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the error code for the exception type is already set.</exception>
        public static IOrchestrationServiceBuilder AddErrorCodeBinding<TException>(this IOrchestrationServiceBuilder builder, int errorCode)
            where TException : Exception
        {
            if (_errorCodeBindings.ContainsKey(typeof(TException)))
                throw new InvalidOperationException($"Error code for {typeof(TException).Name} is already set.");

            _errorCodeBindings.Add(typeof(TException), errorCode);
            return builder;
        }

        /// <summary>
        /// Registers core orchestration context, metadata setter, and interceptors in the service collection if not already added.
        /// </summary>
        /// <param name="services">The service collection to add core services to.</param>
        private static void AddCore(this IServiceCollection services)
        {
            if (_isCoreAdded)
                return;

            _isCoreAdded = true;

            services.AddScoped<IPublishInterceptor, OrchestrationPublishInterceptor>();
            services.AddScoped<IConsumeInterceptor, OrchestrationConsumeInterceptor>();

            services.AddScoped<IOrchestrationContext, OrchestrationContext>();
            services.AddScoped<IMetadataSetter, OrchestrationContext>();
        }
    }
}
