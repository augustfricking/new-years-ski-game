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
        [DllImport("__Internal")] static extern int LRFullscreenKind();
        [DllImport("__Internal")] static extern int LRFullscreenWanted();
        [DllImport("__Internal")] static extern void LRSetFullscreenWanted(int on);
#else
        static int LRCoarsePointer() => 0;
        static int LRShortSide() => EditorShortSide;
        static int LRFullscreenKind() => 2;
        static int _editorFull = 1;
        static int LRFullscreenWanted() => _editorFull;
        static void LRSetFullscreenWanted(int on) => _editorFull = on;
#endif

        /// <summary>The page can take the whole screen by itself (not in an iPhone's Safari).</summary>
        public static bool CanFullscreen => LRFullscreenKind() == 2;
        /// <summary>Running from the home screen already, without the browser's bars.</summary>
        public static bool FromHomeScreen => LRFullscreenKind() == 1;

        /// <summary>On a finger device the game takes the whole screen at the first tap (see index.html), unless turned off.</summary>
        public static bool Fullscreen
        {
            get => LRFullscreenWanted() == 1;
            set => LRSetFullscreenWanted(value ? 1 : 0);
        }

        /// <summary>For trying the touch controls in the Editor.</summary>
        public static bool ForceTouch;
        /// <summary>The Editor's pretend screen, its short side in page pixels (under 700: a phone, with <see cref="ForceTouch"/>).</summary>
        public static int EditorShortSide = 1000;

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

        /// <summary>A phone: a larger interface, and the ski map held closer.</summary>
        public static bool Phone => Touch && !Tablet;
    }
}
