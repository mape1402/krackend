using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using TurtlePath.Domain.Identifier.Json;
using TurtlePath.ExceptionHandling.AspNetCore;

namespace Microsoft.Extensions.DependencyInjection
{
    [ExcludeFromCodeCoverage]
    internal static class MvcExtensions
    {
        internal static IServiceCollection AddMvcDefaults(this IServiceCollection services)
        {
            services.AddRouting(opts => opts.LowercaseUrls = true);
            services.AddControllers(opts =>
            {
                opts.Filters.Add<GlobalExceptionFilter>();
            });

            services.Configure<MvcOptions>(opts =>
            {
                opts.Conventions.Add(new RoutePrefixConvention(new RouteAttribute("api/v{version:apiVersion}")));
            });

            services.AddApiVersioning(opts =>
            {
                opts.DefaultApiVersion = new ApiVersion(1, 0);
                opts.AssumeDefaultVersionWhenUnspecified = true;
                opts.ReportApiVersions = true;
            })
            .AddMvc()
            .AddApiExplorer(opts =>
            {
                opts.GroupNameFormat = "'v'VVV";
                opts.SubstituteApiVersionInUrl = true;
            });

            services.Configure<JsonOptions>(opts =>
            {
                // Configure JSON options here if needed
                // For example, to use camel case naming:
                opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                opts.JsonSerializerOptions.AddTurtlePathCIdConverters();
                opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            return services.AddExceptionHandlingDefaults();
        }
    }

    /// <summary>
    /// An MVC application model convention that applies a global route prefix to all controllers.
    /// </summary>
    public class RoutePrefixConvention : IApplicationModelConvention
    {
        private readonly AttributeRouteModel RoutePrefix;

        /// <summary>
        /// Initializes a new instance of the <see cref="RoutePrefixConvention"/> class with the specified route dtemplate provider.
        /// </summary>
        /// <param name="route">The route dtemplate provider to use as a prefix.</param>
        public RoutePrefixConvention(IRouteTemplateProvider route) => RoutePrefix = new AttributeRouteModel(route);

        /// <summary>
        /// Applies the route prefix to all controllers in the application model.
        /// </summary>
        /// <param name="application">The application model to modify.</param>
        public void Apply(ApplicationModel application)
        {
            foreach (var selector in application.Controllers.SelectMany(c => c.Selectors))
            {
                if (selector.AttributeRouteModel != null)
                    selector.AttributeRouteModel = AttributeRouteModel.CombineAttributeRouteModel(RoutePrefix, selector.AttributeRouteModel);
                else
                    selector.AttributeRouteModel = RoutePrefix;
            }
        }
    }
}
