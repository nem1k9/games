using System;
using System.Collections.Generic;

namespace Gnomes.Core.Level
{
    public enum FloorStyle : byte { Wood, DarkWood, Tile, Checker, Carpet, Grass }
    public enum WallStyle : byte { StripesBlue, MintTile, Yellow, Damask, Floral, Panel, Plaster }
    public enum OpeningKind : byte { Door, Window, GnomeHole, FakeDoor }

    public sealed class Room
    {
        public string Id, NameRu, NameEn;
        public float X0, Z0, X1, Z1; // world units
        public FloorStyle Floor;
        public WallStyle Wall;
        public bool Contains(float x, float z) => x >= X0 && x <= X1 && z >= Z0 && z <= Z1;
        public V3 Center => new V3((X0 + X1) / 2, 0, (Z0 + Z1) / 2);
    }

    public sealed class Opening
    {
        public string Id;
        public OpeningKind Kind;
        public float T0, T1, Y0, Y1; // along the wall from A, world units
    }

    public sealed class Wall
    {
        public string Id;
        public float Ax, Az, Bx, Bz, Thick;
        public bool Exterior;
        public List<Opening> Openings = new List<Opening>();
        public float Length => (float)Math.Sqrt((Bx - Ax) * (Bx - Ax) + (Bz - Az) * (Bz - Az));

        /// <summary>Solid rectangles (t0,t1,y0,y1) left after cutting the openings out.</summary>
        public List<(float t0, float t1, float y0, float y1)> Solids(float height)
        {
            var list = new List<(float, float, float, float)>();
            var ops = new List<Opening>(Openings);
            ops.Sort((a, b) => a.T0.CompareTo(b.T0));
            float t = 0, len = Length;
            foreach (var o in ops)
            {
                if (o.T0 > t) list.Add((t, o.T0, 0, height));
                if (o.Y0 > 0) list.Add((o.T0, o.T1, 0, o.Y0));
                if (o.Y1 < height) list.Add((o.T0, o.T1, o.Y1, height));
                t = Math.Max(t, o.T1);
            }
            if (t < len) list.Add((t, len, 0, height));
            list.RemoveAll(s => s.Item2 - s.Item1 < 1e-4f || s.Item4 - s.Item3 < 1e-4f);
            return list;
        }
    }

    public sealed class Placement
    {
        public string Id;
        public string Model; // GMDL model name
        public V3 Pos; // world units
        public float RotY; // radians
        public string Room;
        public Dictionary<string, float> Params = new Dictionary<string, float>();
        public float P(string key, float def = 0) => Params.TryGetValue(key, out var v) ? v : def;
    }

    /// <summary>An item to spawn: on a furniture surface (SURF marker) or on a room floor.</summary>
    public sealed class ItemSpawn
    {
        public string Kind;
        public string Furniture; // placement id, or null for floor
        public int Surface = -1; // -1 = random surface
        public string Room; // for floor spawns
        public V3? FloorPos; // explicit floor position
        public bool Jitter = true;
    }

    public sealed class Decor
    {
        public string Model;
        public V3 Pos;
        public float RotY;
        public float Scale = 1;
    }

    public sealed class HouseLayout
    {
        public const float HS = GameConsts.HS;
        public int Seed;
        public float WallHeight;
        public List<Room> Rooms = new List<Room>();
        public List<Wall> Walls = new List<Wall>();
        public List<Placement> Furniture = new List<Placement>();
        public List<ItemSpawn> Items = new List<ItemSpawn>();
        public List<Decor> Garden = new List<Decor>();
        public V3 Mushroom; // yarn basket where fallen gnomes are re-knitted
        public V3 Porch; // porch (village entrance) front centre
        public V3 StashCenter, StashSize;
        public List<V3> Spawns = new List<V3>();
        public V3 GnomeHole;
        public V3 HouseMin, HouseMax, WorldMin, WorldMax;
        /// <summary>Named points the old man walks between (world units).</summary>
        public Dictionary<string, V3> Spots = new Dictionary<string, V3>();

        public Room RoomAt(float x, float z)
        {
            foreach (var r in Rooms) if (r.Contains(x, z)) return r;
            return null;
        }

