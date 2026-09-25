using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Gnomes.Core;
using Gnomes.Core.Level;

// Usage: dotnet run --project tools/LevelDump -- <seed> <out.json>
static class Program
{
    static float[] V(V3 v) => new[] { v.x, v.y, v.z };

    static int Main(string[] args)
    {
        int seed = args.Length > 0 ? int.Parse(args[0]) : 12345;
        string outPath = args.Length > 1 ? args[1] : "layout.json";
        var L = HouseLayout.Generate(seed);
        var rooms = new List<object>();
        foreach (var r in L.Rooms)
            rooms.Add(new { id = r.Id, x0 = r.X0, z0 = r.Z0, x1 = r.X1, z1 = r.Z1, floor = r.Floor.ToString(), wall = r.Wall.ToString() });
        var walls = new List<object>();
        foreach (var w in L.Walls)
        {
            var solids = new List<float[]>();
            foreach (var s in w.Solids(L.WallHeight)) solids.Add(new[] { s.t0, s.t1, s.y0, s.y1 });
            var openings = new List<object>();
            foreach (var o in w.Openings) openings.Add(new { kind = o.Kind.ToString(), t0 = o.T0, t1 = o.T1, y0 = o.Y0, y1 = o.Y1 });
            walls.Add(new { id = w.Id, ax = w.Ax, az = w.Az, bx = w.Bx, bz = w.Bz, thick = w.Thick, exterior = w.Exterior, solids, openings });
        }
        var furniture = new List<object>();
        foreach (var p in L.Furniture)
            furniture.Add(new { id = p.Id, model = p.Model, pos = V(p.Pos), rotY = p.RotY, room = p.Room, prms = p.Params });
        var garden = new List<object>();
        foreach (var d in L.Garden) garden.Add(new { model = d.Model, pos = V(d.Pos), rotY = d.RotY, scale = d.Scale });
        var items = new List<object>();
        foreach (var i in L.Items)
            items.Add(new { kind = i.Kind, furniture = i.Furniture, surface = i.Surface, room = i.Room, floorPos = i.FloorPos.HasValue ? V(i.FloorPos.Value) : null });
        var spots = new Dictionary<string, float[]>();
        foreach (var kv in L.Spots) spots[kv.Key] = V(kv.Value);
        var spawns = new List<float[]>();
        foreach (var s in L.Spawns) spawns.Add(V(s));
        var doc = new
        {
            seed,
            hs = GameConsts.HS,
            wallHeight = L.WallHeight,
            houseMin = V(L.HouseMin), houseMax = V(L.HouseMax), worldMin = V(L.WorldMin), worldMax = V(L.WorldMax),
            rooms, walls, furniture, garden, items, spots, spawns,
            porch = V(L.Porch), yarnBasket = V(L.Mushroom), stash = V(L.StashCenter),
        };
        File.WriteAllText(outPath, JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"layout {seed}: {L.Rooms.Count} rooms, {L.Walls.Count} walls, {L.Furniture.Count} furniture, {L.Items.Count} items -> {outPath}");
        return 0;
    }
}
