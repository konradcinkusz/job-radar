using JobRadar.Core.Ports;

namespace JobRadar.Sources.Http;

/// <summary>
/// The robots.txt guard for HTML hosts (SPEC R9, §6.3). Reads the <c>User-agent: *</c> group only. The longest matching rule wins;
/// on equal length Allow wins; no match allows. An unreachable robots.txt (an exception, or any status other than 200) allows.
/// Each host's robots.txt is fetched once and cached. Wildcards (<c>*</c>, <c>$</c>) are not interpreted: they are literal characters.
/// </summary>
public sealed class RobotsGuard
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Task<RobotsRules>> _cache = new(StringComparer.Ordinal); // guarded by _gate

    /// <summary>
    /// Checks <paramref name="url"/> against the host's robots.txt. The fetch is not cancelled by <paramref name="ct"/>, because the
    /// cached result is shared by later callers; <paramref name="ct"/> only stops this caller from waiting.
    /// </summary>
    public async Task<bool> IsAllowedAsync(Uri url, ISourceHttp http, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(http);

        RobotsRules rules = await RulesFor(url, http).WaitAsync(ct).ConfigureAwait(false);
        return rules.IsAllowed(url.PathAndQuery);
    }

    private Task<RobotsRules> RulesFor(Uri url, ISourceHttp http)
    {
        string key = url.GetLeftPart(UriPartial.Authority); // scheme://host[:port]
        lock (_gate)
        {
            if (!_cache.TryGetValue(key, out Task<RobotsRules>? cached))
            {
                cached = FetchAsync(new Uri(url, "/robots.txt"), http);
                _cache[key] = cached;
            }

            return cached;
        }
    }

    private static async Task<RobotsRules> FetchAsync(Uri robotsUrl, ISourceHttp http)
    {
        try
        {
            HttpResult result = await http.GetAsync(robotsUrl, CancellationToken.None).ConfigureAwait(false);
            return result.Status == 200 ? RobotsRules.Parse(result.Body) : RobotsRules.AllowAll;
        }
#pragma warning disable CA1031 // unreachable robots.txt allows (SPEC R9): any failure to fetch is an allow
        catch (Exception)
        {
            return RobotsRules.AllowAll;
        }
#pragma warning restore CA1031
    }

    private sealed record Rule(string Path, bool Allow);

    private sealed class RobotsRules
    {
        public static readonly RobotsRules AllowAll = new(Array.Empty<Rule>());

        private readonly IReadOnlyList<Rule> _rules;

        private RobotsRules(IReadOnlyList<Rule> rules) => _rules = rules;

        /// <summary>Keeps only the rules of groups whose <c>User-agent</c> lines include <c>*</c>. Consecutive User-agent lines form one group.</summary>
        public static RobotsRules Parse(string body)
        {
            var rules = new List<Rule>();
            bool inStarGroup = false;
            bool previousWasUserAgent = false;

            foreach (string rawLine in body.Split('\n'))
            {
                string line = rawLine;
                int hash = line.IndexOf('#', StringComparison.Ordinal);
                if (hash >= 0)
                {
                    line = line[..hash];
                }

                line = line.Trim();
                int colon = line.IndexOf(':', StringComparison.Ordinal);
                if (colon < 0)
                {
                    continue; // blank or not a directive
                }

                string name = line[..colon].Trim();
                string value = line[(colon + 1)..].Trim();

                if (name.Equals("User-agent", StringComparison.OrdinalIgnoreCase))
                {
                    if (!previousWasUserAgent)
                    {
                        inStarGroup = false; // a User-agent line after a rule starts a new group
                    }

                    if (value == "*")
                    {
                        inStarGroup = true;
                    }

                    previousWasUserAgent = true;
                    continue;
                }

                previousWasUserAgent = false;
                if (!inStarGroup || value.Length == 0)
                {
                    continue; // an empty Disallow allows everything, so it adds no rule
                }

                if (name.Equals("Disallow", StringComparison.OrdinalIgnoreCase))
                {
                    rules.Add(new Rule(value, Allow: false));
                }
                else if (name.Equals("Allow", StringComparison.OrdinalIgnoreCase))
                {
                    rules.Add(new Rule(value, Allow: true));
                }
            }

            return new RobotsRules(rules);
        }

        /// <summary>Longest matching rule wins; on equal length Allow wins; no match allows.</summary>
        public bool IsAllowed(string pathAndQuery)
        {
            Rule? best = null;
            foreach (Rule rule in _rules)
            {
                if (!pathAndQuery.StartsWith(rule.Path, StringComparison.Ordinal))
                {
                    continue;
                }

                if (best is null
                    || rule.Path.Length > best.Path.Length
                    || (rule.Path.Length == best.Path.Length && rule.Allow))
                {
                    best = rule;
                }
            }

            return best?.Allow ?? true;
        }
    }
}
