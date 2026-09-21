using System;
using UnityEngine;

namespace FlagRush.Demo
{
    /// <summary>
    /// Startup knobs from the URL (<c>?level=2&amp;agents=5000&amp;ghosts=200</c>) or command line
    /// (<c>--level=2 --agents=5000</c>). Parsed once, clamped to sane ranges.
    /// </summary>
    public static class DemoConfig
    {
        public const int DefaultAgents = 150;
        public const int DefaultGhosts = 8;
        public const int MaxLevel = 2;

        static int? _agents, _ghosts, _level;

        public static int Level => _level ??= Read("level", 1, 1, MaxLevel);
        public static int Agents => _agents ??= Read("agents", DefaultAgents, 0, Sandbox.MaxAgents);
        public static int Ghosts => _ghosts ??= Read("ghosts", DefaultGhosts, 0, Sandbox.MaxGhosts);

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
