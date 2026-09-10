namespace Krackend.Sagas.Orchestrations.Runtime.ButterMorph;

using System.Text;

/// <summary>
/// Default ButterMorph alias formatter for orchestration keys.
/// </summary>
public sealed class ButterMorphAliasNameFormatter : IButterMorphAliasNameFormatter
{
    /// <inheritdoc />
    public string Format(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "item";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        var formatted = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(formatted) ? "item" : formatted;
    }
}
