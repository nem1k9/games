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
            var tr = new TaskTracker(new[] { TaskCatalog.Get("smashPlates"), TaskCatalog.Get("flush"), TaskCatalog.Get("loot30"), TaskCatalog.Get("remoteFridge") });
            Assert.Empty(tr.Apply(GameEvent.Broken("mug")));
            Assert.Empty(tr.Apply(GameEvent.Broken("plate")));
            Assert.Empty(tr.Apply(GameEvent.Broken("plate")));
            Assert.Equal(new[] { 0 }, tr.Apply(GameEvent.Broken("plate")));
            Assert.Empty(tr.Apply(GameEvent.Broken("plate"))); // already done, no double completion
            Assert.Equal(new[] { 1 }, tr.Apply(GameEvent.Flushed("sock")));
            Assert.Empty(tr.Apply(GameEvent.InZone("oven", "remote")));
            Assert.Equal(new[] { 3 }, tr.Apply(GameEvent.InZone("fridge", "remote")));
            for (int i = 0; i < 4; i++) tr.Apply(GameEvent.Banked("goldBar", 8));
            Assert.True(tr.IsDone(2));
            Assert.Equal(4, tr.CompletedCount);
            Assert.Equal(4 + 2 + 4 + 4, tr.GnomiumEarned); // tiers 2,1,2,2 -> 2 gnomium per tier
        }

        [Fact]
        public void NightStateBanksAndReports()
        {
            var ns = new NightState(1, 123, 1, GearEffects.From(new SaveData()));
            Assert.Equal(GameConsts.SporesSolo, ns.Spores);
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
            var boots = GearCatalog.Get(GearId.SpringBoots);
            Assert.False(s.Craft(GearId.SpringBoots));
            for (int i = 0; i < 6; i++) s.Materials[i] = 10;
            Assert.True(s.Craft(GearId.SpringBoots));
            Assert.False(s.Craft(GearId.SpringBoots)); // already owned
            Assert.Equal(10 - boots.Cost[1], s.Materials[1]);
            Assert.False(s.Craft(GearId.GrapplingArms)); // needs stretchy arms first
            Assert.True(s.Craft(GearId.SleepPotion));
            Assert.True(s.Craft(GearId.SleepPotion));
            Assert.Equal(2, s.Potions);
            var fx = GearEffects.From(s);
            Assert.Equal(GameConsts.SpringJumpSpeed, fx.JumpSpeed);
            Assert.Equal(GameConsts.ArmMax, fx.ArmMax);
        }

        [Fact]
        public void SaveRoundTripsAndStrikesGetYouFired()
        {
            var s = new SaveData { Night = 4, Strikes = 1, Potions = 2 };
            s.Materials[3] = 17;
            s.Gear.Add(GearId.SneakySocks);
            var s2 = SaveData.Deserialize(s.Serialize());
            Assert.Equal(4, s2.Night);
            Assert.Equal(1, s2.Strikes);
            Assert.Equal(17, s2.Materials[3]);
            Assert.True(s2.Has(GearId.SneakySocks));
            Assert.Equal(2, s2.Potions);

            var bad = new NightReport { Passed = false };
            Assert.False(s2.ApplyReport(bad));
            Assert.Equal(2, s2.Strikes);
            Assert.True(s2.ApplyReport(bad)); // third strike
            Assert.Equal(1, s2.Night);
            Assert.Equal(0, s2.Materials[3]);
            Assert.True(s2.Has(GearId.SneakySocks)); // gear survives getting fired
            Assert.Equal(1, s2.TimesFired);

            var garbage = SaveData.Deserialize("night=abc\nmat9=5\ngear=1,99,x\n");
            Assert.Equal(1, garbage.Night);
            Assert.True(garbage.Has(GearId.StretchyArms));
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
            foreach (var k in new[] { "dentures", "glasses", "remote", "vase", "watch", "piggyBank", "trophy", "catBowl", "trashBin", "goldBar", "alarmClock", "hearingAid" })
                Assert.Contains(L.Items, i => i.Kind == k);
            Assert.True(L.Items.Count(i => i.Kind == "sock") >= 3);
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
