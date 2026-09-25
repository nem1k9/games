using System;
using System.Collections.Generic;

namespace Gnomes.Core
{
    /// <summary>Crafting materials that stolen goods are broken down into.</summary>
    public enum Mat : byte
    {
        Plasto = 0, // plastic junk
        Clonk = 1, // metal
        Fluff = 2, // fabric / paper
        Glint = 3, // glass, ceramics, jewels
        Munch = 4, // food
        Gnomium = 5, // magic currency paid by the High-Gnome for tasks
    }

    [Flags]
    public enum ItemFlags
    {
        None = 0,
        Breakable = 1,
        Pocketable = 2,
        Squeaky = 4,
        Noisy = 8, // alarm clock etc.
        Food = 16,
        Heavy = 32, // needs teamwork or upgrades
        Debris = 64,
    }

    public sealed class ItemDef
    {
        public string Kind;
        public string NameRu, NameEn;
        public float Mass; // a gnome weighs 3
        public int[] Yield = new int[6]; // per Mat
        public ItemFlags Flags;
        public float BreakImpulse = 4f;
        public float Loudness = 1f;
        public float Friction = 0.6f;
        public float Bounce = 0.1f;
        public string[] Tags = Array.Empty<string>();
        public string SpillKind;
        public int SpillCount;

        public bool Has(ItemFlags f) => (Flags & f) != 0;
        public bool HasTag(string tag) => Array.IndexOf(Tags, tag) >= 0;

        public int Value
        {
            get
            {
                int v = 0;
                for (int i = 0; i < 5; i++) v += Yield[i];
                return v;
            }
        }

        public string Name(Lang lang) => lang == Lang.Ru ? NameRu : NameEn;
    }

    public static class ItemDefs
    {
        static readonly Dictionary<string, ItemDef> defs = new Dictionary<string, ItemDef>();
        static readonly List<string> order = new List<string>();

        public static IReadOnlyList<string> Kinds => order;

        public static ItemDef Get(string kind)
        {
            if (defs.TryGetValue(kind, out var d)) return d;
            throw new KeyNotFoundException("Unknown item kind: " + kind);
        }

        public static bool TryGet(string kind, out ItemDef d) => defs.TryGetValue(kind, out d);

        static ItemDef D(string kind, string ru, string en, float mass, string yield, ItemFlags flags = ItemFlags.None, params string[] tags)
        {
            var d = new ItemDef { Kind = kind, NameRu = ru, NameEn = en, Mass = mass, Flags = flags, Tags = tags };
            // yield syntax: "clonk2 glint1"
            foreach (var part in yield.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int i = 0;
                while (i < part.Length && char.IsLetter(part[i])) i++;
                var name = part.Substring(0, i);
                int n = i < part.Length ? int.Parse(part.Substring(i)) : 1;
                Mat m;
                switch (name)
                {
                    case "plasto": m = Mat.Plasto; break;
                    case "clonk": m = Mat.Clonk; break;
                    case "fluff": m = Mat.Fluff; break;
                    case "glint": m = Mat.Glint; break;
                    case "munch": m = Mat.Munch; break;
                    default: throw new ArgumentException("bad material " + name);
                }
                d.Yield[(int)m] += n;
            }
            if (mass >= 3.2f) d.Flags |= ItemFlags.Heavy;
            defs[kind] = d;
            order.Add(kind);
            return d;
        }

