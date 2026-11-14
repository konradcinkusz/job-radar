using System.Security.Cryptography;

namespace JobRadar.Config.Tests;

public sealed class CriteriaLockTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "jr-lock-" + Guid.NewGuid().ToString("N"));

    public CriteriaLockTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Lock_ok_when_nothing_changed()
    {
        var criteria = CopyCriteria(Real("criteria.yaml"));
        var lockPath = WriteLock(criteria, version: 1);

        Assert.Empty(CriteriaLock.Check(criteria, lockPath));
    }

    [Fact]
    public void Lock_mismatch_fails_when_content_changed_but_version_did_not()
    {
        var criteria = CopyCriteria(Real("criteria.yaml"));
        var lockPath = WriteLock(criteria, version: 1);
        File.WriteAllText(criteria, Mutate(File.ReadAllText(criteria), "threshold: 70", "threshold: 65"));

        var error = Assert.Single(CriteriaLock.Check(criteria, lockPath));
        Assert.Equal("criteria.yaml", error.File);
        Assert.Equal("version", error.Path);
        Assert.Equal("criteria.yaml changed but its version was not increased (R15)", error.Rule);
    }

    [Fact]
    public void Lock_ok_when_version_and_hash_both_changed()
    {
        var criteria = CopyCriteria(Real("criteria.yaml"));
        var lockPath = WriteLock(criteria, version: 1);
        File.WriteAllText(criteria, Mutate(File.ReadAllText(criteria), "threshold: 70", "threshold: 65")
            .Replace("version: 1\n", "version: 2\n", StringComparison.Ordinal));

        Assert.Empty(CriteriaLock.Check(criteria, lockPath));
    }

    [Fact]
    public void Real_lock_agrees_with_real_criteria()
    {
        Assert.Empty(CriteriaLock.Check(Real("criteria.yaml"), Real("criteria.lock")));
    }

    [Fact]
    public void Same_content_with_a_different_lock_version_is_an_error()
    {
        var criteria = CopyCriteria(Real("criteria.yaml"));
        var lockPath = Path.Combine(_dir, "criteria.lock");
        File.WriteAllText(lockPath, $"version: 2\nsha256: {Sha256(criteria)}\n");

        var error = Assert.Single(CriteriaLock.Check(criteria, lockPath));
        Assert.Equal("criteria.lock", error.File);
        Assert.Equal("version differs from the lock although the content is identical", error.Rule);
    }

    [Fact]
    public void Malformed_lock_is_an_error()
    {
        var criteria = CopyCriteria(Real("criteria.yaml"));
        var lockPath = Path.Combine(_dir, "criteria.lock");
        File.WriteAllText(lockPath, "version: 1\n");

        var error = Assert.Single(CriteriaLock.Check(criteria, lockPath));
        Assert.Equal("criteria.lock", error.File);
        Assert.StartsWith("malformed lock", error.Rule, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "JobRadar.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("JobRadar.sln not found above the test binaries");
    }

    private static string Real(string name) => Path.Combine(RepoRoot(), "config", name);

    private static string Mutate(string text, string from, string to)
    {
        Assert.Contains(from, text, StringComparison.Ordinal);
        return text.Replace(from, to, StringComparison.Ordinal);
    }

    private static string Sha256(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private string CopyCriteria(string source)
    {
        var path = Path.Combine(_dir, "criteria.yaml");
        File.Copy(source, path);
        return path;
    }

    private string WriteLock(string criteriaPath, int version)
    {
        var path = Path.Combine(_dir, "criteria.lock");
        File.WriteAllText(path, $"version: {version}\nsha256: {Sha256(criteriaPath)}\n");
        return path;
    }
}