        public Placement Find(string id) => Furniture.Find(f => f.Id == id);

        public static HouseLayout Generate(int seed)
        {
            var L = new HouseLayout { Seed = seed, WallHeight = 2.6f * HS };
            var rng = new Rng(seed);
            L.BuildRooms();
            L.BuildWalls();
            L.BuildFurniture(rng);
            L.BuildItems(rng);
            L.BuildGarden(seed);
            L.HouseMin = new V3(0, 0, 0);
            L.HouseMax = new V3(14 * HS, L.WallHeight, 10 * HS);
            L.WorldMin = new V3(-3 * HS, -1, -1 * HS);
            L.WorldMax = new V3(17 * HS, 12, 17 * HS);
            return L;
        }

        void R(string id, string ru, string en, float x0, float z0, float x1, float z1, FloorStyle f, WallStyle w) =>
            Rooms.Add(new Room { Id = id, NameRu = ru, NameEn = en, X0 = x0 * HS, Z0 = z0 * HS, X1 = x1 * HS, Z1 = z1 * HS, Floor = f, Wall = w });

        void BuildRooms()
        {
            R("bedroom", "Спальня", "Bedroom", 0, 0, 5, 4.5f, FloorStyle.Carpet, WallStyle.StripesBlue);
            R("bathroom", "Ванная", "Bathroom", 5, 0, 8, 4.5f, FloorStyle.Checker, WallStyle.MintTile);
            R("kitchen", "Кухня", "Kitchen", 8, 0, 14, 4.5f, FloorStyle.Tile, WallStyle.Yellow);
            R("hallway", "Коридор", "Hallway", 0, 4.5f, 14, 6, FloorStyle.Wood, WallStyle.Damask);
            R("living", "Гостиная", "Living room", 0, 6, 9, 10, FloorStyle.Wood, WallStyle.Floral);
            R("study", "Кабинет", "Study", 9, 6, 14, 10, FloorStyle.DarkWood, WallStyle.Panel);
        }

        const float WallT = 0.15f, DoorH = 2.1f;

        void W(string id, float ax, float az, float bx, float bz, bool ext, params Opening[] ops)
        {
            var w = new Wall { Id = id, Ax = ax * HS, Az = az * HS, Bx = bx * HS, Bz = bz * HS, Thick = WallT * HS, Exterior = ext };
            foreach (var o in ops)
            {
                o.T0 *= HS; o.T1 *= HS; o.Y0 *= HS; o.Y1 *= HS;
                w.Openings.Add(o);
            }
            Walls.Add(w);
        }

        static Opening Door(string id, float t0, float t1) => new Opening { Id = id, Kind = OpeningKind.Door, T0 = t0, T1 = t1, Y0 = 0, Y1 = DoorH };
        static Opening Win(string id, float t0, float t1, float y0, float y1) => new Opening { Id = id, Kind = OpeningKind.Window, T0 = t0, T1 = t1, Y0 = y0, Y1 = y1 };

        void BuildWalls()
        {
            float e = WallT / 2;
            W("wN", -e, 0, 14 + e, 0, true, Win("winBed", 3.3f + e, 4.4f + e, 0.9f, 2.0f), Win("winBath", 6.2f + e, 6.8f + e, 1.45f, 2.1f), Win("winKitchen", 9.5f + e, 10.6f + e, 1.05f, 2.0f));
            W("wS", -e, 10, 14 + e, 10, true,
                new Opening { Id = "gnomeHole", Kind = OpeningKind.GnomeHole, T0 = 1.8f + e, T1 = 2.18f + e, Y0 = 0, Y1 = 0.42f },
                Win("winLiving", 5.8f + e, 7.2f + e, 0.8f, 2.0f), Win("winStudy", 12.2f + e, 13.2f + e, 0.9f, 2.0f));
            W("wW", 0, -e, 0, 10 + e, true, Win("winLivingW", 7.4f + e, 8.8f + e, 0.8f, 2.0f));
            W("wE", 14, -e, 14, 10 + e, true, new Opening { Id = "backDoor", Kind = OpeningKind.FakeDoor, T0 = 2.4f + e, T1 = 3.3f + e, Y0 = 0, Y1 = DoorH });
            W("iHallN", 0, 4.5f, 14, 4.5f, false, Door("dBed", 2.0f, 2.9f), Door("dBath", 6.0f, 6.8f), Door("dKitchen", 9.3f, 10.5f));
            W("iHallS", 0, 6, 14, 6, false, Door("dLiving", 3.6f, 5.0f), Door("dStudy", 11.0f, 11.9f));
            W("iBedBath", 5, 0, 5, 4.5f, false);
            W("iBathKit", 8, 0, 8, 4.5f, false);
            W("iLivStudy", 9, 6, 9, 10, false);
            GnomeHole = new V3((1.8f + 2.18f) / 2 * HS, 0, 10 * HS);
        }