        static ItemDefs()
        {
            const ItemFlags B = ItemFlags.Breakable, P = ItemFlags.Pocketable, F = ItemFlags.Food;
            // kitchen
            D("mug", "Кружка", "Mug", 0.4f, "glint1", B, "dish").BreakImpulse = 3.2f;
            D("plate", "Тарелка", "Plate", 0.5f, "glint1", B, "plate", "dish").BreakImpulse = 2.6f;
            D("teapot", "Чайник заварочный", "Teapot", 1.1f, "glint2", B, "dish").BreakImpulse = 4f;
            D("kettle", "Чайник", "Kettle", 1.6f, "clonk2").Loudness = 1.3f;
            D("toaster", "Тостер", "Toaster", 4.5f, "clonk4", ItemFlags.None, "appliance").Loudness = 1.6f;
            D("pot", "Кастрюля", "Pot", 2f, "clonk3").Loudness = 1.8f;
            D("pan", "Сковородка", "Frying pan", 1.8f, "clonk3", ItemFlags.None, "weapon").Loudness = 2f;
            D("spoon", "Ложка", "Spoon", 0.15f, "clonk1", P);
            D("fork", "Вилка", "Fork", 0.15f, "clonk1", P);
            D("can", "Консервы", "Tin can", 0.5f, "clonk1 munch1").Loudness = 1.2f;
            D("bottle", "Бутылка", "Bottle", 0.7f, "glint2", B).BreakImpulse = 3.2f;
            D("cheese", "Сыр", "Cheese", 0.5f, "munch2", F, "food");
            D("apple", "Яблоко", "Apple", 0.25f, "munch1", P | F, "food");
            D("sausage", "Колбаса", "Sausage", 0.3f, "munch2", P | F, "food");
            D("bread", "Батон", "Bread", 0.6f, "munch2", F, "food");
            D("catBowl", "Кошачья миска", "Cat bowl", 0.6f, "plasto1 munch1", ItemFlags.None, "catBowl");
            var jar = D("cookieJar", "Банка с печеньем", "Cookie jar", 1.4f, "glint1 munch3", B);
            jar.BreakImpulse = 4.2f; jar.SpillKind = "cookie"; jar.SpillCount = 4;
            D("cookie", "Печенька", "Cookie", 0.1f, "munch1", P | F, "food");

            // living room / study
            D("remote", "Пульт", "TV remote", 0.2f, "plasto1", P, "remote");
            var vase = D("vase", "Ваза", "Vase", 1f, "glint2", B, "vase");
            vase.BreakImpulse = 2.6f; vase.Loudness = 1.6f;
            D("book", "Книга", "Book", 0.8f, "fluff2", ItemFlags.None, "book");
            D("photoFrame", "Фоторамка", "Photo frame", 0.4f, "glint1", B).BreakImpulse = 3.5f;
            D("candle", "Свечка", "Candle", 0.2f, "munch1", P);
            D("globe", "Глобус", "Globe", 1.6f, "plasto2 clonk1");
            D("trophy", "Золотой кубок", "Golden trophy", 3.5f, "clonk3 glint3", ItemFlags.None, "valuable", "trophy").Loudness = 1.5f;
            var piggy = D("piggyBank", "Копилка-свинка", "Piggy bank", 1.4f, "glint1", B, "piggyBank");
            piggy.BreakImpulse = 3.2f; piggy.SpillKind = "coins"; piggy.SpillCount = 4; piggy.Loudness = 1.5f;
            D("alarmClock", "Будильник", "Alarm clock", 0.6f, "clonk1 plasto1", ItemFlags.Noisy, "alarmClock").Loudness = 1.5f;
            D("telephone", "Телефон", "Telephone", 1.5f, "plasto2 clonk1");
            D("yarn", "Клубок", "Ball of yarn", 0.3f, "fluff2", ItemFlags.None, "catToy");

            // valuables
            D("coins", "Монетки", "Coins", 0.3f, "glint2 clonk1", P, "valuable", "coins");
            D("ring", "Кольцо", "Ring", 0.1f, "glint4", P, "valuable", "jewel");
            D("watch", "Золотые часы", "Gold watch", 0.15f, "glint3 clonk2", P, "valuable", "watch");
            D("pearls", "Жемчуг", "Pearls", 0.15f, "glint4", P, "valuable", "jewel");
            var jb = D("jewelBox", "Шкатулка", "Jewel box", 0.8f, "fluff1 glint2", B, "valuable");
            jb.BreakImpulse = 6f; jb.SpillKind = "ring"; jb.SpillCount = 1;
            D("goldBar", "Слиток золота", "Gold bar", 1.2f, "glint6 clonk2", P, "valuable", "gold");
            D("cash", "Пачка денег", "Wad of cash", 0.1f, "fluff3", P, "valuable", "cash");
            D("pipe", "Трубка деда", "Grandpa's pipe", 0.15f, "fluff1 clonk1", P, "valuable");

            // the old man's personal things
            D("dentures", "Вставная челюсть", "Dentures", 0.15f, "plasto2", P | ItemFlags.Squeaky, "dentures");
            D("glasses", "Очки деда", "Grandpa's glasses", 0.1f, "glint1 plasto1", P, "glasses");
            D("hearingAid", "Слуховой аппарат", "Hearing aid", 0.05f, "plasto2 clonk1", P, "hearingAid");
            D("slipper", "Тапок", "Slipper", 0.4f, "fluff2", ItemFlags.None, "slipper");
            D("sock", "Носок", "Sock", 0.1f, "fluff1", P, "sock");

            // bathroom
            D("duck", "Резиновая уточка", "Rubber duck", 0.2f, "plasto1", ItemFlags.Squeaky, "duck").Bounce = 0.5f;
            D("toiletPaper", "Туалетная бумага", "Toilet paper", 0.2f, "fluff1", ItemFlags.None, "toiletPaper");
            D("soap", "Мыло", "Soap", 0.15f, "munch1", P).Friction = 0.05f;
            D("shampoo", "Шампунь", "Shampoo", 0.4f, "plasto2");
            D("toothbrush", "Зубная щётка", "Toothbrush", 0.05f, "plasto1", P);

            // misc / large
            D("chair", "Стул", "Chair", 7f, "fluff3 clonk1", ItemFlags.None, "furniture").Loudness = 2f;
            var bin = D("trashBin", "Мусорное ведро", "Trash bin", 3f, "clonk2", ItemFlags.None, "trashBin");
            bin.Loudness = 2.2f; bin.SpillKind = "can"; bin.SpillCount = 2;
            D("box", "Коробка", "Cardboard box", 1.5f, "fluff2");
            D("battery", "Батарейка", "Battery", 0.08f, "clonk1 plasto1", P);
            D("lightbulb", "Лампочка", "Light bulb", 0.08f, "glint1 clonk1", B | P).BreakImpulse = 2f;
            D("shard", "Осколок", "Shard", 0.03f, "", ItemFlags.Debris, "debris");
        }
    }
}
