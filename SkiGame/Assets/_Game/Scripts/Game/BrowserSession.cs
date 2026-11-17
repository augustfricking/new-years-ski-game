using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Ski.Game
{
    /// <summary>
    /// Yes/no flags that last for this browser tab (its sessionStorage): kept through a reload,
    /// gone in a new tab. Outside the browser they last until the game closes.
    /// </summary>
    public static class BrowserSession
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int LastRunSessionGet(string key);
        [DllImport("__Internal")] static extern void LastRunSessionSet(string key);
#endif
        static readonly HashSet<string> _here = new HashSet<string>();

        public static bool Has(string key)
        {
            if (_here.Contains(key)) return true;
#if UNITY_WEBGL && !UNITY_EDITOR
            try { return LastRunSessionGet(key) == 1; } catch { return false; }
#else
            return false;
#endif
        }

        /// <summary>Forgets a flag on this side (for replaying things while developing).</summary>
        public static void Forget(string key) => _here.Remove(key);

        public static void Set(string key)
        {
            _here.Add(key);
#if UNITY_WEBGL && !UNITY_EDITOR
            try { LastRunSessionSet(key); } catch { }
#endif
        }
    }
}
