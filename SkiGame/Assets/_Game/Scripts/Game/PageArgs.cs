using System;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// Options in the page address, for trying things out with more than one browser tab:
    /// "?profile=b" signs in as a separate player, "?room=test" plays in a private room of that name
    /// (nothing goes to the world boards from there), "?echo=1" sends every player's movement back
    /// to them from this tab, a little to the side (a stand-in to race beside).
    /// </summary>
    public static class PageArgs
    {
        /// <summary>The value of key=... in the address (letters, digits, '_' and '-'), or null.</summary>
        public static string Get(string key)
        {
            string url = Application.absoluteURL ?? "";
            int q = url.IndexOf('?');
            if (q < 0) return null;
            int i = url.IndexOf(key + "=", q, StringComparison.Ordinal);
            if (i < 0 || (url[i - 1] != '?' && url[i - 1] != '&')) return null;
            var sb = new System.Text.StringBuilder();
            for (int k = i + key.Length + 1; k < url.Length && sb.Length < 20; k++)
            {
                char c = url[k];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-') sb.Append(c); else break;
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        /// <summary>A private room to try things in ("?room=name"), or null for the shared world.</summary>
        public static string TestRoom => Get("room");
    }
}