        Placement F(string model, float x, float z, string room, string id = null, float rot = 0, float y = 0, params (string, float)[] ps)
        {
            var p = new Placement { Id = id ?? (model + "_" + Furniture.Count), Model = model, Pos = new V3(x * HS, y * HS, z * HS), RotY = rot, Room = room };
            foreach (var (k, v) in ps) p.Params[k] = v;
            Furniture.Add(p);
            return p;
        }

        const float R90 = (float)(Math.PI / 2), PI = (float)Math.PI;

        void BuildFurniture(Rng rng)
        {
            float H = 2.6f;
            // Bedroom
            F("bed", 1.9f, 1.2f, "bedroom", "bed");
            F("nightstand", 0.55f, 0.35f, "bedroom", "nightstandL");
            F("nightstand", 3.25f, 0.35f, "bedroom", "nightstandR");
            F("tableLamp", 0.45f, 0.3f, "bedroom", "lampBed", 0, 0.55f, ("on", 1));
            F("wardrobe", 0.32f, 3.4f, "bedroom", "wardrobe", R90);
            F("dresser", 4.65f, 2.6f, "bedroom", "dresser", -R90);
            F("rugBedroom", 2.2f, 3.1f, "bedroom");
            F("plant", 4.6f, 4.1f, "bedroom");
            F("ceilingLamp", 2.5f, 2.25f, "bedroom", "ceilBed", 0, H, ("on", 0));
            // Bathroom
            F("bathtub", 6.0f, 0.47f, "bathroom", "bathtub");
            F("toilet", 7.62f, 1.9f, "bathroom", "toilet", -R90);
            F("vanity", 5.33f, 3.2f, "bathroom", "vanity", R90);
            F("wallShelf", 7.85f, 3.2f, "bathroom", "bathShelf", -R90, 1.3f);
            F("ceilingLamp", 6.5f, 2.25f, "bathroom", "ceilBath", 0, H, ("on", 0));
            // Kitchen
            F("counter", 8.72f, 0.33f, "kitchen", "counterA");
            F("sinkCounter", 10.0f, 0.33f, "kitchen", "sink");
            F("stove", 10.95f, 0.33f, "kitchen", "stove");
            F("counter", 11.9f, 0.33f, "kitchen", "counterB");
            F("fridge", 13.5f, 0.4f, "kitchen", "fridge");
            F("counter", 13.66f, 1.75f, "kitchen", "counterC", -R90);
            F("kitchenTable", 10.6f, 2.7f, "kitchen", "kitchenTable");
            F("catBed", 8.55f, 3.9f, "kitchen", "catBed");
            F("wallShelf", 12.0f, 0.13f, "kitchen", "kitchenShelf", 0, 1.6f);
            F("ceilingLamp", 10.6f, 2.6f, "kitchen", "ceilKitchen", 0, H, ("on", 0));
            F("jarShelf", 8.9f, 4.37f, "kitchen", "jarShelf", PI, 1.3f); // grandpa pickles gnomes up here
            // Hallway
            F("grandfatherClock", 13.6f, 5.25f, "hallway", "clock", -R90);
            F("coatRack", 0.35f, 5.25f, "hallway");
            F("shoeRack", 1.3f, 4.75f, "hallway", "shoeRack");
            F("hallTable", 7.6f, 4.8f, "hallway", "hallTable");
            F("rugHall", 7f, 5.25f, "hallway");
            F("plantBig", 12.4f, 5.7f, "hallway");
            F("ceilingLamp", 4f, 5.25f, "hallway", "ceilHall", 0, H, ("on", 1));
            // Living room
            F("tvStand", 8.65f, 8.0f, "living", "tvStand", -R90);
            F("tv", 8.65f, 8.0f, "living", "tv", -R90, 0.5f);
            F("sofa", 5.3f, 8.0f, "living", "sofa", R90);
            F("armchair", 7.2f, 9.35f, "living", "armchair", PI - 0.5f);
            F("coffeeTable", 6.9f, 8.0f, "living", "coffeeTable", R90);
            F("rugLiving", 7.0f, 8.0f, "living");
            F("bookshelf", 0.2f, 7.2f, "living", "bookshelfL", R90);
            F("floorLamp", 8.4f, 9.55f, "living", "floorLamp", 0, 0, ("on", 1));
            F("sideTable", 0.45f, 6.45f, "living", "sideTable");
            F("fireplace", 2.7f, 6.33f, "living", "fireplace");
            F("plantBig", 0.4f, 9.55f, "living");
            F("ceilingLamp", 4.5f, 8f, "living", "ceilLiving", 0, H, ("on", 0));
            F("parrotCage", 4.3f, 9.55f, "living", "parrotCage", PI);
            // Study
            F("desk", 11.2f, 9.55f, "study", "desk", PI);
            F("bookshelf", 13.8f, 7.0f, "study", "bookshelfS1", -R90);
            F("bookshelf", 13.8f, 8.2f, "study", "bookshelfS2", -R90);
            F("safe", 9.45f, 9.55f, "study", "safe", PI);
            F("fishTank", 9.3f, 7.4f, "study", "fishTank", R90);
            F("tableLampGreen", 10.75f, 9.65f, "study", "lampDesk", 0, 0.765f, ("on", 1));
            F("rugStudy", 11.5f, 8.0f, "study");
            F("ceilingLamp", 11.5f, 8f, "study", "ceilStudy", 0, H, ("on", 0));

            // Hazards: mousetraps and creaky floorboards at seeded spots
            var trapSpots = new List<(float x, float z, string room)>
            {
                (9.2f, 1.6f, "kitchen"), (12.3f, 3.7f, "kitchen"), (5.7f, 4.9f, "hallway"), (9.8f, 5.3f, "hallway"),
                (1.2f, 5.5f, "hallway"), (3.3f, 8.8f, "living"), (7.9f, 7.0f, "living"), (12.8f, 6.8f, "study"),
                (3.9f, 3.9f, "bedroom"), (6.9f, 3.7f, "bathroom"),
            };
            rng.Shuffle(trapSpots);
            for (int i = 0; i < 4; i++)
                F("mousetrap", trapSpots[i].x, trapSpots[i].z, trapSpots[i].room, "mousetrap" + i, rng.Range(0, 6.28f));
            var creakSpots = new List<(float x, float z, string room)>
            {
                (2.4f, 5.2f, "hallway"), (6.4f, 5.3f, "hallway"), (10.9f, 5.2f, "hallway"), (4.3f, 7.0f, "living"),
                (2.6f, 8.6f, "living"), (11.8f, 7.4f, "study"), (2.4f, 3.6f, "bedroom"),
            };
            rng.Shuffle(creakSpots);
            for (int i = 0; i < 4; i++)
                F("creakyBoard", creakSpots[i].x, creakSpots[i].z, creakSpots[i].room, "creak" + i, rng.Chance(0.5f) ? 0 : R90);

            // Windows (sash window models fitted into the wall openings)
            foreach (var w in Walls)
            {
                foreach (var o in w.Openings)
                {
                    if (o.Kind != OpeningKind.Window) continue;
                    float len = w.Length;
                    float tx = (w.Bx - w.Ax) / len, tz = (w.Bz - w.Az) / len;
                    float tm = (o.T0 + o.T1) / 2;
                    float cx = w.Ax + tx * tm, cz = w.Az + tz * tm;
                    // window model: front (+Z) faces into the house
                    float nx = -tz, nz = tx; // one normal
                    float hcx = 7 * HS, hcz = 5 * HS;
                    if ((hcx - cx) * nx + (hcz - cz) * nz < 0) { nx = -nx; nz = -nz; }
                    float rot = (float)Math.Atan2(nx, nz);
                    var p = new Placement { Id = o.Id, Model = "window", Pos = new V3(cx, o.Y0, cz), RotY = rot, Room = RoomAt(cx + nx, cz + nz)?.Id ?? "" };
                    p.Params["w"] = (o.T1 - o.T0) / HS;
                    p.Params["h"] = (o.Y1 - o.Y0) / HS;
                    Furniture.Add(p);
                }
            }

            // spots used by the old man's routine (in front of furniture, world units)
            Spots["bed"] = new V3(2.9f * HS, 0, 2.6f * HS);
            Spots["armchair"] = new V3(6.8f * HS, 0, 8.9f * HS);
            Spots["fridge"] = new V3(13.3f * HS, 0, 1.6f * HS);
            Spots["stove"] = new V3(10.95f * HS, 0, 1.35f * HS);
            Spots["toilet"] = new V3(7.0f * HS, 0, 1.9f * HS);
            Spots["sink"] = new V3(10.0f * HS, 0, 1.3f * HS);
            Spots["hall"] = new V3(7f * HS, 0, 5.25f * HS);
            Spots["study"] = new V3(11.5f * HS, 0, 8.0f * HS);
            Spots["livingWindow"] = new V3(6.5f * HS, 0, 9.3f * HS);
            Spots["kitchenTable"] = new V3(10.6f * HS, 0, 3.7f * HS);
        }

