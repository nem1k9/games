using Gnomes.Core.Level;
using Gnomes.Core.Protocol;
using Godot;
using SockGang.NPC;
using SockGang.Session;

namespace SockGang.World
{
    /// <summary>Builds a level identically on the host and on every client.</summary>
    public static class WorldLoader
    {
        public static Node Parent; // set by the app

        public static GameWorld Build(LevelKind kind, int seed, bool authority, GameSession session)
        {
            var old = GameWorld.Current;
            if (old != null && GodotObject.IsInstanceValid(old))
            {
                old.GetParent()?.RemoveChild(old);
                old.QueueFree();
            }
            GameWorld.Current = null;
            var w = GameWorld.Create(Parent, kind, seed, authority, session);
            if (kind == LevelKind.House)
            {
                var layout = HouseLayout.Generate(seed);
                HouseBuilder.Build(w, layout);
                OldMan.Spawn(w, authority);
                Cat.Spawn(w, authority);
                Parrot.Attach(w, authority);
                Atmosphere.Night(w);
            }
            else
            {
                VillageBuilder.Build(w);
                Atmosphere.Village(w);
            }
            return w;
        }
    }
}
