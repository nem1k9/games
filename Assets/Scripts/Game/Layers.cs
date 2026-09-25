using UnityEngine;

namespace Gnomes
{
    /// <summary>Physics layers (indices match ProjectSettings/TagManager.asset; used numerically).</summary>
    public static class Layers
    {
        public const int Default = 0;
        public const int IgnoreRaycast = 2;
        public const int Gnome = 8; // remote gnomes (kinematic proxies) and host-side gnome bodies
        public const int Prop = 9; // loose physics items
        public const int NPC = 10; // old man, cat, roomba
        public const int LocalGnome = 11; // the locally controlled gnome
        public const int Trigger = 12;
        public const int Debris = 13;
        public const int Mech = 14; // doors / moving furniture parts

        public static int Mask(params int[] layers)
        {
            int m = 0;
            foreach (var l in layers) m |= 1 << l;
            return m;
        }

        /// <summary>Everything a gnome can stand on / bump into.</summary>
        public static readonly int SolidForGnome = Mask(Default, Prop, NPC, Mech, Gnome);
        /// <summary>What the crosshair / grab ray can hit.</summary>
        public static readonly int Grabbable = Mask(Default, Prop, Mech, NPC, Gnome);
        public static readonly int World = Mask(Default, Mech);

        /// <summary>Configure the collision matrix once at startup.</summary>
        public static void SetupCollisionMatrix()
        {
            Physics.IgnoreLayerCollision(Trigger, Trigger, true);
            Physics.IgnoreLayerCollision(Debris, Gnome, true);
            Physics.IgnoreLayerCollision(Debris, LocalGnome, true);
            Physics.IgnoreLayerCollision(Debris, NPC, true);
            Physics.IgnoreLayerCollision(Debris, Debris, true);
            for (int i = 0; i < 32; i++)
            {
                // triggers only interact through OnTrigger queries of solid layers
                if (i != Prop && i != Gnome && i != LocalGnome && i != NPC) Physics.IgnoreLayerCollision(Trigger, i, true);
            }
        }
    }
}
