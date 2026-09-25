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
            ["title"] = new[] { "THE SOCK GANG", "THE SOCK GANG" },
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
            ["climbHint"] = new[] { "W — лезть вверх по нитке, S — вниз, Пробел — спрыгнуть", "W — climb up the yarn, S — down, Space — jump off" },

            // --- the handmade interface ---
            ["yourGnome"] = new[] { "Твой гном", "Your gnome" },
            ["pokeHint"] = new[] { "(его можно потыкать)", "(you may poke him)" },
            ["randomName"] = new[] { "Случайное имя", "Random name" },
            ["legendName"] = new[] { "ЛЕГЕНДАРНОЕ ИМЯ! Выпадает один раз из сорока.", "A LEGENDARY NAME! One roll in forty." },
            ["gangTitle"] = new[] { "Своя банда", "Your own gang" },
            ["gangInfo"] = new[] { "Ты хозяин лаза. Друзья подключаются по твоему IP-адресу (нажми, чтобы скопировать):", "You host the tunnel. Friends connect to your IP address (click to copy):" },
            ["copied"] = new[] { "Скопировано! Отправь другу.", "Copied! Send it to a friend." },
            ["openTunnel"] = new[] { "Открыть лаз", "Open the tunnel" },
            ["joinTitle"] = new[] { "К друзьям", "Join friends" },
            ["joinBy"] = new[] { "Или по адресу:", "Or by address:" },
            ["gangOf"] = new[] { "Банда", "Gang of" },
            ["gnomeWord1"] = new[] { "гном", "gnome" },
            ["gnomeWord2"] = new[] { "гнома", "gnomes" },
            ["gnomeWord5"] = new[] { "гномов", "gnomes" },
            ["theGang"] = new[] { "Банда", "The gang" },
            ["nightTime"] = new[] { "Ночь", "Night" },
            ["credits"] = new[] { "Об игре", "About" },
            ["creditsText"] = new[] {
                "«The Sock Gang» — игра про гномов из-под крыльца.\n\nМодели связаны из треугольников вручную (ну, скриптами). Шрифты: Balsamiq Sans, Lobster и Rubik Mono One (SIL OFL). Движок: Godot.\n\nНи один носок не пострадал. Ну, почти.\nДед, если ты это читаешь — очки в аквариуме.",
                "\"The Sock Gang\" is a game about the gnomes under the porch.\n\nThe models were knitted from triangles by hand (well, by scripts). Fonts: Balsamiq Sans, Lobster and Rubik Mono One (SIL OFL). Engine: Godot.\n\nNo socks were harmed. Well, almost.\nGrandpa, if you are reading this: your glasses are in the fish tank." },
            ["stampGood"] = new[] { "ОДОБРЕНО НОСКОМ", "SOCK APPROVED" },
            ["stampBad"] = new[] { "ВЫГОВОР", "REPRIMAND" },
            ["stampLegend"] = new[] { "ЛЕГЕНДА!", "LEGEND!" },
            ["gnomeWisdom"] = new[] { "Гномья мудрость", "Gnome wisdom" },
            ["tipTitle"] = new[] { "Совет бывалого", "Old hand's tip" },
            ["woolTag"] = new[] { "100% шерсть", "100% wool" },
            ["paused"] = new[] { "Пауза", "Paused" },

            // --- easter eggs ---
            ["eggKonami"] = new[] { "↑↑↓↓←→←→BA — связан радужный колпак! Он теперь в выборе цвета.", "↑↑↓↓←→←→BA — a rainbow hat is knitted! Pick it among the colours." },
            ["eggGrandpa"] = new[] { "Дед?! Как ты сюда попал? ...А, это имя. Смело.", "Grandpa?! How did you get in here? ...Oh, it's a name. Brave." },
            ["eggSock"] = new[] { "Гном по имени Носок. Великий Носок одобряет выбор.", "A gnome named Sock. The Great Sock approves." },
            ["eggGandalf"] = new[] { "Ты не пройдёшь! ...мимо деда незамеченным. Шучу, пройдёшь.", "You shall not pass! ...grandpa unnoticed. Just kidding, you shall." },
            ["eggKesha"] = new[] { "Кеша хороший! Кеша ВОРОВ не выдаст! (выдаст)", "Kesha is a good boy! Kesha won't tell on THIEVES! (he will)" },
            ["eggBarsik"] = new[] { "Мяу. Кот тоже хочет в банду, но у него лапки.", "Meow. The cat wants to join the gang too, but he has paws." },
            ["eggGnome"] = new[] { "Гном по имени Гном. Оригинально!", "A gnome named Gnome. How original!" },
            ["eggClaude"] = new[] { "Привет от того, кто вязал этих гномов ночами напролёт!", "Greetings from the one who knitted these gnomes all night long!" },
            ["eggPoke5"] = new[] { "Гном: «Щекотно!»", "Gnome: \"That tickles!\"" },
            ["eggPoke10"] = new[] { "Гном: «Апчхи! У меня аллергия на курсор!»", "Gnome: \"Achoo! I'm allergic to cursors!\"" },
            ["eggPoke20"] = new[] { "Гном: «Всё, я пошёл к Великому Носку жаловаться.»", "Gnome: \"That's it, I'm telling the Great Sock.\"" },
            ["eggLogo3"] = new[] { "Не тяни за нитку, распустишь!", "Don't pull the yarn, it'll unravel!" },
            ["eggLogo7"] = new[] { "Буквы связаны вручную. Семь раз отмерь — один раз распусти.", "The letters are hand-knitted. Measure seven times, unravel once." },
            ["eggLogo12"] = new[] { "Всё, петля убежала. Теперь это «THE SOCK GAN».", "That's it, a stitch ran away. Now it's \"THE SOCK GAN\"." },
            ["eggWinter"] = new[] { "С Новым годом, банда! Дед повесил носок для подарков. Наш размер!", "Happy holidays, gang! Grandpa hung a stocking for presents. Our size!" },
            ["eggApril"] = new[] { "Первое апреля: дед сегодня особенно подозрительный.", "April Fools' Day: grandpa is extra suspicious today." },
            ["sockSecret0"] = new[] { "Великий Носок шепчет: «Я был левым. Правого унесла стиральная машина. Я помню.»", "The Great Sock whispers: \"I was the left one. The washing machine took the right one. I remember.\"" },
            ["sockSecret1"] = new[] { "Великий Носок: «Дед до сих пор думает, что пульт у него съедает диван.»", "The Great Sock: \"Grandpa still believes the sofa eats his remote.\"" },
            ["sockSecret2"] = new[] { "Великий Носок: «Легенда гласит: где-то в доме есть носок без пары, который светится.»", "The Great Sock: \"Legend says somewhere in the house lies a single sock that glows.\"" },
            ["sockSecret3"] = new[] { "Великий Носок: «Свистеть в доме — к шуму. Свистеть в деревне — к секретам. Ты понял.»", "The Great Sock: \"Whistle in the house for trouble. Whistle in the village for secrets. You get it.\"" },
            ["sockSecret4"] = new[] { "Великий Носок: «Кот не злой. Кот просто не выспался. Уже девять лет.»", "The Great Sock: \"The cat isn't evil. He just didn't sleep well. For nine years.\"" },
            ["tip0"] = new[] { "Деды не видят гномов, которые не видят дедов. Наверное.", "Grandpas can't see gnomes who can't see grandpas. Probably." },
            ["tip1"] = new[] { "Если дед нашёл тебя — сделай вид, что ты садовый гном. Не поможет, но попробуй.", "If grandpa spots you, pretend to be a garden gnome. It won't work, but try." },
            ["tip2"] = new[] { "Нитку можно привязать к тапкам деда (T). Утро будет весёлым.", "You can tie the yarn to grandpa's slippers (T). The morning will be fun." },
            ["tip3"] = new[] { "Кеша сдаёт всех. Полотенце на клетку — лучший друг гнома.", "Kesha tells on everyone. A towel over the cage is a gnome's best friend." },
            ["tip4"] = new[] { "По нитке можно залезть на стол: зацепись и держи W.", "You can climb onto a table on the yarn: hook the edge and hold W." },
            ["tip5"] = new[] { "Под кроватью дед тебя не достанет. Зато там живут пыльные зайцы.", "Grandpa can't reach under the bed. Dust bunnies can, though." },
            ["tip6"] = new[] { "Свет выключен — дед видит вдвое хуже. Пни торшер (F).", "Lights off: grandpa sees half as well. Kick the lamp (F)." },
            ["tip7"] = new[] { "Одинокие носки — самая ценная добыча. Второй всё равно не найдётся.", "Single socks are the best loot. The other one is never coming back anyway." },
            ["tip8"] = new[] { "Банка из-под огурцов — не приговор. Раскачивай её (F) или жди друга.", "A pickle jar isn't the end. Rock it (F) or wait for a friend." },
            ["tip9"] = new[] { "В деревне посвисти (H) рядом с Великим Носком. Три раза.", "In the village, whistle (H) next to the Great Sock. Three times." },
            ["tip10"] = new[] { "Говорят, в меню работает старый чит-код. ↑↑↓↓... дальше не помню.", "Rumour has it an old cheat code works in the menu. ↑↑↓↓... I forget the rest." },
            ["tip11"] = new[] { "Кот спит 16 часов в сутки. Оставшиеся 8 он охотится на тебя.", "The cat sleeps 16 hours a day. The other 8 he hunts you." },
            ["wisdom0"] = new[] { "Тише едешь — дольше не пойман.", "The slower you go, the longer you stay free." },
            ["wisdom1"] = new[] { "Не тот гном, кто носок унёс, а тот, кто его донёс.", "It's not about taking the sock, it's about getting it home." },
            ["wisdom2"] = new[] { "Семь раз отмерь нитку, один раз прыгай.", "Measure the yarn seven times, jump once." },
            ["wisdom3"] = new[] { "Дед спит — проказа идёт.", "While grandpa sleeps, the pranks go on." },
            ["wisdom4"] = new[] { "Один носок — случайность, три носка — традиция.", "One sock is an accident, three socks are a tradition." },
            ["wisdom5"] = new[] { "Не буди деда, пока он тихо храпит.", "Let sleeping grandpas snore." },
            ["wisdom6"] = new[] { "В банке тесно, зато видно всю кухню.", "It's cramped in the jar, but what a view of the kitchen." },
            ["wisdom7"] = new[] { "Колпак — это не шапка. Колпак — это образ жизни.", "A hat isn't headwear. A hat is a way of life." },
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
            ["portalHint"] = new[] { "Залезь в носочный лаз (он за спиной), чтобы начать ночь. Великий Носок — впереди, верстак-вязальня — справа.", "Crawl into the sock tunnel (behind you) to start the night. The Great Sock is ahead, the knitting corner on the right." },
            ["craftHostOnly"] = new[] { "Прогресс общий: вяжет хост банды.", "Progress is shared: the host's gang does the knitting." },
            ["waitHost"] = new[] { "Ждём, когда хост продолжит...", "Waiting for the host to continue..." },
            ["yarn"] = new[] { "Нитка", "Yarn" },
            ["steps"] = new[] { "Шаги", "Steps" },
            ["stepSilent"] = new[] { "не слышно", "silent" },
            ["stepQuiet"] = new[] { "тихо", "quiet" },
            ["stepNormal"] = new[] { "слышно", "audible" },
            ["stepLoud"] = new[] { "ГРОМКО", "LOUD" },
            ["inShadow"] = new[] { "в тени", "in shadow" },
            ["inLight"] = new[] { "на свету", "in the light" },
            ["loading"] = new[] { "Загрузка…", "Loading…" },
            ["fullscreen"] = new[] { "Полный экран", "Fullscreen" },
            ["qLow"] = new[] { "Низкое", "Low" },
            ["qMedium"] = new[] { "Среднее", "Medium" },
            ["qHigh"] = new[] { "Высокое", "High" },
            ["chat"] = new[] { "Сообщение… (Enter — отправить)", "Message… (Enter to send)" },
            ["sockTip0"] = new[] { "Совет Носка: под кроватью дед тебя не достанет.", "Sock tip: grandpa can't reach you under the bed." },
            ["sockTip1"] = new[] { "Совет Носка: крадись (Ctrl) по скрипучим половицам — не будут скрипеть.", "Sock tip: sneak (Ctrl) over creaky boards and they won't creak." },
            ["sockTip2"] = new[] { "Совет Носка: Кеша молчит под полотенцем. Или за печеньку.", "Sock tip: Kesha stays quiet under a towel. Or for a cookie." },
            ["sockTip3"] = new[] { "Совет Носка: кинь клубок — кот забудет про тебя.", "Sock tip: throw a ball of yarn and the cat forgets about you." },
            ["sockTip4"] = new[] { "Совет Носка: без очков дед слеп как крот, без слухового аппарата — глух как пень.", "Sock tip: without glasses grandpa is blind as a mole, without the hearing aid deaf as a post." },
            ["sockTip5"] = new[] { "Совет Носка: пни торшер (F) — в темноте дед видит вдвое хуже. Только он пойдёт включать свет обратно.", "Sock tip: kick a lamp (F): grandpa sees half as well in the dark. He will come to switch it back on, though." },
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
                "WASD — ходить, Shift — бежать (шумно), Ctrl — красться, Пробел — прыжок\nЛКМ — взять/положить, ПКМ — клубок-крюк (с вещью в руках — швырнуть), на нитке: W/S — лезть вверх/вниз\nT — привязать нитку, F — пинок, E — использовать/в карман, Q — выложить карманы, G — сонная пыльца, H — свист, V — камера, Tab — проказы",
                "WASD — move, Shift — run (noisy), Ctrl — sneak, Space — jump\nLMB — pick up/drop, RMB — yarn hook (holding an item: throw), on the yarn: W/S — climb up/down\nT — tie the yarn, F — kick, E — use/pocket, Q — empty pockets, G — sleep dust, H — whistle, V — camera, Tab — pranks" },
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

        /// <summary>Russian plural form of a count: one (1, 21), few (2-4, 22-24) or many (0, 5-20, 25...).</summary>
        public static string RuPlural(int n, string one, string few, string many)
        {
            n = System.Math.Abs(n) % 100;
            if (n >= 11 && n <= 14) return many;
            int d = n % 10;
            if (d == 1) return one;
            if (d >= 2 && d <= 4) return few;
            return many;
        }

        /// <summary>"N things out of place" in the current language.</summary>
        public static string ChaosCount(int n) => Current == Lang.Ru
            ? n + " " + RuPlural(n, "вещь", "вещи", "вещей") + " не на своих местах"
            : n + (n == 1 ? " thing" : " things") + " out of place";
    }
}
