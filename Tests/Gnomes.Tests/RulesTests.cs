using System.Linq;
using Gnomes.Core;
using Gnomes.Core.Level;
using Gnomes.Core.Rules;
using Xunit;

namespace Gnomes.Tests
{
    public class RulesTests
    {
        [Fact]
        public void RngIsDeterministic()
        {
            var a = new Rng(42);
            var b = new Rng(42);
            for (int i = 0; i < 100; i++) Assert.Equal(a.NextUInt(), b.NextUInt());
            var c = new Rng(7);
            for (int i = 0; i < 1000; i++)
            {
                float f = c.Next();
                Assert.InRange(f, 0f, 0.99999994f);
                int n = c.Int(3, 5);
                Assert.InRange(n, 3, 5);
            }
        }

        [Fact]
        public void EveryItemKindHasAModelAndSaneData()
        {
            foreach (var k in ItemDefs.Kinds)
            {
                var d = ItemDefs.Get(k);
                Assert.True(d.Mass > 0, k);
                Assert.False(string.IsNullOrEmpty(d.NameRu), k);
                if (d.SpillKind != null) Assert.True(ItemDefs.TryGet(d.SpillKind, out _), k + " spills unknown kind");
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(7)]
        public void NightPicksFiveDistinctNonConflictingTasks(int night)
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var tasks = TaskCatalog.PickForNight(night, seed);
                Assert.Equal(GameConsts.TasksPerNight, tasks.Count);
                Assert.Equal(tasks.Count, tasks.Select(t => t.Id).Distinct().Count());
                Assert.All(tasks, t => Assert.True(t.MinNight <= night));
                foreach (var t in tasks)
                    Assert.DoesNotContain(tasks, o => t.ConflictsWith.Contains(o.Id));
            }
            if (night == 1) Assert.DoesNotContain(TaskCatalog.PickForNight(1, 3), t => t.Tier == 3);
        }

        [Fact]
        public void TaskProgressFromEvents()
        {
            var tr = new TaskTracker(new[] { TaskCatalog.Get("smashPlates"), TaskCatalog.Get("flush"), TaskCatalog.Get("loot60"), TaskCatalog.Get("remoteFridge"), TaskCatalog.Get("chaos10"), TaskCatalog.Get("tieSlippers") });
            Assert.Empty(tr.Apply(GameEvent.Broken("mug")));
            Assert.Empty(tr.Apply(GameEvent.Broken("plate")));
            Assert.Empty(tr.Apply(GameEvent.Broken("plate")));
            Assert.Equal(new[] { 0 }, tr.Apply(GameEvent.Broken("plate")));
            Assert.Empty(tr.Apply(GameEvent.Broken("plate"))); // already done, no double completion
            Assert.Equal(new[] { 1 }, tr.Apply(GameEvent.Flushed("sock")));
            Assert.Empty(tr.Apply(GameEvent.InZone("oven", "remote")));
            Assert.Equal(new[] { 3 }, tr.Apply(GameEvent.InZone("fridge", "remote")));
            for (int i = 0; i < 8; i++) tr.Apply(GameEvent.Banked("goldBar", 8));
            Assert.True(tr.IsDone(2));
            // chaos is a level, not a sum: 6 then 4 must not complete a goal of 10
            tr.Apply(GameEvent.Chaos(6));
            tr.Apply(GameEvent.Chaos(4));
            Assert.False(tr.IsDone(4));
            Assert.Equal(6, tr.Progress[4]);
            Assert.Equal(new[] { 4 }, tr.Apply(GameEvent.Chaos(11)));
            // tying a slipper to a sock is not tying the slippers together
            Assert.Empty(tr.Apply(GameEvent.Tied("slipper", "sock")));
            Assert.Equal(new[] { 5 }, tr.Apply(GameEvent.Tied("slipper", "slipper")));
            Assert.Equal(6, tr.CompletedCount);
            Assert.Equal(4 + 2 + 6 + 4 + 4 + 4, tr.GigglesEarned); // tiers 2,1,3,2,2,2 -> 2 giggles per tier
        }

        [Fact]
        public void NightStateBanksAndReports()
        {
            var ns = new NightState(1, 123, 1, GearEffects.From(new SaveData()));
            Assert.Equal(GameConsts.SpareHatsSolo, ns.SpareHats);
            ns.Bank("goldBar");
            ns.Bank("sock");
            Assert.Equal(ItemDefs.Get("goldBar").Value + 1, ns.HaulValue);
            Assert.Equal(2, ns.ItemsStolen);
            Assert.Equal("00:00", ns.Clock);
            ns.TimeLeft = GameConsts.NightSeconds / 2;
            Assert.Equal("03:00", ns.Clock);
            var r = ns.MakeReport(false);
            Assert.Equal(ns.HaulValue, r.HaulValue);
            Assert.Equal(r.TasksDone >= 3, r.Passed);
        }

