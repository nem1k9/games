using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Gnomes.Core.Rules
{
    public enum GearId : byte
    {
        HopperGaiters = 0, // jump higher
        LongYarn = 1, // yarn hook reaches further
        EndlessYarn = 2, // even further (needs LongYarn)
        StrongMittens = 3, // carry heavier things
        QuietBooties = 4, // silent steps
        BigPocket = 5, // +2 pocket slots
        SpareHat = 6, // +1 revive per night
        SleepDust = 7, // consumable: puts grandpa to sleep
        OwlGoggles = 8, // see in the dark
        ParachuteHat = 9, // glide by holding jump in the air
    }

    public sealed class GearDef
    {
        public GearId Id;
        public string Ru, En, DescRu, DescEn;
        public int[] Cost = new int[6];
        public bool Consumable;
        public GearId? Requires;
        public string Model; // icon model name for the hub
        public string Name(Lang l) => l == Lang.Ru ? Ru : En;
        public string Desc(Lang l) => l == Lang.Ru ? DescRu : DescEn;
    }

    public static class GearCatalog
    {
        public static readonly List<GearDef> All = new List<GearDef>();

        static GearDef G(GearId id, string ru, string en, string dru, string den, string cost, bool consumable = false, GearId? req = null)
        {
            var g = new GearDef { Id = id, Ru = ru, En = en, DescRu = dru, DescEn = den, Consumable = consumable, Requires = req };
            foreach (var part in cost.Split(' '))
            {
                if (part.Length == 0) continue;
                int i = 0;
                while (i < part.Length && char.IsLetter(part[i])) i++;
                int n = int.Parse(part.Substring(i));
                switch (part.Substring(0, i))
                {
                    case "buttons": g.Cost[0] += n; break;
                    case "bolts": g.Cost[1] += n; break;
                    case "yarn": g.Cost[2] += n; break;
                    case "glitter": g.Cost[3] += n; break;
                    case "crumbs": g.Cost[4] += n; break;
                    case "giggles": g.Cost[5] += n; break;
                    default: throw new ArgumentException(part);
                }
            }
            All.Add(g);
            return g;
        }

        static GearCatalog()
        {
            G(GearId.ParachuteHat, "Колпак-парашют", "Parachute hat", "Держи Пробел в воздухе — плавно планируешь", "Hold Space in the air to glide", "yarn5 buttons2");
            G(GearId.LongYarn, "Длинная пряжа", "Long yarn", "Клубок-крюк летит дальше", "The yarn hook reaches further", "yarn6 bolts2");
            G(GearId.EndlessYarn, "Бесконечный клубок", "Endless yarn", "Клубок-крюк летит очень далеко", "The yarn hook reaches very far", "yarn8 glitter3 giggles6", false, GearId.LongYarn);
            G(GearId.StrongMittens, "Силачьи варежки", "Mighty mittens", "Носишь вещи в 1.6 раза тяжелее", "Carry 1.6x heavier things", "yarn4 bolts3 giggles2");
            G(GearId.QuietBooties, "Тихие пинетки", "Quiet booties", "Шагов почти не слышно", "Footsteps are almost silent", "yarn6");
            G(GearId.BigPocket, "Большой карман", "Big pocket", "+2 места в карманах", "+2 pocket slots", "yarn4 buttons3");
            G(GearId.HopperGaiters, "Кузнечиковые гетры", "Hopper gaiters", "Прыгаешь намного выше", "Jump much higher", "yarn3 bolts4");
            G(GearId.SpareHat, "Запасной колпак", "Spare hat", "+1 возрождение за ночь", "+1 revive per night", "yarn5 crumbs3 giggles4");
            G(GearId.OwlGoggles, "Совиные очки", "Owl goggles", "Лучше видно в темноте", "See better in the dark", "glitter4 buttons2");
            G(GearId.SleepDust, "Сонная пыльца", "Sleep dust", "Кинь в деда (G) — уснёт на 25 сек", "Throw at grandpa (G) — sleeps for 25s", "crumbs3 glitter1", true);
        }

        public static GearDef Get(GearId id) => All.Find(g => g.Id == id);
    }

    /// <summary>Numeric effects of owned gear.</summary>
    public struct GearEffects
    {
        public float JumpSpeed, YarnRange, StrengthMul, FootstepNoiseMul, DarkVision;
        public int PocketSize, BonusHats;
        public bool Parachute;

        public static GearEffects From(SaveData s)
        {
            var e = new GearEffects
            {
                JumpSpeed = GameConsts.JumpSpeed,
                YarnRange = GameConsts.YarnRange,
                StrengthMul = 1f,
                FootstepNoiseMul = 1f,
                PocketSize = GameConsts.PocketSize,
                BonusHats = 0,
                DarkVision = 0,
            };
            if (s == null) return e;
            if (s.Has(GearId.HopperGaiters)) e.JumpSpeed = GameConsts.HopperJumpSpeed;
            if (s.Has(GearId.LongYarn)) e.YarnRange = GameConsts.YarnRangeLong;
            if (s.Has(GearId.EndlessYarn)) e.YarnRange = GameConsts.YarnRangeEndless;
            if (s.Has(GearId.StrongMittens)) e.StrengthMul = 1.6f;
            if (s.Has(GearId.QuietBooties)) e.FootstepNoiseMul = 0.35f;
            if (s.Has(GearId.BigPocket)) e.PocketSize += 2;
            if (s.Has(GearId.SpareHat)) e.BonusHats = 1;
            if (s.Has(GearId.OwlGoggles)) e.DarkVision = 1;
            e.Parachute = s.Has(GearId.ParachuteHat);
            return e;
        }
    }

    /// <summary>Persistent progression of a crew (the host's save is used in co-op).</summary>
    public sealed class SaveData
    {
        public int Night = 1;
        public int Strikes;
        public int[] Materials = new int[6];
        public HashSet<GearId> Gear = new HashSet<GearId>();
        public int Potions;
        public int TotalLoot;
        public int BestNight = 1;
        public int TimesFired;

        public bool Has(GearId g) => Gear.Contains(g);

        public bool CanAfford(GearDef g)
        {
            for (int i = 0; i < 6; i++) if (Materials[i] < g.Cost[i]) return false;
            return true;
        }

        public bool CanCraft(GearDef g, out string reason)
        {
            reason = null;
            if (!g.Consumable && Has(g.Id)) { reason = "owned"; return false; }
            if (g.Requires.HasValue && !Has(g.Requires.Value)) { reason = "requires"; return false; }
            if (!CanAfford(g)) { reason = "notEnough"; return false; }
            return true;
        }

        public bool Craft(GearId id)
        {
            var g = GearCatalog.Get(id);
            if (g == null || !CanCraft(g, out _)) return false;
            for (int i = 0; i < 6; i++) Materials[i] -= g.Cost[i];
            if (g.Consumable)
            {
                if (id == GearId.SleepDust) Potions++;
            }
            else Gear.Add(id);
            return true;
        }

        /// <summary>Apply the result of a night. Returns true if the crew got fired.</summary>
        public bool ApplyReport(NightReport r)
        {
            for (int i = 0; i < 5; i++) Materials[i] += r.Haul[i];
            Materials[(int)Mat.Giggles] += r.Giggles;
            TotalLoot += r.HaulValue;
            Potions = Math.Max(0, Potions - r.PotionsUsed);
            if (!r.Passed) Strikes++;
            if (Strikes >= GameConsts.MaxStrikes)
            {
                // Disbanded: the Great Sock confiscates the stash and the gang starts over. Gear is kept.
                TimesFired++;
                Night = 1;
                Strikes = 0;
                Materials = new int[6];
                return true;
            }
            Night++;
            BestNight = Math.Max(BestNight, Night);
            return false;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append("version=1\n");
            sb.Append("night=").Append(Night).Append('\n');
            sb.Append("strikes=").Append(Strikes).Append('\n');
            for (int i = 0; i < 6; i++) sb.Append("mat").Append(i).Append('=').Append(Materials[i]).Append('\n');
            var gear = new List<string>();
            foreach (var g in Gear) gear.Add(((int)g).ToString(CultureInfo.InvariantCulture));
            gear.Sort();
            sb.Append("gear=").Append(string.Join(",", gear)).Append('\n');
            sb.Append("potions=").Append(Potions).Append('\n');
            sb.Append("totalLoot=").Append(TotalLoot).Append('\n');
            sb.Append("bestNight=").Append(BestNight).Append('\n');
            sb.Append("fired=").Append(TimesFired).Append('\n');
            return sb.ToString();
        }

        public static SaveData Deserialize(string text)
        {
            var s = new SaveData();
            if (string.IsNullOrEmpty(text)) return s;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line.Substring(0, eq), v = line.Substring(eq + 1);
                int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n);
                switch (k)
                {
                    case "night": s.Night = Math.Max(1, n); break;
                    case "strikes": s.Strikes = Math.Max(0, Math.Min(GameConsts.MaxStrikes - 1, n)); break;
                    case "potions": s.Potions = Math.Max(0, n); break;
                    case "totalLoot": s.TotalLoot = n; break;
                    case "bestNight": s.BestNight = Math.Max(1, n); break;
                    case "fired": s.TimesFired = n; break;
                    case "gear":
                        foreach (var g in v.Split(','))
                            if (int.TryParse(g, out int gi) && Enum.IsDefined(typeof(GearId), (byte)gi)) s.Gear.Add((GearId)gi);
                        break;
                    default:
                        if (k.StartsWith("mat") && int.TryParse(k.Substring(3), out int mi) && mi >= 0 && mi < 6) s.Materials[mi] = Math.Max(0, n);
                        break;
                }
            }
            return s;
        }
    }

    /// <summary>Summary of a finished night.</summary>
    public sealed class NightReport
    {
        public int Night;
        public string[] TaskIds = Array.Empty<string>();
        public bool[] TaskDone = Array.Empty<bool>();
        public int TasksDone;
        public bool Passed;
        public int[] Haul = new int[6];
        public int HaulValue;
        public int Giggles;
        public int Chaos; // items left out of their room at dawn
        public int ItemsStolen;
        public int TimesCaught;
        public int Deaths;
        public int PotionsUsed;
        public bool Fired;
        public bool Dawn; // ended by the sunrise rather than leaving
    }

    /// <summary>Authoritative state of the current night (lives on the host).</summary>
    public sealed class NightState
    {
        public int Night;
        public int Seed;
        public float TimeLeft = GameConsts.NightSeconds;
        public TaskTracker Tasks;
        public readonly int[] Haul = new int[6];
        public int HaulValue;
        public int ItemsStolen;
        public int SpareHats;
        public int Chaos;
        public int TimesCaught;
        public int Deaths;
        public int PotionsUsed;
        public bool Over;
        public readonly Dictionary<string, int> BankedByKind = new Dictionary<string, int>();

        public NightState(int night, int seed, int players, GearEffects gear)
        {
            Night = night;
            Seed = seed;
            Tasks = new TaskTracker(TaskCatalog.PickForNight(night, seed));
            SpareHats = (players <= 1 ? GameConsts.SpareHatsSolo : GameConsts.SpareHatsCoop) + gear.BonusHats;
        }

        /// <summary>0 at midnight .. 1 at dawn.</summary>
        public float Progress01 => 1f - TimeLeft / GameConsts.NightSeconds;

        /// <summary>In-game clock "HH:MM" (00:00 -> 06:00).</summary>
        public string Clock
        {
            get
            {
                float hours = Progress01 * 6f;
                int h = (int)hours;
                int m = (int)((hours - h) * 60);
                return $"{h:00}:{m:00}";
            }
        }

        public List<int> Bank(string kind)
        {
            var d = ItemDefs.Get(kind);
            for (int i = 0; i < 5; i++) Haul[i] += d.Yield[i];
            HaulValue += d.Value;
            ItemsStolen++;
            BankedByKind.TryGetValue(kind, out int n);
            BankedByKind[kind] = n + 1;
            return Tasks.Apply(GameEvent.Banked(kind, d.Value));
        }

        public NightReport MakeReport(bool dawn)
        {
            var r = new NightReport
            {
                Night = Night,
                TaskIds = Tasks.Ids(),
                TaskDone = new bool[Tasks.Tasks.Count],
                TasksDone = Tasks.CompletedCount,
                HaulValue = HaulValue,
                Giggles = Tasks.GigglesEarned + Chaos / 2,
                Chaos = Chaos,
                ItemsStolen = ItemsStolen,
                TimesCaught = TimesCaught,
                Deaths = Deaths,
                PotionsUsed = PotionsUsed,
                Dawn = dawn,
            };
            for (int i = 0; i < r.TaskDone.Length; i++) r.TaskDone[i] = Tasks.IsDone(i);
            r.Passed = r.TasksDone >= Math.Min(GameConsts.TasksRequired, Tasks.Tasks.Count);
            Array.Copy(Haul, r.Haul, 6);
            return r;
        }
    }
}
