using System;
using System.Collections.Generic;
using Gnomes.Core;
using Godot;
using SockGang.App;

namespace SockGang.UI
{
    /// <summary>
    /// Little secrets hidden around the game:
    ///  - the Konami code in the main menu knits a rainbow hat (and it stays unlocked);
    ///  - secret gnome names get a reply (try "дед", "носок", "гэндальф"...);
    ///  - the dice in the menu rolls silly gnome names, one in forty is legendary;
    ///  - poking the menu gnome or the logo too often has consequences;
    ///  - whistling three times next to the Great Sock makes it tell a secret;
    ///  - winter holidays bring snow to the menu, April Fools makes grandpa suspicious.
    /// </summary>
    public static class EasterEggs
    {
        static readonly Key[] Konami = { Key.Up, Key.Up, Key.Down, Key.Down, Key.Left, Key.Right, Key.Left, Key.Right, Key.B, Key.A };
        static int konamiStep;
        static readonly Random rng = new Random();

        /// <summary>Feed key presses while the menu is open; true when the whole code was typed.</summary>
        public static bool KonamiKey(Key k)
        {
            if (k == Konami[konamiStep]) konamiStep++;
            else konamiStep = k == Konami[0] ? 1 : 0;
            if (konamiStep < Konami.Length) return false;
            konamiStep = 0;
            return true;
        }

        // ------------------------------------------------------------------ names

        static readonly string[] FirstRu = { "Носок", "Пятка", "Клубок", "Шнурок", "Пуговка", "Напёрсток", "Помпон", "Штопка", "Петелька", "Спица", "Катушка", "Заплатка", "Мухомор", "Сухарик", "Тапок" };
        static readonly string[] LastRu = { "Шустрый", "Тихоход", "Хитрюга", "Бородач", "Непоседа", "Ворчун", "Храпун", "Шалун", "Ловкач", "Соня", "Растяпа", "Проныра", "Весельчак", "Грозный", "Мягкий" };
        static readonly string[] FirstEn = { "Sock", "Heel", "Yarn", "Lace", "Button", "Thimble", "Pompom", "Darn", "Loop", "Needle", "Spool", "Patch", "Toadstool", "Crumb", "Slipper" };
        static readonly string[] LastEn = { "the Swift", "Tiptoe", "the Sly", "Beardy", "Fidget", "the Grumpy", "Snorer", "the Cheeky", "Quickhands", "Sleepy", "Butterfingers", "the Sneaky", "the Jolly", "the Dread", "the Soft" };
        static readonly string[] LegendRu = { "Гэндальф Серый Носок", "Шерлок Носкс", "Носферату", "Носкорадамус", "Лев Носков", "Капитан Штопка", "Носкофер Колумб", "Пятка Великолепный" };
        static readonly string[] LegendEn = { "Gandalf the Grey Sock", "Sherlock Socks", "Sockferatu", "Sockstradamus", "Sir Darns-a-Lot", "Captain Darn", "Sockrates", "Heel the Magnificent" };

        /// <summary>A silly gnome name; one roll in forty is legendary (and says so).</summary>
        public static string RandomName(out bool legendary)
        {
            bool ru = Loc.Current == Lang.Ru;
            legendary = rng.Next(40) == 0;
            if (legendary)
            {
                var l = ru ? LegendRu : LegendEn;
                return l[rng.Next(l.Length)];
            }
            var f = ru ? FirstRu : FirstEn;
            var s = ru ? LastRu : LastEn;
            string name = f[rng.Next(f.Length)] + " " + s[rng.Next(s.Length)];
            return name.Length > 16 ? f[rng.Next(f.Length)] + rng.Next(10, 99) : name;
        }

        /// <summary>A reply when a secret name is typed in the menu (null: nothing special).</summary>
        public static string NameReaction(string name)
        {
            string n = (name ?? "").Trim().ToLowerInvariant();
            switch (n)
            {
                case "дед":
                case "дедушка":
                case "grandpa":
                    return Loc.T("eggGrandpa");
                case "носок":
                case "sock":
                    return Loc.T("eggSock");
                case "гэндальф":
                case "gandalf":
                    return Loc.T("eggGandalf");
                case "кеша":
                case "kesha":
                    return Loc.T("eggKesha");
                case "барсик":
                case "barsik":
                    return Loc.T("eggBarsik");
                case "гном":
                case "gnome":
                    return Loc.T("eggGnome");
                case "claude":
                case "клод":
                    return Loc.T("eggClaude");
            }
            return null;
        }

        // ------------------------------------------------------------------ pokes

        public static string PokeGnome(int pokes) => pokes switch
        {
            5 => Loc.T("eggPoke5"),
            10 => Loc.T("eggPoke10"),
            20 => Loc.T("eggPoke20"),
            _ => null,
        };

        public static string PokeLogo(int pokes) => pokes switch
        {
            3 => Loc.T("eggLogo3"),
            7 => Loc.T("eggLogo7"),
            12 => Loc.T("eggLogo12"),
            _ => null,
        };

        // ------------------------------------------------------------------ calendar

        public static bool Winter
        {
            get
            {
                var d = DateTime.Now;
                return (d.Month == 12 && d.Day >= 20) || (d.Month == 1 && d.Day <= 10);
            }
        }

        public static bool AprilFools => DateTime.Now.Month == 4 && DateTime.Now.Day == 1;

        public static string CalendarGreeting() => Winter ? Loc.T("eggWinter") : AprilFools ? Loc.T("eggApril") : null;

        // ------------------------------------------------------------------ tips & wisdom

        public const int TipCount = 12;
        public const int WisdomCount = 8;

        public static string Tip() => Loc.T("tip" + rng.Next(TipCount));
        public static string Wisdom() => Loc.T("wisdom" + rng.Next(WisdomCount));

        // ------------------------------------------------------------------ the Great Sock's secrets

        static readonly List<float> whistles = new List<float>();
        static int secret;

        /// <summary>Three whistles within four seconds near the Great Sock: it tells one of its secrets.</summary>
        public static string Whistle(float now, bool nearSock)
        {
            if (!nearSock)
            {
                whistles.Clear();
                return null;
            }
            whistles.Add(now);
            whistles.RemoveAll(t => now - t > 4f);
            if (whistles.Count < 3) return null;
            whistles.Clear();
            return Loc.T("sockSecret" + (secret++ % 5));
        }
    }
}
