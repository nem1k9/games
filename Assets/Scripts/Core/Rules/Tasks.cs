using System;
using System.Collections.Generic;

namespace Gnomes.Core.Rules
{
    /// <summary>Things that happen in the house that orders can react to.</summary>
    public enum Ev : byte
    {
        Broken = 0, // a loose item broke (Kind = item kind)
        Banked = 1, // an item reached the stash (Kind, Value)
        Flushed = 2, // an item got flushed down the toilet
        Mech = 3, // a mechanism changed state (Role, On)
        InZone = 4, // an item is inside a named zone (Zone, Kind)
        FurnitureBroken = 5, // static furniture destroyed (Kind = furniture model)
        Tipped = 6, // an item fell over (Kind)
        Caught = 7, // a gnome got caught by the old man
    }

    public struct GameEvent
    {
        public Ev Type;
        public string Kind;
        public string Zone;
        public string Role;
        public bool On;
        public int Value;

        public static GameEvent Broken(string kind) => new GameEvent { Type = Ev.Broken, Kind = kind };
        public static GameEvent Banked(string kind, int value) => new GameEvent { Type = Ev.Banked, Kind = kind, Value = value };
        public static GameEvent Flushed(string kind) => new GameEvent { Type = Ev.Flushed, Kind = kind };
        public static GameEvent Mech(string role, bool on) => new GameEvent { Type = Ev.Mech, Role = role, On = on };
        public static GameEvent InZone(string zone, string kind) => new GameEvent { Type = Ev.InZone, Zone = zone, Kind = kind };
        public static GameEvent FurnitureBroken(string kind) => new GameEvent { Type = Ev.FurnitureBroken, Kind = kind };
        public static GameEvent Tipped(string kind) => new GameEvent { Type = Ev.Tipped, Kind = kind };

        public bool ItemHasTag(string tag) => Kind != null && ItemDefs.TryGet(Kind, out var d) && d.HasTag(tag);
    }

    public sealed class TaskDef
    {
        public string Id;
        public string Ru, En;
        public int Tier; // 1 easy, 2 medium, 3 hard
        public int Goal = 1;
        public int MinNight = 1;
        public Func<GameEvent, int> Progress; // returns how much progress this event gives
        public string[] ConflictsWith = Array.Empty<string>();
        public int Reward => Tier * 2; // gnomium
        public string Text(Lang l) => l == Lang.Ru ? Ru : En;
    }

    public static class TaskCatalog
    {
        public static readonly List<TaskDef> All = new List<TaskDef>();

        static TaskDef T(string id, int tier, string ru, string en, Func<GameEvent, int> progress, int goal = 1, int minNight = 1)
        {
            var t = new TaskDef { Id = id, Tier = tier, Ru = ru, En = en, Progress = progress, Goal = goal, MinNight = minNight };
            All.Add(t);
            return t;
        }

        static int If(bool b) => b ? 1 : 0;

