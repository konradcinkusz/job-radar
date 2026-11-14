using JobRadar.Core.Models;
using YamlDotNet.RepresentationModel;

namespace JobRadar.Config;

/// <summary>
/// Loads and validates the config files (SPEC §6.2). A bad file is never an exception: a missing file, invalid YAML
/// or a broken rule comes back as <see cref="ConfigError"/>s and <c>Value</c> is null.
/// Other loaders (blocklist, report) are added as further partial files.
/// </summary>
public static partial class ConfigLoader
{
    public static ConfigResult<IReadOnlyList<SourceConfig>> LoadSources(string path) =>
        Load(path, static (reader, root) => SourcesParser.Parse(reader, root));

    public static ConfigResult<Criteria> LoadCriteria(string path) =>
        Load(path, static (reader, root) => CriteriaParser.Parse(reader, root));

    internal static ConfigResult<T> Load<T>(string path, Func<ConfigReader, YamlMappingNode, T?> parse) where T : class
    {
        var reader = new ConfigReader(System.IO.Path.GetFileName(path));
        if (!File.Exists(path))
        {
            reader.Error(ConfigReader.RootPath, "file not found");
            return new ConfigResult<T>(null, reader.Errors);
        }

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            reader.Error(ConfigReader.RootPath, $"cannot read file: {e.Message}");
            return new ConfigResult<T>(null, reader.Errors);
        }

        var root = reader.Root(text);
        var value = root is null ? null : parse(reader, root);
        return value is not null && reader.Errors.Count == 0
            ? new ConfigResult<T>(value, [])
            : new ConfigResult<T>(null, reader.Errors);
    }
}
