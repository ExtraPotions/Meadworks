// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the MeadWorks menu's hover tips, one plain sentence per product from its own manifest description.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// What each product does, shown when the pointer rests on its MeadWorks menu entry. Served with the kit's words
    /// (<see cref="KitText"/>, namespace <c>crucible_ui</c>) so translators override them like any kit text. An entry id
    /// maps to its product by the part before the first '.' or '-' (<c>cairn.hud</c> and <c>cairn</c> share Cairn's tip).
    /// English is authoritative; the other six languages are best-effort and await native-speaker review.
    /// </summary>
    public static class MeadworksMenuTips {
        const string Prefix="menu_tip_";
        static readonly Dictionary<string,string> Actions=new Dictionary<string,string>(StringComparer.Ordinal){
            ["@settings"]="View and change this mod's settings, with host-controlled values marked as read-only.",
            ["@back"]="Return to the list of MeadWorks mods.",
            ["@close"]="Close the MeadWorks menu and return to the game.",
            ["almanac"]="Browse settings by mod, search for an option, and inspect who can change it.",
            ["burrow-preview"]="Show or hide chest contents when you point at a chest.",
            ["cairn"]="Choose which map and HUD elements are shown and how they appear.",
            ["cairn.hud"]="Show or hide the Cairn HUD without changing its saved display settings.",
            ["hex"]="Open the map-based world editing tools permitted for your player.",
            ["inscribe"]="Open Inscribe's rarity, rune and display settings.",
            ["shepherd"]="Open the herd book to inspect animals and their available commands.",
            ["thing"]="Open available server, player and world management commands.",
            ["threshold-pins"]="Show or hide discovered portal pins on the map.",
            ["wildblood.recap"]="Review the recorded damage and events leading to your latest death.",
            ["wildblood.lore"]="Open the creature lore and discovery book.",
            ["wildroot"]="Enable or disable Wildroot's gardening changes when session rules allow it."
        };
        static string ActionKey(string id)=>"menu_action_"+id.TrimStart('@').Replace('.','_').Replace('-','_');

        // Columns follow Products.
        static readonly string[] Products={"almanac","burrow","cairn","hex","inscribe","mantle","shepherd","thing","threshold","wildblood","wildroot",
            "beacon","bestiary","crucible","delve","edge","ember","keel","kindle","larder","magpie","saga","tempest","timber","wayfarer"};

        internal static readonly Dictionary<string,string[]> Rows=new Dictionary<string,string[]>(StringComparer.Ordinal){
            ["English"]=new[]{
                "Opens one window for every installed mod's settings and shows which ones the server locks.",
                "Lets you craft and build from nearby chests, and adds chest previews, bigger chests, sorting and quick stack.",
                "Adds exploration: a reveal radius, fog controls, a time, weather and compass HUD, and tidy map pins.",
                "A cheat toolkit with personal powers, item and creature spawning, skills and map tools, under server-controlled permissions.",
                "Gives gear rarity, inscriptions, runes, gems and sockets.",
                "Adds a configurable inventory grid, gear, ring and belt slots, backpacks and a loadout swap.",
                "Adds animal husbandry: feed troughs, breeding with sires, star inheritance, twins, herd limits and lineage.",
                "Server administration for admins: moderation, messages, world tools and in-game server config editing.",
                "Lets you pick any portal from a list or the map, with named hubs, access modes and favorites.",
                "Makes wild creatures grow with the world: difficulty and stars, mutations, boss aspects and loot rules.",
                "Adds gardening: grid planting, snapping, bulk harvest, replanting and a Farming skill with perks.",
                "Saves servers and passwords securely, with favorites, automatic joining and reconnecting.",
                "Adds creatures, including mimics, giants, the Kraken and skeletons with new weapons.",
                "Provides the shared MeadWorks menu, interface, language support and server permissions.",
                "Controls dungeon sizes and safely replenishes chests, resources and creatures on timers.",
                "Adds weapon secondary attacks, quickstep, slope-aware swings and equipment recovery while swimming.",
                "Adds faster nights when players sleep, warming torches and doors that open as you approach.",
                "Adds ship settings, new ships and a Sailing skill with Wind Call and Lookout.",
                "Adds recipe search, sorting, favorites, batch crafting, recipe tracking, salvage and a Cart Workbench.",
                "Controls food, mead, eating, regeneration and stamina costs, with kitchen stations and Cooking perks.",
                "Adds magnet pickup, ground-stack merging, floating drops and a server-controlled loot multiplier.",
                "Turns boss powers into permanent passives and adds skill perks with a shared skill book.",
                "Adds rare severe storms with advance warnings and a server weather filter.",
                "Adds a build camera, flooring options and area repair, with server-controlled building cheats.",
                "Adds return-to-bed travel and server-controlled travel to stones, boats, graves, players or map locations."},
            ["Korean"]=new[]{
                "설치된 모든 모드의 설정을 한 창에서 열고, 서버가 잠근 설정을 보여 줍니다.",
                "근처 상자의 재료로 제작과 건축을 하고, 상자 미리보기, 더 큰 상자, 정렬, 빠른 보관을 추가합니다.",
                "탐험 기능을 추가합니다: 탐색 반경, 안개 설정, 시간·날씨·나침반 HUD, 깔끔한 지도 핀.",
                "개인 능력, 아이템·생물 소환, 스킬, 지도 도구를 담은 치트 도구로, 서버가 권한을 관리합니다.",
                "주문서로 장비에 희귀도, 룬, +1부터 +20까지의 강화 단계를 부여합니다.",
                "인벤토리 칸 수 설정, 장비·반지·벨트 칸, 배낭, 장비 세트 교체를 추가합니다.",
                "축산을 추가합니다: 먹이통, 종축 번식, 별 유전, 쌍둥이, 무리 제한, 혈통.",
                "관리자용 서버 관리: 제재, 메시지, 월드 도구, 게임 안에서 서버 설정 편집.",
                "목록이나 지도에서 원하는 포털을 고르고, 이름 붙은 허브, 접근 방식, 즐겨찾기를 씁니다.",
                "야생 생물이 월드와 함께 강해집니다: 난이도와 별, 변이, 보스 특성, 전리품 규칙.",
                "원예를 추가합니다: 격자 심기, 맞춤 배치, 일괄 수확, 다시 심기, 특성이 있는 농사 스킬."},
            ["Spanish"]=new[]{
                "Abre una sola ventana con los ajustes de todos los mods instalados y muestra cuáles bloquea el servidor.",
                "Permite fabricar y construir con los cofres cercanos y añade vista previa de cofres, cofres más grandes, orden y guardado rápido.",
                "Añade exploración: radio de descubrimiento, control de la niebla, HUD de hora, clima y brújula, y marcadores de mapa ordenados.",
                "Herramientas de trucos con poderes personales, generación de objetos y criaturas, habilidades y mapa, con permisos que controla el servidor.",
                "Da al equipo rareza, inscripciones, runas, gemas y engarces.",
                "Añade una cuadrícula de inventario configurable, ranuras de equipo, anillos y cinturón, mochilas y cambio de equipamiento.",
                "Añade ganadería: comederos, cría con sementales, herencia de estrellas, gemelos, límites de rebaño y linaje.",
                "Administración del servidor para administradores: moderación, mensajes, herramientas del mundo y edición de la configuración en el juego.",
                "Elige cualquier portal desde una lista o el mapa, con centros con nombre, modos de acceso y favoritos.",
                "Las criaturas salvajes crecen con el mundo: dificultad y estrellas, mutaciones, aspectos de jefes y reglas de botín.",
                "Añade jardinería: plantar en cuadrícula, ajuste, cosecha en masa, replantado y una habilidad de Agricultura con ventajas."},
            ["Chinese"]=new[]{
                "在一个窗口中打开所有已安装模组的设置，并显示服务器锁定了哪些设置。",
                "可以使用附近箱子里的材料制作和建造，并加入箱子预览、更大的箱子、整理和快速存放。",
                "加入探索功能：探索半径、迷雾控制、时间/天气/指南针显示，以及整洁的地图标记。",
                "作弊工具：个人能力、生成物品和生物、技能和地图工具，权限由服务器控制。",
                "用卷轴为装备赋予稀有度、符文和 +1 到 +20 的强化等级。",
                "加入可调整的背包格子、装备/戒指/腰带栏位、背包和一键换装。",
                "加入畜牧：饲料槽、种畜繁殖、星级遗传、双胞胎、畜群上限和血统。",
                "供管理员使用的服务器管理：管理玩家、消息、世界工具和游戏内服务器配置编辑。",
                "从列表或地图中选择任意传送门，支持命名枢纽、访问模式和收藏。",
                "野生生物随世界成长：难度和星级、变异、首领特性和掉落规则。",
                "加入园艺：网格种植、对齐、批量收获、自动补种，以及带天赋的农耕技能。"},
            ["Russian"]=new[]{
                "Открывает одно окно с настройками всех установленных модов и показывает, какие из них блокирует сервер.",
                "Позволяет создавать и строить из соседних сундуков и добавляет просмотр сундуков, большие сундуки, сортировку и быстрое складывание.",
                "Добавляет исследование: радиус открытия карты, настройки тумана, панель времени, погоды и компаса и аккуратные метки.",
                "Набор читов: личные способности, создание предметов и существ, навыки и инструменты карты с правами, которые задаёт сервер.",
                "Даёт снаряжению редкость, руны и уровни усиления от +1 до +20 с помощью свитков.",
                "Добавляет настраиваемую сетку инвентаря, ячейки снаряжения, колец и пояса, рюкзаки и смену комплекта.",
                "Добавляет животноводство: кормушки, разведение с производителями, наследование звёзд, двойни, лимиты стада и родословную.",
                "Администрирование сервера для админов: модерация, сообщения, инструменты мира и правка настроек сервера в игре.",
                "Выбор любого портала из списка или на карте, с именованными узлами, режимами доступа и избранным.",
                "Дикие существа растут вместе с миром: сложность и звёзды, мутации, черты боссов и правила добычи.",
                "Добавляет садоводство: посадку по сетке, привязку, массовый сбор, пересадку и навык Земледелия с перками."},
            ["French"]=new[]{
                "Ouvre une seule fenêtre pour les réglages de tous les mods installés et montre ceux que le serveur verrouille.",
                "Permet de fabriquer et construire depuis les coffres proches, et ajoute l'aperçu des coffres, des coffres plus grands, le tri et le rangement rapide.",
                "Ajoute l'exploration : rayon de découverte, réglages du brouillard, affichage de l'heure, de la météo et de la boussole, et des repères de carte bien rangés.",
                "Une boîte à outils de triche : pouvoirs personnels, apparition d'objets et de créatures, compétences et outils de carte, avec des droits contrôlés par le serveur.",
                "Donne à l'équipement une rareté, des inscriptions, des runes, des gemmes et des châsses.",
                "Ajoute une grille d'inventaire réglable, des emplacements d'équipement, d'anneaux et de ceinture, des sacs à dos et un échange d'équipement.",
                "Ajoute l'élevage : mangeoires, reproduction avec des géniteurs, héritage des étoiles, jumeaux, limites de troupeau et lignée.",
                "Administration du serveur pour les admins : modération, messages, outils du monde et édition de la configuration du serveur en jeu.",
                "Choisissez n'importe quel portail dans une liste ou sur la carte, avec des carrefours nommés, des modes d'accès et des favoris.",
                "Les créatures sauvages grandissent avec le monde : difficulté et étoiles, mutations, aspects de boss et règles de butin.",
                "Ajoute le jardinage : plantation en grille, alignement, récolte groupée, replantation et une compétence d'Agriculture avec des bonus."},
            ["German"]=new[]{
                "Öffnet ein einziges Fenster für die Einstellungen aller installierten Mods und zeigt, welche der Server sperrt.",
                "Lässt dich aus nahen Truhen herstellen und bauen und bringt Truhenvorschau, größere Truhen, Sortieren und schnelles Einlagern.",
                "Bringt Erkundung: Aufdeckradius, Nebeleinstellungen, eine Anzeige für Zeit, Wetter und Kompass sowie aufgeräumte Kartenmarker.",
                "Ein Cheat-Werkzeugkasten mit persönlichen Kräften, Gegenstands- und Kreatur-Spawns, Fertigkeiten und Kartenwerkzeugen, mit vom Server gesteuerten Rechten.",
                "Gibt Ausrüstung Seltenheit, Runen und Verbesserungsstufen von +1 bis +20 mit Schriftrollen.",
                "Bringt ein einstellbares Inventarraster, Plätze für Ausrüstung, Ringe und Gürtel, Rucksäcke und einen Ausrüstungswechsel.",
                "Bringt Tierhaltung: Futtertröge, Zucht mit Vatertieren, Sternvererbung, Zwillinge, Herdengrenzen und Abstammung.",
                "Serververwaltung für Admins: Moderation, Nachrichten, Weltwerkzeuge und Bearbeitung der Serverkonfiguration im Spiel.",
                "Wähle jedes Portal aus einer Liste oder auf der Karte, mit benannten Knotenpunkten, Zugriffsarten und Favoriten.",
                "Wilde Kreaturen wachsen mit der Welt: Schwierigkeit und Sterne, Mutationen, Boss-Aspekte und Beuteregeln.",
                "Bringt Gartenbau: Pflanzen im Raster, Einrasten, Massenernte, Nachpflanzen und eine Landwirtschafts-Fertigkeit mit Talenten."},
        };

        /// <summary>The translation keys, in column order.</summary>
        public static IReadOnlyList<string> Keys { get; }=Products.Select(product=>Prefix+product).Concat(Actions.Keys.Select(ActionKey)).ToArray();

        /// <summary>The kit text key of the entry's product tip, or null when the product has none.</summary>
        public static string? Key(string? id){
            if(string.IsNullOrEmpty(id))return null;
            int cut=id!.IndexOfAny(new[]{'.','-'});
            string product=(cut>0?id.Substring(0,cut):id).ToLowerInvariant();
            return Array.IndexOf(Products,product)>=0?Prefix+product:null;
        }

        /// <summary>The tip for this entry in <paramref name="language"/>, or empty when its product has none.</summary>
        public static string Text(string? id,string? language,ProductTranslations? translations=null){
            var key=Key(id);
            return key==null?"":KitText.Text(key,language,translations);
        }

        public static string ActionText(string? id,string? language,ProductTranslations? translations=null)=>
            id!=null&&Actions.ContainsKey(id)?KitText.Text(ActionKey(id),language,translations):Text(id,language,translations);

        internal static void AddTo(string language,Dictionary<string,string> table){
            if(!Rows.TryGetValue(language,out var row))return;
            // New descriptions use the existing English fallback until translations are available.
            for(int i=0;i<Products.Length;i++)table.Add(Prefix+Products[i],i<row.Length?row[i]:Rows["English"][i]);
            foreach(var action in Actions)table.Add(ActionKey(action.Key),action.Value);
        }
    }
}