        void S(string kind, string furn, int surface = -1, bool jitter = true) => Items.Add(new ItemSpawn { Kind = kind, Furniture = furn, Surface = surface, Jitter = jitter });
        void Fl(string kind, string room, float? x = null, float? z = null) =>
            Items.Add(new ItemSpawn { Kind = kind, Room = room, FloorPos = x.HasValue ? new V3(x.Value * HS, 0, z.Value * HS) : (V3?)null });

        void BuildItems(Rng rng)
        {
            // task-critical items (always present, randomised spots)
            S("glasses", rng.Pick(new[] { "nightstandL", "nightstandR" }));
            S("dentures", rng.Chance(0.5f) ? "vanity" : "nightstandR");
            S("alarmClock", "nightstandL");
            S("remote", rng.Pick(new[] { "coffeeTable", "sofa" }));
            S("vase", "sideTable", 0, false);
            S("watch", "dresser");
            S("jewelBox", "dresser");
            S("piggyBank", rng.Pick(new[] { "desk", "dresser" }));
            S("trophy", rng.Pick(new[] { "bookshelfS1", "bookshelfS2" }), 3);
            S("hearingAid", rng.Pick(new[] { "nightstandR", "nightstandL" }));
            S("pipe", "desk");
            for (int i = 0; i < 3; i++) S("plate", rng.Pick(new[] { "kitchenTable", "kitchenTable", "sink" }));
            S("teapot", "kitchenTable");
            S("mug", "kitchenTable");
            S("mug", "coffeeTable");
            S("toaster", rng.Pick(new[] { "counterA", "counterB" }));
            S("kettle", "stove");
            S("cookieJar", "counterA");
            S("cheese", "fridge", 0);
            S("sausage", "fridge", 1);
            S("bottle", "fridge", 1);
            S("apple", "kitchenTable");
            S("bread", "counterB");
            S("telephone", "hallTable");
            S("keys", "hallTable");
            S("towel", "bathShelf");
            S("teacup", rng.Pick(new[] { "coffeeTable", "kitchenTable" }));
            S("photoFrame", "hallTable");
            S("candle", "fireplace");
            S("photoFrame", "fireplace");
            S("coins", "safe");
            S("goldBar", "safe");
            S("cash", "desk");
            S("globe", "desk");
            S("book", rng.Pick(new[] { "bookshelfL", "bookshelfS1" }));
            S("book", "coffeeTable");
            S("shampoo", "vanity");
            S("toothbrush", "vanity");
            S("soap", "vanity");
            S("ring", "dresser");
            S("pearls", rng.Pick(new[] { "dresser", "nightstandR" }));
            S("can", "kitchenShelf");
            S("can", "kitchenShelf");
            S("pot", "kitchenShelf");
            S("duck", "bathShelf");
            S("pan", rng.Pick(new[] { "counterB", "counterA" }));
            S("spoon", "kitchenTable");
            S("fork", "kitchenTable");
            Fl("catBowl", "kitchen", 8.9f + rng.Range(-0.1f, 0.1f), 3.95f);
            Fl("trashBin", "kitchen", 12.9f, 3.9f);
            Fl("chair", "kitchen", 10.1f, 3.35f);
            Fl("chair", "kitchen", 11.2f, 2.05f);
            Fl("chair", "study", 11.2f, 8.9f);
            Fl("slipper", "bedroom", 1.0f + rng.Range(-0.2f, 0.2f), 2.6f);
            Fl("slipper", "bedroom", 1.25f + rng.Range(-0.2f, 0.2f), 2.65f);
            int socks = rng.Int(3, 5);
            for (int i = 0; i < socks; i++) Fl("sock", rng.Pick(new[] { "bedroom", "bedroom", "bathroom", "hallway", "living" }));
            Fl("duck", "bathroom");
            Fl("toiletPaper", "bathroom");
            Fl("toiletPaper", "bathroom");
            Fl("yarn", rng.Pick(new[] { "living", "kitchen" }));
            Fl("box", "study", 13.4f, 9.5f);
            Fl("battery", rng.Pick(new[] { "living", "study" }));
            Fl("coins", rng.Pick(new[] { "living", "bedroom", "hallway" }));
            Fl("lightbulb", "hallway");
        }

