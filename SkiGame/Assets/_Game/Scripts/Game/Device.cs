using System.Runtime.InteropServices;
using UnityEngine;

namespace Ski.Game
{
    /// <summary>
    /// What the game is played on: a finger (a phone or a tablet: the touch controls) or a keyboard and
    /// mouse. "?touch=1" or "?touch=0" in the page address decides it by hand (for trying the touch
    /// controls on a computer); <see cref="ForceTouch"/> does in the Editor.
    /// </summary>
    public static class Device
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int LRCoarsePointer();
        [DllImport("__Internal")] static extern int LRShortSide();
#else
        static int LRCoarsePointer() => 0;
        static int LRShortSide() => 1000;
#endif

        /// <summary>For trying the touch controls in the Editor.</summary>
        public static bool ForceTouch;

        static int _touch = -1;

        /// <summary>Played with a finger: the touch controls are shown and the keyboard hints are not.</summary>
        public static bool Touch
        {
            get
            {
                if (ForceTouch) return true;
                if (_touch < 0)
                {
                    string arg = PageArgs.Get("touch");
                    _touch = arg == "1" ? 1 : arg == "0" ? 0 : LRCoarsePointer();
                }
                return _touch == 1;
            }
        }

        /// <summary>A touch device with a big screen (a tablet): it gets more of the graphics, and the interface at its usual size.</summary>
        public static bool Tablet => Touch && LRShortSide() >= 700;

        /// <summary>A phone: the lightest graphics and a larger interface.</summary>
        public static bool Phone => Touch && !Tablet;
    }
}
