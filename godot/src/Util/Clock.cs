namespace SockGang
{
    /// <summary>Game time in seconds (like Unity's Time.time), advanced by the app every frame.</summary>
    public static class Clock
    {
        public static float Now;
        public static float Dt = 1f / 60f;

        public static void Advance(double delta)
        {
            Dt = (float)delta;
            Now += Dt;
        }
    }
}
