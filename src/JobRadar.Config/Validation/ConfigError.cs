namespace JobRadar.Config;

/// <summary>One loading or validation problem: the file name, the dotted path of the offending key and the rule broken.</summary>
public sealed record ConfigError(string File, string Path, string Rule);

/// <summary>Outcome of loading one file. <c>Value</c> is null whenever there is any error.</summary>
public sealed record ConfigResult<T>(T? Value, IReadOnlyList<ConfigError> Errors)
{
    public bool Ok => Errors.Count == 0;
}
