using System.Collections.Generic;

namespace Gnomes.Core
{
    public enum Lang : byte { Ru = 0, En = 1 }

    /// <summary>Tiny localisation table. Russian is the default language.</summary>
    public static class Loc
    {
        public static Lang Current = Lang.Ru;

        static readonly Dictionary<string, string[]> table = new Dictionary<string, string[]>
        {
            // --- menus ---
            ["title"] = new[] { "ХИТРЫЕ ГНОМЫ", "SNEAKY GNOMES" },
            ["subtitle"] = new[] { "кооперативный стелс про воришек-гномов", "a co-op stealth game about tiny thieves" },
            ["solo"] = new[] { "Играть одному", "Play solo" },
            ["host"] = new[] { "Создать кооп", "Host co-op" },
            ["join"] = new[] { "Подключиться", "Join co-op" },
            ["settings"] = new[] { "Настройки", "Settings" },
            ["quit"] = new[] { "Выход", "Quit" },
            ["back"] = new[] { "Назад", "Back" },
            ["resume"] = new[] { "Продолжить", "Resume" },
            ["toMenu"] = new[] { "В главное меню", "Main menu" },
            ["name"] = new[] { "Имя гнома", "Gnome name" },
            ["hatColor"] = new[] { "Цвет колпака", "Hat colour" },
            ["address"] = new[] { "IP-адрес хоста", "Host IP address" },
            ["port"] = new[] { "Порт", "Port" },
            ["connect"] = new[] { "Подключиться", "Connect" },
            ["connecting"] = new[] { "Подключение...", "Connecting..." },
            ["lanGames"] = new[] { "Игры в локальной сети", "LAN games" },
            ["noLanGames"] = new[] { "Не найдено (ищем...)", "None found (searching...)" },
            ["language"] = new[] { "Язык", "Language" },
            ["sensitivity"] = new[] { "Чувствительность мыши", "Mouse sensitivity" },
            ["volume"] = new[] { "Громкость", "Volume" },
            ["invertY"] = new[] { "Инвертировать Y", "Invert Y" },
            ["fov"] = new[] { "Поле зрения", "Field of view" },
            ["quality"] = new[] { "Качество", "Quality" },
            ["resetSave"] = new[] { "Сбросить прогресс", "Reset progress" },
            ["players"] = new[] { "Гномы", "Gnomes" },
            ["hostInfo"] = new[] { "Друзья подключаются по вашему IP. Порт", "Friends connect to your IP. Port" },
            ["disconnected"] = new[] { "Соединение потеряно", "Disconnected" },
            ["ok"] = new[] { "Ок", "OK" },

            // --- hub ---
            ["hub"] = new[] { "Парящий остров", "Floating island" },
            ["craftBench"] = new[] { "Верстак", "Crafting bench" },
            ["portal"] = new[] { "Грибной портал: начать ночь", "Mushroom portal: start the night" },
            ["highGnomeTalk"] = new[] { "Поговорить с Верховным Гномом", "Talk to the High-Gnome" },
            ["craft"] = new[] { "Смастерить", "Craft" },
            ["owned"] = new[] { "Есть", "Owned" },
            ["notEnough"] = new[] { "Не хватает материалов", "Not enough materials" },
            ["waitingPlayers"] = new[] { "Ждём остальных гномов у портала", "Waiting for the others at the portal" },
            ["day"] = new[] { "Ночь", "Night" },
            ["strikes"] = new[] { "Выговоры", "Strikes" },

            // --- house HUD ---
            ["tasks"] = new[] { "Приказы Верховного Гнома", "High-Gnome's orders" },
            ["tasksNeed"] = new[] { "нужно выполнить", "required" },
            ["haul"] = new[] { "Добыча", "Haul" },
            ["pocket"] = new[] { "Карманы", "Pockets" },
            ["spores"] = new[] { "Споры", "Spores" },
            ["dawnIn"] = new[] { "До рассвета", "Dawn in" },
            ["grab"] = new[] { "ЛКМ — схватить", "LMB — grab" },
            ["interact"] = new[] { "E — использовать", "E — use" },
            ["pocketIt"] = new[] { "E — в карман", "E — pocket it" },
            ["struggle"] = new[] { "Жми F чтобы вырваться!", "Mash F to break free!" },
            ["caught"] = new[] { "Дед тебя поймал!", "The old man caught you!" },
            ["trapped"] = new[] { "Ты заперт! Жди помощи или вырывайся (F)", "You're locked in! Wait for help or mash F" },
            ["dead"] = new[] { "Ты погиб... Товарищи могут воскресить тебя у гриба", "You died... Friends can revive you at the mushroom" },
            ["deadSolo"] = new[] { "Гриб возрождает тебя...", "The mushroom is regrowing you..." },
            ["reviveAt"] = new[] { "E — воскресить павших гномов (1 спора)", "E — revive fallen gnomes (1 spore)" },
            ["goHome"] = new[] { "Удерживай E — закончить ночь и уйти домой", "Hold E — end the night and go home" },
            ["stashHint"] = new[] { "Неси добычу в корзину у гриба", "Bring loot to the basket by the mushroom" },
            ["oldManAsleep"] = new[] { "Дед спит", "Grandpa is asleep" },
            ["oldManSuspicious"] = new[] { "Дед что-то услышал...", "Grandpa heard something..." },
            ["oldManAlert"] = new[] { "ДЕД ТЕБЯ ВИДИТ!", "GRANDPA SEES YOU!" },
            ["armsRipped"] = new[] { "Руки оторвались! Отрастают...", "Arms ripped off! Regrowing..." },
            ["tooHeavy"] = new[] { "Слишком тяжело! Позови друга", "Too heavy! Get a friend" },
            ["taskDone"] = new[] { "Приказ выполнен", "Order complete" },
            ["banked"] = new[] { "В корзине", "Stashed" },
            ["dawn"] = new[] { "РАССВЕТ! Ночь окончена", "DAWN! The night is over" },

            // --- report ---
            ["report"] = new[] { "Отчёт для Верховного Гнома", "Report to the High-Gnome" },
            ["verdictGood"] = new[] { "Верховный Гном доволен. Возможно.", "The High-Gnome is pleased. Probably." },
            ["verdictBad"] = new[] { "Верховный Гном недоволен! Выговор!", "The High-Gnome is displeased! Strike!" },
            ["fired"] = new[] { "УВОЛЕН! Три выговора. Начинаем с первой ночи...", "FIRED! Three strikes. Back to night one..." },
            ["continue"] = new[] { "Продолжить", "Continue" },
            ["materials"] = new[] { "Материалы", "Materials" },

            // materials
            ["mat0"] = new[] { "Пласто", "Plasto" },
            ["mat1"] = new[] { "Звяк", "Clonk" },
            ["mat2"] = new[] { "Пух", "Fluff" },
            ["mat3"] = new[] { "Блеск", "Glint" },
            ["mat4"] = new[] { "Хрумк", "Munch" },
            ["mat5"] = new[] { "Гномий", "Gnomium" },

            // controls help
            ["controls"] = new[] {
                "WASD — ходить, Shift — бежать (шумно), Ctrl — красться, Пробел — прыжок\nЛКМ — схватить/карабкаться, колесо — длина рук, ПКМ — бросить/ударить\nE — использовать/в карман, Q — выложить карманы, G — зелье, V — камера, Tab — задания",
                "WASD — move, Shift — run (noisy), Ctrl — sneak, Space — jump\nLMB — grab/climb, wheel — arm length, RMB — throw/punch\nE — use/pocket, Q — empty pockets, G — potion, V — camera, Tab — orders" },
        };

        public static string T(string key)
        {
            if (table.TryGetValue(key, out var v)) return v[(int)Current];
            return key;
        }

        public static string T(string key, Lang lang)
        {
            if (table.TryGetValue(key, out var v)) return v[(int)lang];
            return key;
        }

        public static bool Has(string key) => table.ContainsKey(key);

        public static void Add(string key, string ru, string en) => table[key] = new[] { ru, en };

        public static string MatName(Mat m) => T("mat" + (int)m);
    }
}
