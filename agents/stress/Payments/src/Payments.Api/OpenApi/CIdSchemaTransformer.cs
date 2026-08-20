namespace Payments.Api.OpenApi
{
    using Microsoft.AspNetCore.OpenApi;
    using Microsoft.OpenApi;
    using TurtlePath.Domain.Identifier;

    /// <summary>
    /// Adjusts the schema for <see cref="CId"/> values in generated OpenAPI documents.
    /// </summary>
    public sealed class CIdSchemaTransformer : IOpenApiSchemaTransformer
    {
        /// <inheritdoc />
        public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
        {
            if (context.JsonTypeInfo.Type == typeof(CId) && schema is OpenApiSchema openApiSchema)
            {
                openApiSchema.Type = JsonSchemaType.String;
                openApiSchema.Format = "string";
                openApiSchema.Pattern = string.Empty;
                openApiSchema.Example = Ulid.NewUlid().ToString();
            }

            return Task.CompletedTask;
        }
    }
}
