namespace Gnomes.Core
{
    /// <summary>Global tuning. World units: a gnome is ~1 unit tall; human furniture is real metres x HS.</summary>
    public static class GameConsts
    {
        public const float HS = 4f;
        public const float Gravity = -24f;
        public const int DefaultPort = 27777;
        public const int MaxPlayers = 6;
        public const int SnapshotHz = 20;
        public const int ClientStateHz = 30;
        public const string GameVersion = "0.1.0";

        // ---------- gnome ----------
        public const float GnomeRadius = 0.28f;
        public const float GnomeHeight = 1.0f;
        public const float GnomeCrouchHeight = 0.7f;
        public const float GnomeEye = 0.84f;
        public const float GnomeEyeCrouch = 0.55f;
        public const float GnomeShoulder = 0.62f;
        public const float GnomeMass = 3f;
        public const float WalkSpeed = 4.0f;
        public const float SprintSpeed = 6.4f;
        public const float CrouchSpeed = 2.0f;
        public const float GroundAccel = 45f;
        public const float AirAccel = 12f;
        public const float JumpSpeed = 6.3f;
        public const float HopperJumpSpeed = 8.8f;
        public const float GlideFallSpeed = 1.6f;
        public const float CoyoteTime = 0.12f;
        public const float HandReach = 1.5f; // how far the hands reach to grab things
        public const float HoldDistance = 0.9f; // carried things float in front of the gnome
        public const float YarnMin = 0.5f; // shortest yarn when reeling in
        public const float YarnRange = 8f; // yarn hook throw range (world units)
        public const float YarnRangeLong = 12f;
        public const float YarnRangeEndless = 17f;
        public const float InteractRange = 2.6f;
        /// <summary>Reach for fridge doors, taps, flush buttons: a bit more generous so a 25 cm gnome can hop and tug.</summary>
        public const float MechReach = 3.4f;
        public const float HoldStrength = 95f; // newtons available to hold/drag objects
        public const float ClimbStrength = 130f; // yarn tension when climbing (weight = 72)
        public const float YarnPullStrength = 70f; // pulling things on the yarn
        public const float ThrowSpeed = 10f;
        public const float PunchImpulse = 4f;
        public const float GripTime = 14f; // seconds of hanging on the yarn before the gnome gets tired
        public const float ClimbSpeed = 2.6f; // climbing up/down the yarn (W/S), units per second
        public const float YarnSnapTime = 3f; // a snapped yarn takes this long to re-spool
        public const int PocketSize = 3;
        public const float MaxHealth = 100f;

        // ---------- old man ----------
        public const float OldManRadius = 1.1f;
        public const float OldManHeight = 7f;
        public const float OldManEye = 6.3f;
        public const float OldManWalk = 3.2f;
        public const float OldManSearch = 4.0f;
        public const float OldManRun = 7.0f;
        public const float OldManReach = 2.7f;
        public const float SwatterReach = 4.2f; // fly swatter reach (flattens gnomes he can't grab)
        public const float OldManGrabMaxHeight = 8.8f;
        public const float OldManFovDeg = 115f;
        public const float OldManViewDist = 34f;
        /// <summary>Gnomes with a ceiling lower than this above them are unreachable (under beds/sofas).</summary>
        public const float HideCeiling = 1.6f;

        // ---------- night ----------
        public const float NightSeconds = 8 * 60f;
        public const int TasksPerNight = 5;
        public const int TasksRequired = 3;
        public const int MaxStrikes = 3;
        public const int SpareHatsSolo = 2;
        public const int SpareHatsCoop = 0; // in co-op, friends carry your hat to the yarn basket
        public const float SoloReviveDelay = 4f;
        public const float JarAirSeconds = 75f; // time a gnome can stay in a pickle jar before fainting
        public const float JarUnscrewSeconds = 1.6f; // holding E on the lid
        public const int StruggleJar = 26; // wobbles to tip the jar off the shelf
        public const int StruggleCarried = 12;
    }
}
