using Gnomes.Core.Level;
using Gnomes.Core.Protocol;
using Gnomes.NPC;
using Gnomes.Session;
using UnityEngine;

namespace Gnomes.World
{
    /// <summary>Builds a level identically on the host and on every client.</summary>
    public static class WorldLoader
    {
        public static GameWorld Build(LevelKind kind, int seed, bool authority, GameSession session)
        {
            var old = GameWorld.Current;
            if (old != null)
            {
                old.gameObject.SetActive(false);
                Object.Destroy(old.gameObject);
            }
            HouseBuilder.ClearNavMesh();
            var w = GameWorld.Create(kind, seed, authority, session);
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