        static TaskCatalog()
        {
            // ---- easy ----
            T("flush", 1, "Смыть что-нибудь в унитаз", "Flush something down the toilet", e => If(e.Type == Ev.Flushed));
            T("openWindow", 1, "Открыть окно", "Open a window", e => If(e.Type == Ev.Mech && e.Role == "window" && e.On));
            T("turnOnTv", 1, "Включить телевизор", "Turn on the TV", e => If(e.Type == Ev.Mech && e.Role == "tvPower" && e.On));
            T("floodBath", 1, "Устроить потоп: открыть кран в ванне", "Flood the bath: turn on the tap", e => If(e.Type == Ev.Mech && e.Role == "tubFaucet" && e.On));
            T("breakVase", 1, "Разбить вазу", "Smash the vase", e => If(e.Type == Ev.Broken && e.ItemHasTag("vase")));
            T("feedFish", 1, "Покормить рыбок (еду в аквариум)", "Feed the fish (food into the tank)", e => If(e.Type == Ev.InZone && e.Zone == "fishTank" && e.ItemHasTag("food")));
            T("knockTrash", 1, "Опрокинуть мусорное ведро", "Knock over the trash bin", e => If(e.Type == Ev.Tipped && e.ItemHasTag("trashBin")));
            T("duckToilet", 1, "Искупать уточку в унитазе", "Give the duck a bath in the toilet", e => If(e.Type == Ev.InZone && e.Zone == "toiletBowl" && e.ItemHasTag("duck")));
            T("stealAlarm", 1, "Украсть будильник", "Steal the alarm clock", e => If(e.Type == Ev.Banked && e.ItemHasTag("alarmClock")));
            T("stealSocks", 1, "Украсть 3 носка", "Steal 3 socks", e => If(e.Type == Ev.Banked && e.ItemHasTag("sock")), 3);

            // ---- medium ----
            T("breakTv", 2, "Разбить телевизор", "Break the TV", e => If(e.Type == Ev.FurnitureBroken && e.Kind == "tv"));
            T("stealDentures", 2, "Украсть вставную челюсть", "Steal the dentures", e => If(e.Type == Ev.Banked && e.ItemHasTag("dentures")));
            T("stealGlasses", 2, "Украсть очки деда (он станет хуже видеть)", "Steal grandpa's glasses (he'll see worse)", e => If(e.Type == Ev.Banked && e.ItemHasTag("glasses")));
            T("smashPlates", 2, "Разбить 3 тарелки", "Smash 3 plates", e => If(e.Type == Ev.Broken && e.ItemHasTag("plate")), 3);
            T("remoteFridge", 2, "Спрятать пульт в холодильник", "Hide the TV remote in the fridge", e => If(e.Type == Ev.InZone && e.Zone == "fridge" && e.ItemHasTag("remote")));
            T("catBowlOven", 2, "Засунуть кошачью миску в духовку", "Put the cat bowl in the oven", e => If(e.Type == Ev.InZone && e.Zone == "oven" && e.ItemHasTag("catBowl")));
            T("stealWatch", 2, "Украсть золотые часы", "Steal the gold watch", e => If(e.Type == Ev.Banked && e.ItemHasTag("watch")));
            T("piggyBank", 2, "Разбить копилку", "Smash the piggy bank", e => If(e.Type == Ev.Broken && e.ItemHasTag("piggyBank")));
            T("stealSlippers", 2, "Украсть 2 тапка", "Steal 2 slippers", e => If(e.Type == Ev.Banked && e.ItemHasTag("slipper")), 2);
            T("stealHearingAid", 2, "Украсть слуховой аппарат (он станет хуже слышать)", "Steal the hearing aid (he'll hear worse)", e => If(e.Type == Ev.Banked && e.ItemHasTag("hearingAid")));
            T("loot30", 2, "Натащить добра на 30 материалов", "Stash loot worth 30 materials", e => e.Type == Ev.Banked ? e.Value : 0, 30);

            // ---- hard ----
            T("crackSafe", 3, "Вскрыть сейф (колоти по замку)", "Crack the safe (keep hitting the lock)", e => If(e.Type == Ev.Mech && e.Role == "safe" && e.On), 1, 2);
            T("stealTrophy", 3, "Украсть золотой кубок (тяжёлый!)", "Steal the golden trophy (heavy!)", e => If(e.Type == Ev.Banked && e.ItemHasTag("trophy")));
            T("stealGold", 3, "Украсть слиток золота из сейфа", "Steal the gold bar from the safe", e => If(e.Type == Ev.Banked && e.ItemHasTag("gold")), 1, 2);
            T("loot60", 3, "Натащить добра на 60 материалов", "Stash loot worth 60 materials", e => e.Type == Ev.Banked ? e.Value : 0, 60, 2);
            T("stealToaster", 3, "Украсть тостер (вдвоём легче)", "Steal the toaster (easier with a friend)", e => If(e.Type == Ev.Banked && e.Kind == "toaster"));

            Get("loot30").ConflictsWith = new[] { "loot60" };
            Get("loot60").ConflictsWith = new[] { "loot30" };
            Get("crackSafe").ConflictsWith = new[] { "stealGold" };
            Get("stealGold").ConflictsWith = new[] { "crackSafe" };
        }

