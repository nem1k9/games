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
        public const float SpringJumpSpeed = 8.8f;
        public const float CoyoteTime = 0.12f;
        public const float ArmMin = 0.6f;
        public const float ArmMax = 2.6f;
        public const float ArmMaxStretchy = 3.8f;
        public const float ArmMaxGrapple = 5.5f;
        public const float InteractRange = 2.6f;
        public const float HoldStrength = 95f; // newtons available to hold/drag objects
        public const float ClimbStrength = 130f; // newtons when hanging on static geometry (weight = 72)
        public const float ThrowSpeed = 10f;
        public const float PunchImpulse = 4f;
        public const float GripTime = 8f; // seconds of hanging before the grip gives out
        public const float ArmRegrowTime = 4f;
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
        public const int SporesSolo = 2;
        public const int SporesCoop = 3;
        public const float SoloReviveDelay = 4f;
        public const float OvenDamagePerSec = 4f;
        public const float FreezerDamagePerSec = 1.4f;
        public const int StruggleOven = 22;
        public const int StruggleFreezer = 28;
        public const int StruggleCarried = 12;
    }
}
