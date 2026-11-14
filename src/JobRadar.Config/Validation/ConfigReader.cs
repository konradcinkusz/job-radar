using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace JobRadar.Config;

/// <summary>
/// Checks one YAML document by hand and records every problem with its file, path and rule (SPEC §6.2).
/// A key that is absent or written as a YAML null counts as absent.
/// </summary>
internal sealed class ConfigReader(string file)
{
    public const string RootPath = "(root)";

    private readonly List<ConfigError> _errors = [];

    public IReadOnlyList<ConfigError> Errors => _errors;

    public void Error(string path, string rule) => _errors.Add(new ConfigError(file, path, rule));

    /// <summary>Joins a parent path and a key; the top level has the empty parent.</summary>
    public static string Join(string parent, string key) => parent.Length == 0 ? key : $"{parent}.{key}";

    /// <summary>Parses the text and returns its root mapping, or null after recording the error.</summary>
    public YamlMappingNode? Root(string text)
    {
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(text));
        }
        catch (Exception e) when (e is YamlException or ArgumentException)
        {
            Error(RootPath, $"invalid YAML: {e.Message}");
            return null;
        }

        if (stream.Documents.Count != 1)
        {
            Error(RootPath, "file must hold exactly one YAML document");
            return null;
        }

        if (stream.Documents[0].RootNode is YamlMappingNode map)
        {
            return map;
        }

        Error(RootPath, "must be a mapping");
        return null;
    }

    public void UnknownKeys(YamlMappingNode map, string path, IReadOnlyCollection<string> allowed)
    {
        foreach (var key in map.Children.Keys)
        {
            var name = key is YamlScalarNode { Value: not null } scalar ? scalar.Value : null;
            if (name is null || !allowed.Contains(name))
            {
                Error(Join(path, name ?? "(non-scalar key)"), "unknown key");
            }
        }
    }

    public string? Key(YamlNode key, string path)
    {
        if (key is YamlScalarNode { Value: not null } scalar)
        {
            return scalar.Value;
        }

        Error(path, "key must be a plain string");
        return null;
    }

    public YamlMappingNode? Mapping(YamlNode node, string path)
    {
        if (node is YamlMappingNode map)
        {
            return map;
        }

        Error(path, "must be a mapping");
        return null;
    }

    /// <summary>The value under <paramref name="key"/>, or null after recording "required key is missing".</summary>
    public YamlNode? Node(YamlMappingNode map, string path, string key, bool required)
    {
        if (map.Children.TryGetValue(new YamlScalarNode(key), out var node) && !IsNull(node))
        {
            return node;
        }

        if (required)
        {
            Error(Join(path, key), "required key is missing");
        }

        return null;
    }

    public bool? Bool(YamlMappingNode map, string path, string key, bool required)
    {
        if (Node(map, path, key, required) is not { } node)
        {
            return null;
        }

        if (node is YamlScalarNode { Style: ScalarStyle.Plain } scalar && bool.TryParse(scalar.Value, out var value))
        {
            return value;
        }

        Error(Join(path, key), "must be true or false");
        return null;
    }

    public int? Int(YamlMappingNode map, string path, string key, bool required, int min, int max, string rule)
    {
        if (Node(map, path, key, required) is not { } node)
        {
            return null;
        }

        if (node is YamlScalarNode { Style: ScalarStyle.Plain } scalar
            && int.TryParse(scalar.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            && value >= min && value <= max)
        {
            return value;
        }

        Error(Join(path, key), rule);
        return null;
    }

    public string? String(YamlMappingNode map, string path, string key, bool required)
    {
        if (Node(map, path, key, required) is not { } node)
        {
            return null;
        }

        if (node is YamlScalarNode { Value: not null } scalar)
        {
            return scalar.Value;
        }

        Error(Join(path, key), "must be a string");
        return null;
    }

    /// <summary>A string that must match <paramref name="rule"/>; the rule text is the pattern itself.</summary>
    public string? Matching(YamlMappingNode map, string path, string key, bool required, Regex rule)
    {
        var value = String(map, path, key, required);
        if (value is null)
        {
            return null;
        }

        if (rule.IsMatch(value))
        {
            return value;
        }

        Error(Join(path, key), $"must match {rule}");
        return null;
    }

    /// <summary>The value under <paramref name="key"/> as an enum; the names are the snake_case names of the members.</summary>
    public T? EnumValue<T>(YamlMappingNode map, string path, string key) where T : struct, Enum
    {
        if (Node(map, path, key, required: true) is not { } node)
        {
            return null;
        }

        return Enumerated<T>(node, Join(path, key));
    }

    public T? Enumerated<T>(YamlNode node, string path) where T : struct, Enum
    {
        var names = Enum.GetValues<T>()
            .ToDictionary(v => JsonNamingPolicy.SnakeCaseLower.ConvertName(v.ToString()), v => v);
        if (node is YamlScalarNode { Value: not null } scalar && names.TryGetValue(scalar.Value, out var value))
        {
            return value;
        }

        Error(path, $"must be one of {string.Join(", ", names.Keys)}");
        return null;
    }

    /// <summary>The items of a list under <paramref name="key"/>; empty when the key is absent.</summary>
    public IReadOnlyList<YamlNode> Sequence(YamlMappingNode map, string path, string key, bool required)
    {
        if (Node(map, path, key, required) is not { } node)
        {
            return [];
        }

        if (node is YamlSequenceNode sequence)
        {
            return sequence.Children.ToList();
        }

        Error(Join(path, key), "must be a list");
        return [];
    }

    /// <summary>A list of strings; each item must match <paramref name="item"/> when given.</summary>
    public IReadOnlyList<string> Strings(YamlMappingNode map, string path, string key, bool required, Regex? item = null)
    {
        var items = Sequence(map, path, key, required);
        var result = new List<string>();
        for (var i = 0; i < items.Count; i++)
        {
            var itemPath = $"{Join(path, key)}[{i}]";
            if (items[i] is not YamlScalarNode { Value: not null } scalar)
            {
                Error(itemPath, "must be a string");
            }
            else if (item is not null && !item.IsMatch(scalar.Value))
            {
                Error(itemPath, $"must match {item}");
            }
            else
            {
                result.Add(scalar.Value);
            }
        }

        return result;
    }

    /// <summary>A list of enum names; invalid items are recorded and skipped.</summary>
    public IReadOnlyList<T> EnumList<T>(YamlMappingNode map, string path, string key) where T : struct, Enum
    {
        var items = Sequence(map, path, key, required: false);
        var result = new List<T>();
        for (var i = 0; i < items.Count; i++)
        {
            if (Enumerated<T>(items[i], $"{Join(path, key)}[{i}]") is { } value)
            {
                result.Add(value);
            }
        }

        return result;
    }

    private static bool IsNull(YamlNode node) =>
        node is YamlScalarNode { Style: ScalarStyle.Plain } scalar && scalar.Value is null or "" or "~" or "null";
}
