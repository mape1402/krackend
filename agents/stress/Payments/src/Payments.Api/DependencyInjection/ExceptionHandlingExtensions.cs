using System.Net;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using TurtlePath.Exceptions;
using TurtlePath.ExceptionHandling;
using TurtlePath.ExceptionHandling.AspNetCore;
using TurtlePath.ExceptionHandling.Consumers;
using TurtlePath.ExceptionHandling.Workers;
using Payments.Business;
using TurtlePath.Validation;

namespace Microsoft.Extensions.DependencyInjection
{
    [ExcludeFromCodeCoverage]
    internal static class ExceptionHandlingExtensions
    {
        internal static IServiceCollection AddExceptionHandlingDefaults(
            this IServiceCollection services,
            Action<ExceptionHandlingOptionsBuilder> configure = null)
        {
            var profileAssemblies = GetExceptionHandlingProfileAssemblies();

            services.AddTemplateExceptionHandlingCore(configure);
            services.AddTurtlePathAspNetCoreExceptionHandling(builder =>
            {
                ConfigureProfiles(
                    profileAssemblies,
                    "TurtlePath.ExceptionHandling.AspNetCore.IHttpExceptionHandlingProfile",
                    builder);
            });
            services.AddTurtlePathConsumerExceptionHandling(builder =>
            {
                ConfigureProfiles(
                    profileAssemblies,
                    "TurtlePath.ExceptionHandling.Consumers.IConsumerExceptionHandlingProfile",
                    builder);
            });
            services.AddExceptionHandlingProfiles(profileAssemblies);

            return services;
        }

        internal static IServiceCollection AddJobExceptionHandlingDefaults(
            this IServiceCollection services,
            Action<ExceptionHandlingOptionsBuilder> configure = null)
        {
            var profileAssemblies = GetExceptionHandlingProfileAssemblies();

            services.AddTemplateExceptionHandlingCore(configure);
            services.AddTurtlePathWorkerExceptionHandling(builder =>
            {
                ConfigureProfiles(
                    profileAssemblies,
                    "TurtlePath.ExceptionHandling.Workers.IBackgroundExceptionHandlingProfile",
                    builder);
            });
            services.AddExceptionHandlingProfiles(profileAssemblies);

            return services;
        }

        private static IServiceCollection AddTemplateExceptionHandlingCore(
            this IServiceCollection services,
            Action<ExceptionHandlingOptionsBuilder> configure = null)
        {
            return services.AddTurtlePathExceptionHandlingCore(builder =>
            {
                builder.For<ValidationException>(
                    _ => ExceptionKind.Validation,
                    exception => "validation",
                    exception => exception.Errors);

                builder.For<HttpException>(
                    exception => MapHttpStatusCode(exception.StatusCode),
                    exception => ((int)exception.StatusCode).ToString(),
                    exception => [ exception.Message ]);

                builder.For<BadRequestException>(ExceptionKind.Validation, exception => exception.Message);
                builder.For<ForbiddenException>(ExceptionKind.Forbidden, exception => exception.Message);
                builder.For<NotFoundException>(ExceptionKind.NotFound, exception => exception.Message);
                builder.For<UnauthorizedException>(ExceptionKind.Unauthorized, exception => exception.Message);

                configure?.Invoke(builder);
            });
        }

        private static Assembly[] GetExceptionHandlingProfileAssemblies()
        {
            return
            [
                typeof(Constants).Assembly,
                typeof(ExceptionHandlingExtensions).Assembly
            ];
        }

        private static void ConfigureProfiles(
            IEnumerable<Assembly> profileAssemblies,
            string profileInterfaceName,
            object builder)
        {
            foreach (var profileType in profileAssemblies.SelectMany(assembly => assembly.GetTypes()))
            {
                if (profileType.IsAbstract ||
                    !profileType.GetInterfaces().Any(type => type.FullName == profileInterfaceName))
                {
                    continue;
                }

                var constructor = profileType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null);
                if (constructor == null)
                    continue;

                var configure = profileType
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .FirstOrDefault(method =>
                    {
                        if (method.Name != "Configure")
                            return false;

                        var parameters = method.GetParameters();
                        return parameters.Length == 1 && parameters[0].ParameterType.IsInstanceOfType(builder);
                    });

                if (configure == null)
                    continue;

                var profile = constructor.Invoke(null);
                configure.Invoke(profile, [ builder ]);
            }
        }

        private static ExceptionKind MapHttpStatusCode(HttpStatusCode statusCode)
        {
            return statusCode switch
            {
                HttpStatusCode.BadRequest => ExceptionKind.Validation,
                HttpStatusCode.Unauthorized => ExceptionKind.Unauthorized,
                HttpStatusCode.Forbidden => ExceptionKind.Forbidden,
                HttpStatusCode.NotFound => ExceptionKind.NotFound,
                HttpStatusCode.Conflict => ExceptionKind.Conflict,
                _ when (int)statusCode >= 500 => ExceptionKind.Transient,
                _ => ExceptionKind.Business
            };
        }
    }
}
