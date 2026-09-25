namespace SockGang
{
    /// <summary>Physics layer bits (Godot collision_layer / collision_mask).</summary>
    public static class Layers
    {
        public const uint World = 1 << 0; // walls, floors, furniture colliders
        public const uint Gnome = 1 << 1; // remote gnome proxies
        public const uint Prop = 1 << 2; // loose items
        public const uint Npc = 1 << 3; // grandpa, the cat
        public const uint LocalGnome = 1 << 4; // the gnome controlled on this machine
        public const uint Mech = 1 << 5; // doors, lids, sashes

        /// <summary>What a gnome stands on / bumps into.</summary>
        public const uint SolidForGnome = World | Mech | Prop | Npc | Gnome;
        /// <summary>What the crosshair / hands / yarn can hit.</summary>
        public const uint Grabbable = World | Mech | Prop | Npc | Gnome;
        /// <summary>Static environment (line of sight).</summary>
        public const uint Solid = World | Mech;
        /// <summary>What loose items collide with.</summary>
        public const uint PropMask = World | Mech | Prop | Npc | Gnome | LocalGnome;
    }
}
