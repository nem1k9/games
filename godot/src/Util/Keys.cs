using System.Collections.Generic;
using Godot;

namespace SockGang
{
    /// <summary>
    /// Polled keyboard/mouse input with "pressed this frame" edges (like Unity's legacy Input).
    /// Mouse motion and wheel are accumulated from input events by <see cref="App.GameApp"/>.
    /// </summary>
    public static class Keys
    {
        static readonly HashSet<Key> down = new HashSet<Key>(), prev = new HashSet<Key>();
        static readonly HashSet<Key> watched = new HashSet<Key>();
        static bool mouse0, mouse1, prevMouse0, prevMouse1;
        public static Vector2 MouseDelta;
        public static float Wheel;
        static Vector2 mouseAcc;
        static float wheelAcc;

        /// <summary>Call once per frame before gameplay code.</summary>
        public static void Update()
        {
            prev.Clear();
            foreach (var k in down) prev.Add(k);
            down.Clear();
            foreach (var k in watched) if (Input.IsPhysicalKeyPressed(k)) down.Add(k);
            prevMouse0 = mouse0;
            prevMouse1 = mouse1;
            mouse0 = Input.IsMouseButtonPressed(MouseButton.Left);
            mouse1 = Input.IsMouseButtonPressed(MouseButton.Right);
            MouseDelta = mouseAcc;
            mouseAcc = Vector2.Zero;
            Wheel = wheelAcc;
            wheelAcc = 0;
        }

        public static void OnInput(InputEvent e)
        {
            if (e is InputEventMouseMotion mm) mouseAcc += mm.Relative;
            else if (e is InputEventMouseButton mb && mb.Pressed)
            {
                if (mb.ButtonIndex == MouseButton.WheelUp) wheelAcc += 1;
                else if (mb.ButtonIndex == MouseButton.WheelDown) wheelAcc -= 1;
            }
        }

        /// <summary>Keys held down by the automated playtest (no real keyboard in a headless run).</summary>
        public static readonly HashSet<Key> Simulated = new HashSet<Key>();

        public static bool Held(Key k)
        {
            watched.Add(k);
            return Input.IsPhysicalKeyPressed(k) || Simulated.Contains(k);
        }

        public static bool Pressed(Key k)
        {
            if (watched.Add(k)) return false; // first time we see it: no edge info yet
            return down.Contains(k) && !prev.Contains(k);
        }

        public static bool MouseDown(int b) => b == 0 ? mouse0 && !prevMouse0 : mouse1 && !prevMouse1;
        public static bool MouseHeld(int b) => b == 0 ? mouse0 : mouse1;

        /// <summary>Register keys up front so their first press is not missed.</summary>
        public static void Watch(params Key[] keys)
        {
            foreach (var k in keys) watched.Add(k);
        }
    }
}
