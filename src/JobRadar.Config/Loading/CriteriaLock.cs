using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace JobRadar.Config;

/// <summary>
/// R15: <c>config/criteria.lock</c> holds the SHA-256 of the raw bytes of <c>criteria.yaml</c> and the version it belongs to.
/// The check fails when the content changed and the version did not.
/// </summary>
public static class CriteriaLock
{
    private static readonly Regex VersionLine = new(@"^version: ([1-9][0-9]{0,8})$", RegexOptions.CultureInvariant);
    private static readonly Regex HashLine = new(@"^sha256: ([0-9a-f]{64})$", RegexOptions.CultureInvariant);

    /// <summary>Returns no errors when the lock agrees with the criteria file; otherwise the errors, each naming the wrong file.</summary>
    public static IReadOnlyList<ConfigError> Check(string criteriaPath, string lockPath)
    {
        var criteriaFile = System.IO.Path.GetFileName(criteriaPath);
        var lockFile = System.IO.Path.GetFileName(lockPath);
        var criteria = ConfigLoader.LoadCriteria(criteriaPath);
        var errors = new List<ConfigError>(criteria.Errors);
        var lockData = ReadLock(lockPath, lockFile, errors);
        if (errors.Count > 0 || criteria.Value is null || lockData is null)
        {
            return errors;
        }

        var (lockVersion, lockHash) = lockData.Value;
        var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(criteriaPath)));
        var sameContent = hash == lockHash;
        var sameVersion = criteria.Value.Version == lockVersion;

        if (sameContent && !sameVersion)
        {
            errors.Add(new ConfigError(lockFile, "version", "version differs from the lock although the content is identical"));
        }
        else if (!sameContent && sameVersion)
        {
            errors.Add(new ConfigError(criteriaFile, "version", "criteria.yaml changed but its version was not increased (R15)"));
        }

        return errors;
    }

    private static (int Version, string Hash)? ReadLock(string lockPath, string lockFile, List<ConfigError> errors)
    {
        if (!File.Exists(lockPath))
        {
            errors.Add(new ConfigError(lockFile, ConfigReader.RootPath, "file not found; run scripts/make-criteria-lock.sh"));
            return null;
        }

        var lines = File.ReadAllLines(lockPath);
        if (lines.Length == 2 && VersionLine.Match(lines[0]) is { Success: true } version
            && HashLine.Match(lines[1]) is { Success: true } hash)
        {
            return (int.Parse(version.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), hash.Groups[1].Value);
        }

        errors.Add(new ConfigError(lockFile, ConfigReader.RootPath,
            "malformed lock: expected exactly two lines 'version: N' and 'sha256: <64 lowercase hex>'"));
        return null;
    }
}