        public static TaskDef Get(string id)
        {
            foreach (var t in All) if (t.Id == id) return t;
            return null;
        }

        /// <summary>Pick the night's orders. Harder mixes on later nights.</summary>
        public static List<TaskDef> PickForNight(int night, int seed, int count = GameConsts.TasksPerNight)
        {
            var rng = new Rng(seed * 7919 + night);
            int[] tiers;
            if (night <= 1) tiers = new[] { 1, 1, 1, 2, 2 };
            else if (night == 2) tiers = new[] { 1, 1, 2, 2, 3 };
            else if (night <= 4) tiers = new[] { 1, 2, 2, 3, 3 };
            else tiers = new[] { 2, 2, 3, 3, 3 };
            var chosen = new List<TaskDef>();
            for (int i = 0; i < count; i++)
            {
                int tier = tiers[Math.Min(i, tiers.Length - 1)];
                var pool = All.FindAll(t => t.Tier == tier && t.MinNight <= night && !chosen.Contains(t) && !Conflicts(t, chosen));
                if (pool.Count == 0) pool = All.FindAll(t => t.MinNight <= night && !chosen.Contains(t) && !Conflicts(t, chosen));
                if (pool.Count == 0) break;
                chosen.Add(rng.Pick(pool));
            }
            return chosen;
        }

        static bool Conflicts(TaskDef t, List<TaskDef> chosen)
        {
            foreach (var c in chosen)
                if (Array.IndexOf(c.ConflictsWith, t.Id) >= 0 || Array.IndexOf(t.ConflictsWith, c.Id) >= 0) return true;
            return false;
        }
    }

    /// <summary>Tracks progress of the night's orders.</summary>
    public sealed class TaskTracker
    {
        public readonly List<TaskDef> Tasks = new List<TaskDef>();
        public int[] Progress = Array.Empty<int>();

        public TaskTracker() { }

        public TaskTracker(IEnumerable<TaskDef> tasks)
        {
            Tasks.AddRange(tasks);
            Progress = new int[Tasks.Count];
        }

        public bool IsDone(int i) => Progress[i] >= Tasks[i].Goal;

        public int CompletedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Tasks.Count; i++) if (IsDone(i)) n++;
                return n;
            }
        }

        /// <summary>Apply an event. Returns indices of tasks completed by it.</summary>
        public List<int> Apply(GameEvent e)
        {
            var done = new List<int>();
            for (int i = 0; i < Tasks.Count; i++)
            {
                if (IsDone(i)) continue;
                int p = Tasks[i].Progress(e);
                if (p <= 0) continue;
                Progress[i] = Math.Min(Tasks[i].Goal, Progress[i] + p);
                if (IsDone(i)) done.Add(i);
            }
            return done;
        }

        public int GnomiumEarned
        {
            get
            {
                int g = 0;
                for (int i = 0; i < Tasks.Count; i++) if (IsDone(i)) g += Tasks[i].Reward;
                return g;
            }
        }

        public string[] Ids()
        {
            var a = new string[Tasks.Count];
            for (int i = 0; i < a.Length; i++) a[i] = Tasks[i].Id;
            return a;
        }

        public static TaskTracker FromIds(string[] ids, int[] progress)
        {
            var t = new TaskTracker();
            foreach (var id in ids)
            {
                var d = TaskCatalog.Get(id);
                if (d != null) t.Tasks.Add(d);
            }
            t.Progress = new int[t.Tasks.Count];
            if (progress != null) Array.Copy(progress, t.Progress, Math.Min(progress.Length, t.Progress.Length));
            return t;
        }
    }
}
