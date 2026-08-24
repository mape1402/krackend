namespace Krackend.Sagas.Orchestrations.ControlPlane.Storage.EntityFramework.Design.JsonModels;

/// <summary>
/// Represents DslConditionConfigurationJsonModel.
/// </summary>
public sealed class DslConditionConfigurationJsonModel : ConditionConfigurationJsonModel
{
    /// <summary>
    /// Gets or sets Expression.
    /// </summary>
    public string Expression { get; set; }

    /// <summary>
    /// Supports legacy camelCase payloads.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("expression")]
    public string LegacyExpression
    {
        set
        {
            if (string.IsNullOrWhiteSpace(Expression))
            {
                Expression = value;
            }
        }
    }
}
