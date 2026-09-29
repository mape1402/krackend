namespace Krackend.Sagas.Orchestrations.ControlPlane.WebUI.Design.ButterMorph;

using global::ButterMorph.Web.Razor;
using Microsoft.Extensions.Options;

/// <summary>
/// Configures ButterMorph designer theme options from the orchestration design Web UI theme.
/// </summary>
internal sealed class ConfigureButterMorphDesignerThemeOptions : IConfigureOptions<ButterMorphRazorDesignerOptions>
{
    private readonly IOptions<OrchestratorDesignWebUIOptions> _designOptions;
    private readonly IOrchestrationButterMorphThemeMapper _themeMapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigureButterMorphDesignerThemeOptions"/> class.
    /// </summary>
    /// <param name="designOptions">Design Web UI options.</param>
    /// <param name="themeMapper">ButterMorph theme mapper.</param>
    public ConfigureButterMorphDesignerThemeOptions(
        IOptions<OrchestratorDesignWebUIOptions> designOptions,
        IOrchestrationButterMorphThemeMapper themeMapper)
    {
        _designOptions = designOptions ?? throw new ArgumentNullException(nameof(designOptions));
        _themeMapper = themeMapper ?? throw new ArgumentNullException(nameof(themeMapper));
    }

    /// <inheritdoc />
    public void Configure(ButterMorphRazorDesignerOptions options)
    {
        if (options is null)
        {
            return;
        }

        _themeMapper.Apply(_designOptions.Value.Theme, options.Theme);
    }
}
