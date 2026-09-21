using System;
using UnityEngine;

namespace FlagRush.Spike
{
    /// <summary>
    /// Knobs for the demo, so a viewer can push the numbers: <c>?agents=5000&amp;ghosts=200</c> on the web,
    /// <c>--agents=5000 --ghosts=200</c> for a native player. Parsed once, clamped to sane ranges.
    /// </summary>
    public static class SpikeConfig
    {
        public const int DefaultAgents = 150;
        public const int DefaultGhosts = 8;

        static int? _agents, _ghosts;

        public static int Agents => _agents ??= Read("agents", DefaultAgents, 1, 50000);
        public static int Ghosts => _ghosts ??= Read("ghosts", DefaultGhosts, 1, 2000);

        static int Read(string key, int fallback, int min, int max)
        {
            var raw = FromUrl(key) ?? FromArgs(key);
            return int.TryParse(raw, out var v) ? Mathf.Clamp(v, min, max) : fallback;
        }

        static string FromUrl(string key)
        {
            var url = Application.absoluteURL;
            var q = url?.IndexOf('?') ?? -1;
            if (q < 0) return null;
            foreach (var pair in url.Substring(q + 1).Split('&'))
            {
                var eq = pair.IndexOf('=');
                if (eq > 0 && string.Equals(pair.Substring(0, eq), key, StringComparison.OrdinalIgnoreCase))
                    return pair.Substring(eq + 1);
            }
            return null;
        }

        static string FromArgs(string key)
        {
            foreach (var arg in Environment.GetCommandLineArgs())
                if (arg.StartsWith("--" + key + "=", StringComparison.OrdinalIgnoreCase))
                    return arg.Substring(key.Length + 3);
            return null;
        }
    }
}