        [Fact]
        public void CraftingSpendsMaterialsAndGearChangesStats()
        {
            var s = new SaveData();
            var gaiters = GearCatalog.Get(GearId.HopperGaiters);
            Assert.False(s.Craft(GearId.HopperGaiters));
            for (int i = 0; i < 6; i++) s.Materials[i] = 10;
            Assert.True(s.Craft(GearId.HopperGaiters));
            Assert.False(s.Craft(GearId.HopperGaiters)); // already owned
            Assert.Equal(10 - gaiters.Cost[1], s.Materials[1]);
            Assert.False(s.Craft(GearId.EndlessYarn)); // needs long yarn first
            Assert.True(s.Craft(GearId.SleepDust));
            Assert.True(s.Craft(GearId.SleepDust));
            Assert.Equal(2, s.Potions);
            var fx = GearEffects.From(s);
            Assert.Equal(GameConsts.HopperJumpSpeed, fx.JumpSpeed);
            Assert.Equal(GameConsts.YarnRange, fx.YarnRange);
            Assert.False(fx.Parachute);
        }

        [Fact]
        public void SaveRoundTripsAndStrikesGetYouFired()
        {
            var s = new SaveData { Night = 4, Strikes = 1, Potions = 2 };
            s.Materials[3] = 17;
            s.Gear.Add(GearId.QuietBooties);
            var s2 = SaveData.Deserialize(s.Serialize());
            Assert.Equal(4, s2.Night);
            Assert.Equal(1, s2.Strikes);
            Assert.Equal(17, s2.Materials[3]);
            Assert.True(s2.Has(GearId.QuietBooties));
            Assert.Equal(2, s2.Potions);

            var bad = new NightReport { Passed = false };
            Assert.False(s2.ApplyReport(bad));
            Assert.Equal(2, s2.Strikes);
            Assert.True(s2.ApplyReport(bad)); // third strike
            Assert.Equal(1, s2.Night);
            Assert.Equal(0, s2.Materials[3]);
            Assert.True(s2.Has(GearId.QuietBooties)); // gear survives getting fired
            Assert.Equal(1, s2.TimesFired);

            var garbage = SaveData.Deserialize("night=abc\nmat9=5\ngear=1,99,x\n");
            Assert.Equal(1, garbage.Night);
            Assert.True(garbage.Has(GearId.LongYarn));
        }

        [Fact]
        public void HouseLayoutIsConsistent()
        {
            var L = HouseLayout.Generate(1234);
            Assert.Equal(6, L.Rooms.Count);
            Assert.Equal(L.Furniture.Count, L.Furniture.Select(f => f.Id).Distinct().Count());
            foreach (var it in L.Items)
            {
                Assert.True(ItemDefs.TryGet(it.Kind, out _), "unknown item " + it.Kind);
                if (it.Furniture != null) Assert.NotNull(L.Find(it.Furniture));
                else Assert.Contains(L.Rooms, r => r.Id == it.Room);
            }
            // task-critical items are always present
            foreach (var k in new[] { "dentures", "glasses", "remote", "vase", "watch", "piggyBank", "trophy", "catBowl", "trashBin", "goldBar", "alarmClock", "hearingAid", "keys", "towel", "toiletPaper", "duck" })
                Assert.Contains(L.Items, i => i.Kind == k);
            Assert.True(L.Items.Count(i => i.Kind == "sock") >= 3);
            Assert.Equal(2, L.Items.Count(i => i.Kind == "slipper"));
            Assert.True(L.Items.Count(i => i.Kind == "plate") >= 3);
            // windows got window models
            Assert.Equal(6, L.Furniture.Count(f => f.Model == "window"));
            // the gnome hole is in the south wall and the mushroom is outside
            Assert.Equal(10 * GameConsts.HS, L.GnomeHole.z);
            Assert.True(L.Mushroom.z > L.HouseMax.z);
            Assert.All(L.Spawns, s => Assert.True(s.z > L.HouseMax.z, "spawn inside house"));
            // same seed, same layout
            var L2 = HouseLayout.Generate(1234);
            Assert.Equal(L.Items.Select(i => i.Kind + i.Furniture + i.Room), L2.Items.Select(i => i.Kind + i.Furniture + i.Room));
        }
    }
}

namespace Gnomes.Tests
{
    public class MeshGenTests
    {
        [Fact]
        public void WallSlabFacesPointOutwardsWithCorrectWinding()
        {
            var size = new Gnomes.Core.V3(8, 10, 0.6f);
            var m = Gnomes.Core.Level.MeshGen.WallSlab(size, 3, 0, 5);
            Assert.Equal(3, m.SubMeshes.Count);
            Assert.Equal(24, m.Positions.Count);
            int tris = 0;
            foreach (var sub in m.SubMeshes)
            {
                for (int t = 0; t < sub.Count; t += 3)
                {
                    var a = m.Positions[sub[t]];
                    var b = m.Positions[sub[t + 1]];
                    var c = m.Positions[sub[t + 2]];
                    var n = m.Normals[sub[t]];
                    var cross = Gnomes.Core.V3.Cross(b - a, c - a);
                    Assert.True(Gnomes.Core.V3.Dot(cross, n) > 0, "triangle winding opposite to its normal");
                    // normals point away from the slab centre
                    var centre = (a + b + c) / 3f;
                    Assert.True(Gnomes.Core.V3.Dot(centre, n) > 0, "normal points inwards");
                    tris++;
                }
            }
            Assert.Equal(12, tris);
            Assert.All(m.Uvs, u => Assert.False(float.IsNaN(u)));
        }
    }
}
