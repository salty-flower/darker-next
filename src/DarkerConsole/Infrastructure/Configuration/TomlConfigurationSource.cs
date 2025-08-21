using Microsoft.Extensions.Configuration;

namespace DarkerConsole.Infrastructure.Configuration;

/// <summary>
/// Configuration source for TOML files
/// </summary>
internal sealed class TomlConfigurationSource : FileConfigurationSource
{
    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);
        return new TomlConfigurationProvider(this);
    }
}
