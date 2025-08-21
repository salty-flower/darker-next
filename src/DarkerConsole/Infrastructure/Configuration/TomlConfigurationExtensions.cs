using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace DarkerConsole.Infrastructure.Configuration;

/// <summary>
/// Extension methods for adding TOML configuration
/// </summary>
internal static class TomlConfigurationExtensions
{
    /// <summary>
    /// Adds a TOML configuration source to the builder
    /// </summary>
    public static IConfigurationBuilder AddTomlFile(
        this IConfigurationBuilder builder,
        string path,
        bool optional = false,
        bool reloadOnChange = true
    )
    {
        return builder.AddTomlFile(
            provider: null,
            path: path,
            optional: optional,
            reloadOnChange: reloadOnChange
        );
    }

    /// <summary>
    /// Adds a TOML configuration source to the builder
    /// </summary>
    public static IConfigurationBuilder AddTomlFile(
        this IConfigurationBuilder builder,
        IFileProvider? provider,
        string path,
        bool optional,
        bool reloadOnChange
    )
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("Path cannot be null or empty", nameof(path));

        return builder.AddTomlFile(source =>
        {
            source.FileProvider = provider;
            source.Path = path;
            source.Optional = optional;
            source.ReloadOnChange = reloadOnChange;
            source.ResolveFileProvider();
        });
    }

    /// <summary>
    /// Adds a TOML configuration source to the builder
    /// </summary>
    public static IConfigurationBuilder AddTomlFile(
        this IConfigurationBuilder builder,
        Action<TomlConfigurationSource>? configureSource
    )
    {
        return builder.Add(configureSource);
    }
}
