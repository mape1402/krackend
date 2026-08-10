namespace Krackend.Sagas.Orchestrations.Design.Storage.SqlServer.JsonModels;

/// <summary>
/// Represents HttpTaskConfigurationJsonModel.
/// </summary>
public sealed class HttpTaskConfigurationJsonModel : TaskConfigurationJsonModel
{
    /// <summary>
    /// Gets or sets SchemaBinding.
    /// </summary>
    public SchemaBindingJsonModel SchemaBinding { get; set; }
    /// <summary>
    /// Gets or sets BaseUrlVariableRef.
    /// </summary>
    public string BaseUrlVariableRef { get; set; }
    /// <summary>
    /// Gets or sets RelativePath.
    /// </summary>
    public string RelativePath { get; set; }
    /// <summary>
    /// Gets or sets Method.
    /// </summary>
    public string Method { get; set; }
    /// <summary>
    /// Gets or sets HeadersTemplateJson.
    /// </summary>
    public string HeadersTemplateJson { get; set; }
    /// <summary>
    /// Gets or sets QueryTemplateJson.
    /// </summary>
    public string QueryTemplateJson { get; set; }
    /// <summary>
    /// Gets or sets ExpectedStatusCodes.
    /// </summary>
    public List<int> ExpectedStatusCodes { get; set; } = new();
    /// <summary>
    /// Gets or sets AllowSyncResponse.
    /// </summary>
    public bool AllowSyncResponse { get; set; }
}
