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
        Tied = 8, // two things tied together with yarn (Kind, Role = second kind or furniture)
        Unrolled = 9, // a toilet paper roll got unrolled across the floor
        Chaos = 10, // Value = how many items are currently out of their room
        Silenced = 11, // the parrot got covered / bribed
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
        public static GameEvent Tied(string a, string b) => new GameEvent { Type = Ev.Tied, Kind = a, Role = b };
        public static GameEvent Unrolled(string kind) => new GameEvent { Type = Ev.Unrolled, Kind = kind };
        public static GameEvent Chaos(int misplaced) => new GameEvent { Type = Ev.Chaos, Value = misplaced };
        public static GameEvent Silenced(string how) => new GameEvent { Type = Ev.Silenced, Kind = how };

        public bool ItemsTied(string tagA, string tagB)
        {
            if (Type != Ev.Tied) return false;
            bool a1 = HasTag(Kind, tagA), b1 = HasTag(Role, tagB);
            bool a2 = HasTag(Kind, tagB), b2 = HasTag(Role, tagA);
            return (a1 && b1) || (a2 && b2);
        }

        static bool HasTag(string kind, string tag) => kind != null && ItemDefs.TryGet(kind, out var d) && d.HasTag(tag);

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
        public int Reward => Tier * 2; // giggles
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
            // The Great Sock's list of pranks. Tier 1 = easy, 2 = medium, 3 = hard.
            // ---- easy ----
            T("flush", 1, "Смыть что-нибудь в унитаз", "Flush something down the toilet", e => If(e.Type == Ev.Flushed));
            T("tvNight", 1, "Включить деду телевизор посреди ночи", "Turn the TV on in the middle of the night", e => If(e.Type == Ev.Mech && e.Role == "tvPower" && e.On));
            T("floodBath", 1, "Открыть кран в ванне и уйти", "Turn on the bath tap and walk away", e => If(e.Type == Ev.Mech && e.Role == "tubFaucet" && e.On));
            T("duckToilet", 1, "Искупать уточку в унитазе", "Give the rubber duck a bath in the toilet", e => If(e.Type == Ev.InZone && e.Zone == "toiletBowl" && e.ItemHasTag("duck")));
            T("feedFish", 1, "Покормить рыбок (любую еду в аквариум)", "Feed the fish (any food into the tank)", e => If(e.Type == Ev.InZone && e.Zone == "fishTank" && e.ItemHasTag("food")));
            T("knockTrash", 1, "Опрокинуть мусорное ведро", "Knock over the trash bin", e => If(e.Type == Ev.Tipped && e.ItemHasTag("trashBin")));
            T("socks", 1, "Утащить в деревню 3 одиноких носка", "Bring 3 lonely socks to the village", e => If(e.Type == Ev.Banked && e.ItemHasTag("sock")), 3);
            T("openWindow", 1, "Открыть окно — пусть дед померзнет", "Open a window so grandpa gets chilly", e => If(e.Type == Ev.Mech && e.Role == "window" && e.On));
            T("unroll", 1, "Раскатать туалетную бумагу по полу", "Unroll the toilet paper across the floor", e => If(e.Type == Ev.Unrolled));
            T("alarmClock", 1, "Утащить будильник (пусть проспит)", "Steal the alarm clock (let him oversleep)", e => If(e.Type == Ev.Banked && e.ItemHasTag("alarmClock")));

            // ---- medium ----
            T("glassesFish", 2, "Спрятать очки деда в аквариум", "Hide grandpa's glasses in the fish tank", e => If(e.Type == Ev.InZone && e.Zone == "fishTank" && e.ItemHasTag("glasses")));
            T("glassesPlant", 2, "Спрятать очки деда в цветочный горшок", "Hide grandpa's glasses in a flower pot", e => If(e.Type == Ev.InZone && e.Zone == "plantPot" && e.ItemHasTag("glasses")));
            T("remoteFridge", 2, "Спрятать пульт в холодильник", "Hide the TV remote in the fridge", e => If(e.Type == Ev.InZone && e.Zone == "fridge" && e.ItemHasTag("remote")));
            T("denturesCat", 2, "Положить вставную челюсть в кошачью лежанку", "Put the dentures in the cat's bed", e => If(e.Type == Ev.InZone && e.Zone == "catBed" && e.ItemHasTag("dentures")));
            T("tieSlippers", 2, "Связать дедовы тапки пряжей", "Tie grandpa's slippers together with yarn", e => If(e.ItemsTied("slipper", "slipper")));
            T("catBowlOven", 2, "Спрятать кошачью миску в духовку", "Hide the cat bowl in the oven", e => If(e.Type == Ev.InZone && e.Zone == "oven" && e.ItemHasTag("catBowl")));
            T("keysFreezer", 2, "Спрятать ключи деда в морозилку", "Hide grandpa's keys in the freezer", e => If(e.Type == Ev.InZone && e.Zone == "freezer" && e.ItemHasTag("keys")));
            T("smashPlates", 2, "Разбить 3 тарелки", "Smash 3 plates", e => If(e.Type == Ev.Broken && e.ItemHasTag("plate")), 3);
            T("piggyBank", 2, "Разбить копилку", "Smash the piggy bank", e => If(e.Type == Ev.Broken && e.ItemHasTag("piggyBank")));
            T("breakTv", 2, "Сломать телевизор", "Break the TV", e => If(e.Type == Ev.FurnitureBroken && e.Kind == "tv"));
            T("hearingAid", 2, "Утащить слуховой аппарат (дед станет хуже слышать)", "Steal the hearing aid (he'll hear worse)", e => If(e.Type == Ev.Banked && e.ItemHasTag("hearingAid")));
            T("coverParrot", 2, "Заткнуть попугая Кешу (полотенце или печенька)", "Shut up Kesha the parrot (towel or cookie)", e => If(e.Type == Ev.Silenced));
            T("chaos10", 2, "Устроить переполох: 10 вещей не на своих местах", "Chaos: 10 things out of place", e => e.Type == Ev.Chaos ? e.Value : 0, 10);

            // ---- hard ----
            T("crackSafe", 3, "Вскрыть сейф (пинать замок)", "Crack the safe (kick the lock)", e => If(e.Type == Ev.Mech && e.Role == "safe" && e.On), 1, 2);
            T("stealTrophy", 3, "Утащить в деревню золотой кубок (тяжёлый!)", "Bring the golden trophy to the village (heavy!)", e => If(e.Type == Ev.Banked && e.ItemHasTag("trophy")));
            T("stealToaster", 3, "Утащить тостер (вдвоём легче)", "Steal the toaster (easier with a friend)", e => If(e.Type == Ev.Banked && e.Kind == "toaster"));
            T("loot60", 3, "Натаскать в деревню добра на 60", "Bring loot worth 60 to the village", e => e.Type == Ev.Banked ? e.Value : 0, 60, 2);
            T("chaos20", 3, "Большой переполох: 20 вещей не на своих местах", "Big chaos: 20 things out of place", e => e.Type == Ev.Chaos ? e.Value : 0, 20, 2);
            T("tieGrandpa", 3, "Привязать тапок деда к мебели", "Tie grandpa's slipper to the furniture", e => If(e.Type == Ev.Tied && (e.ItemHasTag("slipper") && e.Role == "furniture")), 1, 2);

            Get("glassesFish").ConflictsWith = new[] { "glassesPlant" };
            Get("glassesPlant").ConflictsWith = new[] { "glassesFish" };
            Get("chaos10").ConflictsWith = new[] { "chaos20" };
            Get("chaos20").ConflictsWith = new[] { "chaos10" };
            Get("tieSlippers").ConflictsWith = new[] { "tieGrandpa" };
            Get("tieGrandpa").ConflictsWith = new[] { "tieSlippers" };
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
                if (e.Type == Ev.Chaos) Progress[i] = Math.Min(Tasks[i].Goal, Math.Max(Progress[i], p)); // a level, not a sum
                else Progress[i] = Math.Min(Tasks[i].Goal, Progress[i] + p);
                if (IsDone(i)) done.Add(i);
            }
            return done;
        }

        public int GigglesEarned
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
