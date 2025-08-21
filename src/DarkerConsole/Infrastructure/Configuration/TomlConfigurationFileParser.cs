using System;
using System.Collections.Generic;
using System.IO;
using Tomlyn;
using Tomlyn.Model;

namespace DarkerConsole.Infrastructure.Configuration;

/// <summary>
/// Parses TOML files into flat key-value pairs for .NET configuration system
/// </summary>
internal static class TomlConfigurationFileParser
{
    private static readonly Dictionary<string, string?> EmptyResult = new(
        StringComparer.OrdinalIgnoreCase
    );

    public static Dictionary<string, string?> Parse(Stream input)
    {
        try
        {
            using var reader = new StreamReader(input);
            var content = reader.ReadToEnd();

            if (string.IsNullOrWhiteSpace(content))
                return EmptyResult;

            var tomlModel = Toml.ToModel(content);
            if (tomlModel is not TomlTable rootTable)
                return EmptyResult;

            return FlattenTomlTable(rootTable);
        }
        catch
        {
            return EmptyResult;
        }
    }

    private static Dictionary<string, string?> FlattenTomlTable(TomlTable table, string prefix = "")
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in table)
        {
            var pascalKey = ToPascalCase(kvp.Key);
            var key = string.IsNullOrEmpty(prefix) ? pascalKey : $"{prefix}:{pascalKey}";

            switch (kvp.Value)
            {
                case TomlTable nestedTable:
                    // Recursively flatten nested tables
                    var nested = FlattenTomlTable(nestedTable, key);
                    foreach (var nestedKvp in nested)
                    {
                        result[nestedKvp.Key] = nestedKvp.Value;
                    }
                    break;

                case string stringValue:
                    result[key] = stringValue;
                    break;

                case bool boolValue:
                    result[key] = boolValue.ToString().ToLowerInvariant();
                    break;

                case long longValue:
                    result[key] = longValue.ToString();
                    break;

                case double doubleValue:
                    result[key] = doubleValue.ToString();
                    break;

                case DateTime dateTimeValue:
                    result[key] = dateTimeValue.ToString("O"); // ISO 8601 format
                    break;

                default:
                    // Convert any other type to string
                    result[key] = kvp.Value?.ToString();
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Converts snake_case to PascalCase for proper .NET configuration binding
    /// </summary>
    private static string ToPascalCase(string snakeCase)
    {
        if (string.IsNullOrEmpty(snakeCase))
            return snakeCase;

        var parts = snakeCase.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var result = new System.Text.StringBuilder();

        foreach (var part in parts)
        {
            if (part.Length > 0)
            {
                result.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1)
                    result.Append(part.AsSpan(1));
            }
        }

        return result.ToString();
    }
}
