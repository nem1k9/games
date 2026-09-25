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
            ["title"] = new[] { "НОСОЧНАЯ БАНДА", "THE SOCK GANG" },
            ["subtitle"] = new[] { "куда на самом деле пропадают носки, очки и пульты", "where socks, glasses and remotes really go" },
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
            ["noLanGames"] = new[] { "Пока не найдено (ищем...)", "None found yet (searching...)" },
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
            ["playerJoined"] = new[] { "присоединился!", "joined!" },
            ["playerLeft"] = new[] { "ушёл", "left" },
            ["portFailed"] = new[] { "Не удалось открыть порт", "Could not open port" },
            ["ok"] = new[] { "Ок", "OK" },

            // --- village (hub) ---
            ["hub"] = new[] { "Деревня под крыльцом", "The village under the porch" },
            ["craftBench"] = new[] { "Вязальня", "Knitting corner" },
            ["portal"] = new[] { "Носочный лаз: начать ночь", "Sock tunnel: start the night" },
            ["highGnomeTalk"] = new[] { "Поговорить с Великим Носком", "Talk to the Great Sock" },
            ["greatSock"] = new[] { "Великий Носок", "The Great Sock" },
            ["craft"] = new[] { "Связать", "Knit" },
            ["owned"] = new[] { "Уже есть", "Owned" },
            ["notEnough"] = new[] { "Не хватает материалов", "Not enough materials" },
            ["waitingPlayers"] = new[] { "Ждём остальных гномов у лаза", "Waiting for the others at the tunnel" },
            ["day"] = new[] { "Ночь", "Night" },
            ["strikes"] = new[] { "Выговоры", "Strikes" },
            ["sockSays"] = new[] {
                "Шурх-шурх... Слушай, гном. Дед опять спит слишком спокойно. Вот тебе список проказ на эту ночь. Выполни хотя бы три — и Носок будет доволен. А если утащишь одинокий носок — вдвойне доволен.",
                "Swish-swish... Listen, gnome. Grandpa sleeps far too peacefully again. Here is tonight's list of pranks. Do at least three and the Sock will be pleased. Bring a lonely sock and it will be twice as pleased." },

            // --- house HUD ---
            ["tasks"] = new[] { "Список проказ", "List of pranks" },
            ["tasksNeed"] = new[] { "нужно выполнить", "required" },
            ["haul"] = new[] { "Добыча", "Loot" },
            ["pocket"] = new[] { "Карманы", "Pockets" },
            ["spareHats"] = new[] { "Запасные колпаки", "Spare hats" },
            ["chaos"] = new[] { "Переполох", "Chaos" },
            ["dawnIn"] = new[] { "До рассвета", "Dawn in" },
            ["grab"] = new[] { "ЛКМ — взять", "LMB — pick up" },
            ["interact"] = new[] { "E — использовать", "E — use" },
            ["pocketIt"] = new[] { "E — в карман", "E — pocket it" },
            ["struggle"] = new[] { "Жми F — раскачивай банку!", "Mash F — rock the jar!" },
            ["carriedStruggle"] = new[] { "Жми F — вырывайся!", "Mash F — wriggle free!" },
            ["caught"] = new[] { "Дед тебя поймал!", "Grandpa caught you!" },
            ["trapped"] = new[] { "Тебя закатали в банку! Жди друзей или раскачивай банку (F)", "You got pickled! Wait for friends or rock the jar (F)" },
            ["dead"] = new[] { "Тебя прихлопнули! Друзья могут отнести твой колпак в корзинку с клубками", "You got swatted! Friends can carry your hat to the yarn basket" },
            ["deadSolo"] = new[] { "Тебя перевязывают заново...", "Re-knitting you..." },
            ["wentHome"] = new[] { "Ты ушёл в деревню. Жди остальных", "You went back to the village. Wait for the others" },
            ["reviveAt"] = new[] { "Принеси колпак павшего гнома в корзинку", "Bring a fallen gnome's hat to the basket" },
            ["goHome"] = new[] { "Удерживай E — закончить ночь и уйти в деревню", "Hold E — end the night and go back to the village" },
            ["stashHint"] = new[] { "Тащи добычу в тележку у лаза", "Bring loot to the cart by the tunnel" },
            ["oldManAsleep"] = new[] { "Дед спит", "Grandpa is asleep" },
            ["oldManSuspicious"] = new[] { "Дед что-то услышал...", "Grandpa heard something..." },
            ["oldManAlert"] = new[] { "ДЕД ТЕБЯ ВИДИТ!", "GRANDPA SEES YOU!" },
            ["parrotAlarm"] = new[] { "Попугай: «ВОРЫ! ВОРЫ!»", "Parrot: \"THIEVES! THIEVES!\"" },
            ["yarnSnapped"] = new[] { "Нитка порвалась!", "The yarn snapped!" },
            ["gripLost"] = new[] { "Руки устали — отцепился", "Your hands got tired" },
            ["tooHeavy"] = new[] { "Слишком тяжело! Позови друга или подтяни пряжей", "Too heavy! Get a friend or pull it with yarn" },
            ["taskDone"] = new[] { "Проказа выполнена", "Prank done" },
            ["banked"] = new[] { "В тележке", "In the cart" },
            ["dawn"] = new[] { "КУКАРЕКУ! Рассвет — ночь окончена", "COCK-A-DOODLE-DOO! Dawn — the night is over" },
            ["tied"] = new[] { "Связано!", "Tied!" },
            ["tieHint"] = new[] { "T — привязать нитку сюда", "T — tie the yarn here" },
            ["unscrew"] = new[] { "Удерживай E — открутить крышку", "Hold E — unscrew the lid" },
            ["hatHere"] = new[] { "Колпак гнома: отнеси в корзинку с клубками", "A gnome's hat: bring it to the yarn basket" },
            ["revived"] = new[] { "Гнома перевязали!", "A gnome got re-knitted!" },
            ["parrotQuiet"] = new[] { "Кеша замолчал", "Kesha went quiet" },
            ["mousetrap"] = new[] { "Мышеловка!", "Mousetrap!" },
            ["pocketFull"] = new[] { "Карманы полны! (Q — выложить)", "Pockets full! (Q — empty them)" },
            ["hostHelp"] = new[] {
                "Друзья в той же сети вводят один из IP выше. Через интернет: Radmin VPN / ZeroTier (одна виртуальная сеть) или проброс UDP-порта на роутере.",
                "Friends on the same network enter one of the IPs above. Over the internet: Radmin VPN / ZeroTier (one virtual network) or forward the UDP port on your router." },
            ["oldManAwake"] = new[] { "Дед бродит по дому", "Grandpa is wandering around" },
            ["portalHint"] = new[] { "Залезь в носочный лаз, чтобы начать ночь. Верстак-вязальня — слева, Великий Носок — в глубине.", "Crawl into the sock tunnel to start the night. The knitting corner is on the left, the Great Sock at the back." },
            ["craftHostOnly"] = new[] { "Прогресс общий: вяжет хост банды.", "Progress is shared: the host's gang does the knitting." },
            ["waitHost"] = new[] { "Ждём, когда хост продолжит...", "Waiting for the host to continue..." },
            ["yarn"] = new[] { "Нитка", "Yarn" },
            ["sockTip0"] = new[] { "Совет Носка: под кроватью дед тебя не достанет.", "Sock tip: grandpa can't reach you under the bed." },
            ["sockTip1"] = new[] { "Совет Носка: крадись (Ctrl) по скрипучим половицам — не будут скрипеть.", "Sock tip: sneak (Ctrl) over creaky boards and they won't creak." },
            ["sockTip2"] = new[] { "Совет Носка: Кеша молчит под полотенцем. Или за печеньку.", "Sock tip: Kesha stays quiet under a towel. Or for a cookie." },
            ["sockTip3"] = new[] { "Совет Носка: кинь клубок — кот забудет про тебя.", "Sock tip: throw a ball of yarn and the cat forgets about you." },
            ["sockTip4"] = new[] { "Совет Носка: без очков дед слеп как крот, без слухового аппарата — глух как пень.", "Sock tip: without glasses grandpa is blind as a mole, without the hearing aid deaf as a post." },
            ["mechOven"] = new[] { "духовка", "oven" },
            ["mechFreezer"] = new[] { "морозилка", "freezer" },
            ["mechFridge"] = new[] { "холодильник", "fridge" },
            ["mechWindow"] = new[] { "окно", "window" },
            ["mechTv"] = new[] { "телевизор", "TV" },
            ["mechFlush"] = new[] { "смыв", "flush" },
            ["mechTap"] = new[] { "кран", "tap" },
            ["mechSafe"] = new[] { "сейф", "safe" },
            ["mechSafeLocked"] = new[] { "сейф заперт — пинай замок (F)", "safe is locked — kick the lock (F)" },
            ["mechJar"] = new[] { "банка", "jar" },
            ["mechCage"] = new[] { "клетка", "cage" },

            // --- report ---
            ["report"] = new[] { "Утро. Дед проснулся и...", "Morning. Grandpa woke up and..." },
            ["verdictGood"] = new[] { "Великий Носок доволен. Шурх!", "The Great Sock is pleased. Swish!" },
            ["verdictBad"] = new[] { "Великий Носок недоволен! Выговор!", "The Great Sock is displeased! Strike!" },
            ["fired"] = new[] { "Банда распущена! Три выговора. Начинаем с первой ночи...", "The gang is disbanded! Three strikes. Back to night one..." },
            ["continue"] = new[] { "Продолжить", "Continue" },
            ["materials"] = new[] { "Материалы", "Materials" },
            ["chaosReport"] = new[] { "вещей не на своих местах", "things out of place" },
            ["grandpaMorning0"] = new[] { "...спокойно выпил чай. Скукота.", "...calmly drank his tea. Boring." },
            ["grandpaMorning1"] = new[] { "...полчаса искал очки. Неплохо!", "...spent half an hour looking for his glasses. Not bad!" },
            ["grandpaMorning2"] = new[] { "...орал на кота до обеда. Отлично!", "...yelled at the cat until lunch. Great!" },
            ["grandpaMorning3"] = new[] { "...вызвал экзорциста. ЛЕГЕНДАРНО!", "...called an exorcist. LEGENDARY!" },

            // materials
            ["mat0"] = new[] { "Пуговки", "Buttons" },
            ["mat1"] = new[] { "Винтики", "Bolts" },
            ["mat2"] = new[] { "Пряжа", "Yarn" },
            ["mat3"] = new[] { "Блёстки", "Glitter" },
            ["mat4"] = new[] { "Крошки", "Crumbs" },
            ["mat5"] = new[] { "Смешинки", "Giggles" },

            // controls help
            ["controls"] = new[] {
                "WASD — ходить, Shift — бежать (шумно), Ctrl — красться, Пробел — прыжок\nЛКМ — взять/положить, ПКМ — клубок-крюк (с вещью в руках — швырнуть), колесо — длина нитки, R — подтянуться\nT — привязать нитку, F — пинок, E — использовать/в карман, Q — выложить карманы, G — сонная пыльца, H — свист, V — камера, Tab — проказы",
                "WASD — move, Shift — run (noisy), Ctrl — sneak, Space — jump\nLMB — pick up/drop, RMB — yarn hook (holding an item: throw), wheel — yarn length, R — reel in\nT — tie the yarn, F — kick, E — use/pocket, Q — empty pockets, G — sleep dust, H — whistle, V — camera, Tab — pranks" },
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