        void BuildGarden(int seed)
        {
            // The gang arrives from the village under the porch (south side of the house).
            Porch = new V3(7.5f * HS, 0, 10.0f * HS);
            Mushroom = new V3(5.6f * HS, 0, 12.3f * HS); // the yarn basket (revive point)
            StashCenter = new V3(9.4f * HS, 0.3f * HS, 12.3f * HS); // the matchbox cart
            StashSize = new V3(0.7f * HS, 0.6f * HS, 0.55f * HS);
            for (int i = 0; i < 6; i++)
                Spawns.Add(new V3((6.3f + i * 0.45f) * HS, 0.1f, 12.0f * HS));
            var g = new Rng(seed ^ 0x5eed);
            for (int i = 0; i < 30; i++)
            {
                float x = g.Range(-2.6f, 16.6f), z = g.Range(10.8f, 16.5f);
                if ((x > 4.8f && x < 10.3f && z < 13.2f) || (x > 1.1f && x < 3.3f && z < 11.8f)) continue; // keep the porch area & gnome hole clear
                float t = g.Next();
                string model = t < 0.4f ? "flowers" : t < 0.7f ? "bush" : t < 0.85f ? "rock" : "toadstool";
                Garden.Add(new Decor { Model = model, Pos = new V3(x * HS, 0, z * HS), RotY = g.Range(0, 6.28f), Scale = g.Range(0.7f, 1.3f) });
            }
            for (int i = 0; i < 10; i++)
                Garden.Add(new Decor { Model = "tree", Pos = new V3((-3 + i * 2.2f + g.Range(-0.3f, 0.3f)) * HS, 0, g.Range(17.6f, 18.8f) * HS), RotY = g.Range(0, 6.28f), Scale = g.Range(0.9f, 1.4f) });
            Garden.Add(new Decor { Model = "gardenLamp", Pos = new V3(7.5f * HS, 0, 10.8f * HS) });
            Garden.Add(new Decor { Model = "fence", Pos = new V3(7f * HS, 0, 17f * HS) });
        }

        static float Dist2(float ax, float az, float bx, float bz) => (ax - bx) * (ax - bx) + (az - bz) * (az - bz);
    }
}
