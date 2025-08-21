using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace DarkerConsole.Infrastructure.Configuration;

/// <summary>
/// Configuration provider for TOML files with hot reloading support
/// </summary>
internal sealed class TomlConfigurationProvider(TomlConfigurationSource source)
    : FileConfigurationProvider(source)
{
    public override void Load(Stream stream)
    {
        try
        {
            var data = TomlConfigurationFileParser.Parse(stream);
            Data = data;
        }
        catch (Exception ex)
        {
            throw new FormatException($"Could not parse the TOML file. Error: {ex.Message}", ex);
        }
    }
}
