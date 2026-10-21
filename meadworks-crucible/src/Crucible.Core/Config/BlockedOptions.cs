using System;
using System.Collections.Generic;
using System.Linq;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// Localized "why is this blocked" texts for every <see cref="ConfigBlockReason"/> (roster: blocked options tell the
    /// player why). English is authoritative and is also what <see cref="AuthorityGate.Describe"/> returns; the other six
    /// languages are best-effort translations awaiting native review. Served through the C05 service under the
    /// <c>crucible</c> namespace, so translators override them like any product text.
    /// </summary>
    public static class BlockedOptionText {
        public const string Product="crucible";
        /// <summary>Player message format: {0} option label, {1} reason text.</summary>
        public const string OptionKey="blocked_option";

        static readonly ConfigBlockReason[] Order={ConfigBlockReason.AuthorityPending,ConfigBlockReason.NotAdmin,ConfigBlockReason.ServerLocked,ConfigBlockReason.VanillaServer,ConfigBlockReason.ProductDisabled,ConfigBlockReason.ServerOnly,ConfigBlockReason.MischiefDenied};
        static readonly string[] Keys={"blocked_authority_pending","blocked_not_admin","blocked_server_locked","blocked_vanilla_server","blocked_product_disabled","blocked_server_only","blocked_mischief_denied"};

        // Columns follow Order, then the option format. English is authoritative; others need native review (task doc C03, C13).
        static readonly Dictionary<string,string[]> Rows=new Dictionary<string,string[]>(StringComparer.Ordinal){
            ["English"]=new[]{
                "Waiting for the server to confirm Meadworks settings.",
                "Only a server admin can change this.",
                "The server controls this setting.",
                "This server does not run Meadworks, so this is locked.",
                "This server does not run this product, so it is turned off for this session.",
                "This product runs only on the server.",
                "The server's cheat policy does not allow you to use this cheat.",
                "{0}: {1}"},
            ["Korean"]=new[]{
                "서버가 Meadworks 설정을 확인하기를 기다리는 중입니다.",
                "서버 관리자만 이것을 변경할 수 있습니다.",
                "이 설정은 서버가 관리합니다.",
                "이 서버는 Meadworks를 실행하지 않으므로 잠겨 있습니다.",
                "이 서버는 이 제품을 실행하지 않으므로 이번 세션 동안 꺼집니다.",
                "이 제품은 서버에서만 실행됩니다.",
                "서버의 치트 정책상 이 치트를 사용할 수 없습니다.",
                "{0}: {1}"},
            ["Spanish"]=new[]{
                "Esperando a que el servidor confirme los ajustes de Meadworks.",
                "Solo un administrador del servidor puede cambiar esto.",
                "El servidor controla este ajuste.",
                "Este servidor no ejecuta Meadworks, así que esto está bloqueado.",
                "Este servidor no ejecuta este producto, así que está desactivado durante esta sesión.",
                "Este producto solo se ejecuta en el servidor.",
                "La política de trucos del servidor no te permite usar este truco.",
                "{0}: {1}"},
            ["Chinese"]=new[]{
                "正在等待服务器确认 Meadworks 设置。",
                "只有服务器管理员可以更改此项。",
                "此设置由服务器控制。",
                "此服务器未运行 Meadworks，因此此项已锁定。",
                "此服务器未运行该产品，因此本次会话中已关闭。",
                "该产品仅在服务器上运行。",
                "服务器的作弊策略不允许你使用此作弊。",
                "{0}：{1}"},
            ["Russian"]=new[]{
                "Ожидание подтверждения настроек Meadworks сервером.",
                "Изменить это может только администратор сервера.",
                "Этот параметр контролирует сервер.",
                "На этом сервере нет Meadworks, поэтому это заблокировано.",
                "На этом сервере нет этого продукта, поэтому он отключён на этот сеанс.",
                "Этот продукт работает только на сервере.",
                "Политика читов на сервере не разрешает вам использовать этот чит.",
                "{0}: {1}"},
            ["French"]=new[]{
                "En attente de la confirmation des paramètres Meadworks par le serveur.",
                "Seul un administrateur du serveur peut modifier ceci.",
                "Le serveur contrôle ce paramètre.",
                "Ce serveur n'utilise pas Meadworks, c'est donc verrouillé.",
                "Ce serveur n'utilise pas ce produit, il est donc désactivé pour cette session.",
                "Ce produit ne fonctionne que sur le serveur.",
                "La politique de triche du serveur ne vous permet pas d'utiliser cette triche.",
                "{0} : {1}"},
            ["German"]=new[]{
                "Warte darauf, dass der Server die Meadworks-Einstellungen bestätigt.",
                "Nur ein Serveradmin kann das ändern.",
                "Der Server legt diese Einstellung fest.",
                "Dieser Server nutzt Meadworks nicht, daher ist das gesperrt.",
                "Dieser Server nutzt dieses Produkt nicht, daher ist es für diese Sitzung ausgeschaltet.",
                "Dieses Produkt läuft nur auf dem Server.",
                "Die Cheat-Richtlinie des Servers erlaubt dir diesen Cheat nicht.",
                "{0}: {1}"},
        };

        // Setting names, help and headings of Crucible's own settings (SettingTextKeys); English equals the bound text.
        static readonly Dictionary<string,Dictionary<string,string>> SettingRows=new Dictionary<string,Dictionary<string,string>>(StringComparer.Ordinal){
            ["English"]=new Dictionary<string,string>(StringComparer.Ordinal){
                ["cfg_achievements_heading"]="Achievements",
                ["cfg_achievements__syncpaststats_name"]="SyncPastStats",
                ["cfg_achievements__syncpaststats_help"]="Copy stats the game counted while achievements were not tracked into the achievement table when a clean character loads (never for characters that used cheats unless Hex allows it). Off while AchievementSync is installed.",
                ["cfg_achievements__resynckey_name"]="ResyncKey",
                ["cfg_achievements__resynckey_help"]="Key that rechecks your saved game statistics and grants any MeadWorks achievements already earned. Unassigned by default.",
                ["cfg_authority_heading"]="Authority",
                ["cfg_authority__vanillaservertimeoutseconds_name"]="VanillaServerTimeoutSeconds",
                ["cfg_authority__vanillaservertimeoutseconds_help"]="Seconds a client waits for the server's Crucible Hello before treating the server as vanilla (cheats locked, required products off).",
                ["cfg_network_heading"]="Network",
                ["cfg_network__peerclasshandshake_name"]="PeerClassHandshake",
                ["cfg_network__peerclasshandshake_help"]="Checks MeadWorks versions when a player joins: the server turns away clients missing a required MeadWorks mod or running a different major.minor version, and a client turns off required mods the server lacks. On by default; turn it off only to diagnose join problems. Restart the game after changing this setting.",
                ["cfg_logging_heading"]="Logging",
                ["cfg_logging__level_name"]="Level",
                ["cfg_logging__level_help"]="Meadworks log level: Quiet (errors and warnings), Normal, Verbose or Debug. Also: console 'meadworks log <level> [product]'.",
                ["cfg_logging__productoverrides_name"]="ProductOverrides",
                ["cfg_logging__productoverrides_help"]="Per-product levels, for example 'Wildroot=Debug,Burrow=Normal'.",
                ["cfg_logging__repeatwindowseconds_name"]="RepeatWindowSeconds",
                ["cfg_logging__repeatwindowseconds_help"]="Identical lines inside this window are written once and counted; 0 writes every line.",
                ["cfg_logging__consolefilterenabled_name"]="ConsoleFilterEnabled",
                ["cfg_logging__consolefilterenabled_help"]="Filter the BepInEx console for all installed mods by the rules below.",
                ["cfg_logging__consolefilterrules_name"]="ConsoleFilterRules",
                ["cfg_logging__consolefilterrules_help"]="Per-source console levels: 'Source=All|Info|Warning|Error|None', separated by ';'. '*' sets the level for unlisted sources.",
                ["cfg_logging__consolefilterappliestodisklog_name"]="ConsoleFilterAppliesToDiskLog",
                ["cfg_logging__consolefilterappliestodisklog_help"]="Also apply the console filter rules to BepInEx's LogOutput.log.",
                ["cfg_logging__showallconsolemessages_name"]="ShowAllConsoleMessages",
                ["cfg_logging__showallconsolemessages_help"]="Show every message on the BepInEx console. By default Crucible hides a short, exact list of known-harmless vanilla and Unity messages (world-generation hints, missing audio or video shaders on a dedicated server and similar); LogOutput.log always keeps everything.",
                ["cfg_mischief_heading"]="Mischief",
                ["cfg_mischief__listedplayers_name"]="ListedPlayers",
                ["cfg_mischief__listedplayers_help"]="Platform user IDs granted Listed cheat categories, separated by commas (for example Steam_76561198000000000; a bare number means a Steam ID, as in adminlist.txt).",
                ["cfg_skills_heading"]="Skills",
                ["cfg_skills__experiencemultiplier_name"]="ExperienceMultiplier",
                ["cfg_skills__experiencemultiplier_help"]="Multiplies experience in every Meadworks custom skill (products' own skills, not the game's). The server's value applies in multiplayer.",
                ["cfg_network__admissionmode_name"]="AdmissionMode",
                ["cfg_network__admissionmode_help"]="Strict: every client must run Meadworks. AllowVanilla: clients without Meadworks (vanilla/console) may join and receive server-side features; it takes effect only when every installed product declares vanilla support and no installed mod's Jotunn metadata requires the mod on clients, otherwise the server logs why and stays Strict. Needs Network.PeerClassHandshake on the server. Saves and items are never changed.",
                ["cfg_mischief__personal_name"]="Personal",
                ["cfg_mischief__personal_help"]="Who may use Personal cheats on this server: Nobody, Admins, Listed (admins and Listed Players) or Everyone. Single player and the host always may. Personal: affects only the user (god mode, speed, size).",
                ["cfg_mischief__economy_name"]="Economy",
                ["cfg_mischief__economy_help"]="Who may use Economy cheats on this server: Nobody, Admins, Listed (admins and Listed Players) or Everyone. Single player and the host always may. Economy: creates or saves materials (spawning, craft speed, loot multipliers, free build).",
                ["cfg_mischief__world_name"]="World",
                ["cfg_mischief__world_help"]="Who may use World cheats on this server: Nobody, Admins, Listed (admins and Listed Players) or Everyone. Single player and the host always may. World: changes the shared world (weather, unrestricted growth).",
                ["cfg_mischief__information_name"]="Information",
                ["cfg_mischief__information_help"]="Who may use Information cheats on this server: Nobody, Admins, Listed (admins and Listed Players) or Everyone. Single player and the host always may. Information: reveals what is hidden (ESP, map reveal, points of interest).",
                ["cfg_mischief__travel_name"]="Travel",
                ["cfg_mischief__travel_help"]="Who may use Travel cheats on this server: Nobody, Admins, Listed (admins and Listed Players) or Everyone. Single player and the host always may. Travel: moves players (teleport, portals).",
                ["cfg_mischief__progress_name"]="Progress",
                ["cfg_mischief__progress_help"]="Who may use Progress cheats on this server: Nobody, Admins, Listed (admins and Listed Players) or Everyone. Single player and the host always may. Progress: skills, boss powers, achievements, guaranteed upgrades.",
            },
            // Language freeze: Spanish and French are complete; the other languages fall back to English. Enum values,
            // console commands, file names and Section.Key literals stay as written.
            ["Spanish"]=new Dictionary<string,string>(StringComparer.Ordinal){
                ["cfg_achievements_heading"]="Logros",
                ["cfg_achievements__syncpaststats_name"]="Sincronizar estadísticas pasadas",
                ["cfg_achievements__syncpaststats_help"]="Copia a la tabla de logros las estadísticas que el juego contó mientras no se registraban logros, al cargar un personaje limpio (nunca en personajes que usaron trucos, salvo que Hex lo permita). Desactivado si AchievementSync está instalado.",
                ["cfg_achievements__resynckey_name"]="Tecla de resincronización",
                ["cfg_achievements__resynckey_help"]="Tecla que vuelve a revisar las estadísticas de tu partida guardada y concede los logros de MeadWorks ya conseguidos. Sin asignar por defecto.",
                ["cfg_authority_heading"]="Autoridad",
                ["cfg_authority__vanillaservertimeoutseconds_name"]="Espera de servidor vanilla (segundos)",
                ["cfg_authority__vanillaservertimeoutseconds_help"]="Segundos que un cliente espera el saludo (Hello) de Crucible del servidor antes de tratarlo como vanilla (trucos bloqueados, productos obligatorios desactivados).",
                ["cfg_network_heading"]="Red",
                ["cfg_network__peerclasshandshake_name"]="Comprobación de versiones al unirse",
                ["cfg_network__peerclasshandshake_help"]="Comprueba las versiones de MeadWorks cuando un jugador se une: el servidor rechaza a los clientes a los que les falta un mod obligatorio de MeadWorks o que usan otra versión mayor.menor, y un cliente desactiva los mods obligatorios que el servidor no tiene. Activado por defecto; desactívalo solo para diagnosticar problemas al unirse. Reinicia el juego después de cambiar este ajuste.",
                ["cfg_logging_heading"]="Registro",
                ["cfg_logging__level_name"]="Nivel",
                ["cfg_logging__level_help"]="Nivel de registro de Meadworks: Quiet (errores y advertencias), Normal, Verbose o Debug. También: en la consola, 'meadworks log <level> [product]'.",
                ["cfg_logging__productoverrides_name"]="Niveles por producto",
                ["cfg_logging__productoverrides_help"]="Niveles por producto, por ejemplo 'Wildroot=Debug,Burrow=Normal'.",
                ["cfg_logging__repeatwindowseconds_name"]="Ventana de repetición (segundos)",
                ["cfg_logging__repeatwindowseconds_help"]="Las líneas idénticas dentro de este intervalo se escriben una vez y se cuentan; 0 escribe todas las líneas.",
                ["cfg_logging__consolefilterenabled_name"]="Filtro de consola activado",
                ["cfg_logging__consolefilterenabled_help"]="Filtra la consola de BepInEx para todos los mods instalados según las reglas de abajo.",
                ["cfg_logging__consolefilterrules_name"]="Reglas del filtro de consola",
                ["cfg_logging__consolefilterrules_help"]="Niveles de consola por origen: 'Source=All|Info|Warning|Error|None', separados por ';'. '*' fija el nivel de los orígenes no listados.",
                ["cfg_logging__consolefilterappliestodisklog_name"]="Filtro de consola también en el registro en disco",
                ["cfg_logging__consolefilterappliestodisklog_help"]="Aplica también las reglas del filtro de consola a LogOutput.log de BepInEx.",
                ["cfg_logging__showallconsolemessages_name"]="Mostrar todos los mensajes de consola",
                ["cfg_logging__showallconsolemessages_help"]="Muestra todos los mensajes en la consola de BepInEx. Por defecto, Crucible oculta una lista breve y exacta de mensajes inofensivos conocidos del juego base y de Unity (pistas de generación del mundo, shaders de audio o vídeo ausentes en un servidor dedicado y similares); LogOutput.log siempre lo conserva todo.",
                ["cfg_mischief_heading"]="Travesuras",
                ["cfg_mischief__listedplayers_name"]="Jugadores listados",
                ["cfg_mischief__listedplayers_help"]="ID de usuario de plataforma a los que se conceden las categorías de trucos Listed, separados por comas (por ejemplo Steam_76561198000000000; un número sin prefijo es un ID de Steam, como en adminlist.txt).",
                ["cfg_skills_heading"]="Habilidades",
                ["cfg_skills__experiencemultiplier_name"]="Multiplicador de experiencia",
                ["cfg_skills__experiencemultiplier_help"]="Multiplica la experiencia de cada habilidad personalizada de Meadworks (las habilidades propias de los productos, no las del juego). En multijugador se aplica el valor del servidor.",
                ["cfg_network__admissionmode_name"]="Modo de admisión",
                ["cfg_network__admissionmode_help"]="Strict: todos los clientes deben usar Meadworks. AllowVanilla: los clientes sin Meadworks (vanilla o consola) pueden unirse y reciben las funciones del servidor; solo tiene efecto cuando todos los productos instalados declaran compatibilidad con vanilla y los metadatos de Jotunn de ningún mod instalado exigen el mod en los clientes; si no, el servidor registra el motivo y sigue en Strict. Requiere Network.PeerClassHandshake en el servidor. Las partidas guardadas y los objetos nunca se modifican.",
                ["cfg_mischief__personal_name"]="Personales",
                ["cfg_mischief__personal_help"]="Quién puede usar trucos Personales en este servidor: Nobody, Admins, Listed (administradores y Jugadores listados) o Everyone. En un jugador y el anfitrión siempre se puede. Personales: solo afectan a quien los usa (modo dios, velocidad, tamaño).",
                ["cfg_mischief__economy_name"]="Economía",
                ["cfg_mischief__economy_help"]="Quién puede usar trucos de Economía en este servidor: Nobody, Admins, Listed (administradores y Jugadores listados) o Everyone. En un jugador y el anfitrión siempre se puede. Economía: crean o ahorran materiales (aparición de objetos, velocidad de fabricación, multiplicadores de botín, construcción gratis).",
                ["cfg_mischief__world_name"]="Mundo",
                ["cfg_mischief__world_help"]="Quién puede usar trucos de Mundo en este servidor: Nobody, Admins, Listed (administradores y Jugadores listados) o Everyone. En un jugador y el anfitrión siempre se puede. Mundo: cambian el mundo compartido (clima, crecimiento sin restricciones).",
                ["cfg_mischief__information_name"]="Información",
                ["cfg_mischief__information_help"]="Quién puede usar trucos de Información en este servidor: Nobody, Admins, Listed (administradores y Jugadores listados) o Everyone. En un jugador y el anfitrión siempre se puede. Información: revelan lo oculto (ESP, revelar el mapa, puntos de interés).",
                ["cfg_mischief__travel_name"]="Viaje",
                ["cfg_mischief__travel_help"]="Quién puede usar trucos de Viaje en este servidor: Nobody, Admins, Listed (administradores y Jugadores listados) o Everyone. En un jugador y el anfitrión siempre se puede. Viaje: mueven a los jugadores (teletransporte, portales).",
                ["cfg_mischief__progress_name"]="Progreso",
                ["cfg_mischief__progress_help"]="Quién puede usar trucos de Progreso en este servidor: Nobody, Admins, Listed (administradores y Jugadores listados) o Everyone. En un jugador y el anfitrión siempre se puede. Progreso: habilidades, poderes de jefes, logros, mejoras garantizadas.",
            },
            ["French"]=new Dictionary<string,string>(StringComparer.Ordinal){
                ["cfg_achievements_heading"]="Succès",
                ["cfg_achievements__syncpaststats_name"]="Synchroniser les statistiques passées",
                ["cfg_achievements__syncpaststats_help"]="Copie dans la table des succès les statistiques que le jeu a comptées pendant que les succès n'étaient pas suivis, au chargement d'un personnage propre (jamais pour les personnages ayant triché, sauf si Hex l'autorise). Désactivé si AchievementSync est installé.",
                ["cfg_achievements__resynckey_name"]="Touche de resynchronisation",
                ["cfg_achievements__resynckey_help"]="Touche qui revérifie les statistiques de votre partie sauvegardée et accorde les succès MeadWorks déjà obtenus. Non attribuée par défaut.",
                ["cfg_authority_heading"]="Autorité",
                ["cfg_authority__vanillaservertimeoutseconds_name"]="Attente d'un serveur vanilla (secondes)",
                ["cfg_authority__vanillaservertimeoutseconds_help"]="Secondes pendant lesquelles un client attend le message Hello de Crucible du serveur avant de le traiter comme vanilla (triches verrouillées, produits requis désactivés).",
                ["cfg_network_heading"]="Réseau",
                ["cfg_network__peerclasshandshake_name"]="Vérification des versions à la connexion",
                ["cfg_network__peerclasshandshake_help"]="Vérifie les versions de MeadWorks quand un joueur se connecte : le serveur refuse les clients auxquels il manque un mod MeadWorks requis ou qui utilisent une autre version majeure.mineure, et un client désactive les mods requis que le serveur n'a pas. Activé par défaut ; ne le désactivez que pour diagnostiquer des problèmes de connexion. Redémarrez le jeu après avoir modifié ce paramètre.",
                ["cfg_logging_heading"]="Journalisation",
                ["cfg_logging__level_name"]="Niveau",
                ["cfg_logging__level_help"]="Niveau de journalisation de Meadworks : Quiet (erreurs et avertissements), Normal, Verbose ou Debug. Aussi : dans la console, 'meadworks log <level> [product]'.",
                ["cfg_logging__productoverrides_name"]="Niveaux par produit",
                ["cfg_logging__productoverrides_help"]="Niveaux par produit, par exemple 'Wildroot=Debug,Burrow=Normal'.",
                ["cfg_logging__repeatwindowseconds_name"]="Fenêtre de répétition (secondes)",
                ["cfg_logging__repeatwindowseconds_help"]="Les lignes identiques dans cet intervalle sont écrites une fois et comptées ; 0 écrit chaque ligne.",
                ["cfg_logging__consolefilterenabled_name"]="Filtre de console activé",
                ["cfg_logging__consolefilterenabled_help"]="Filtre la console BepInEx pour tous les mods installés selon les règles ci-dessous.",
                ["cfg_logging__consolefilterrules_name"]="Règles du filtre de console",
                ["cfg_logging__consolefilterrules_help"]="Niveaux de console par source : 'Source=All|Info|Warning|Error|None', séparés par ';'. '*' fixe le niveau des sources non listées.",
                ["cfg_logging__consolefilterappliestodisklog_name"]="Filtre de console aussi sur le journal disque",
                ["cfg_logging__consolefilterappliestodisklog_help"]="Applique aussi les règles du filtre de console au fichier LogOutput.log de BepInEx.",
                ["cfg_logging__showallconsolemessages_name"]="Afficher tous les messages de console",
                ["cfg_logging__showallconsolemessages_help"]="Affiche chaque message dans la console BepInEx. Par défaut, Crucible masque une liste courte et exacte de messages inoffensifs connus du jeu de base et de Unity (indices de génération du monde, shaders audio ou vidéo absents sur un serveur dédié et similaires) ; LogOutput.log conserve toujours tout.",
                ["cfg_mischief_heading"]="Malice",
                ["cfg_mischief__listedplayers_name"]="Joueurs listés",
                ["cfg_mischief__listedplayers_help"]="Identifiants utilisateur de plateforme auxquels sont accordées les catégories de triche Listed, séparés par des virgules (par exemple Steam_76561198000000000 ; un nombre seul est un identifiant Steam, comme dans adminlist.txt).",
                ["cfg_skills_heading"]="Compétences",
                ["cfg_skills__experiencemultiplier_name"]="Multiplicateur d'expérience",
                ["cfg_skills__experiencemultiplier_help"]="Multiplie l'expérience de chaque compétence personnalisée de Meadworks (les compétences propres aux produits, pas celles du jeu). En multijoueur, la valeur du serveur s'applique.",
                ["cfg_network__admissionmode_name"]="Mode d'admission",
                ["cfg_network__admissionmode_help"]="Strict : chaque client doit utiliser Meadworks. AllowVanilla : les clients sans Meadworks (vanilla ou console) peuvent se connecter et reçoivent les fonctions côté serveur ; cela ne s'applique que si chaque produit installé déclare la prise en charge de vanilla et qu'aucune métadonnée Jotunn d'un mod installé n'exige le mod sur les clients, sinon le serveur consigne la raison et reste en Strict. Nécessite Network.PeerClassHandshake sur le serveur. Les sauvegardes et les objets ne sont jamais modifiés.",
                ["cfg_mischief__personal_name"]="Personnelles",
                ["cfg_mischief__personal_help"]="Qui peut utiliser les triches Personnelles sur ce serveur : Nobody, Admins, Listed (administrateurs et Joueurs listés) ou Everyone. En solo et pour l'hôte, c'est toujours permis. Personnelles : n'affectent que l'utilisateur (mode dieu, vitesse, taille).",
                ["cfg_mischief__economy_name"]="Économie",
                ["cfg_mischief__economy_help"]="Qui peut utiliser les triches d'Économie sur ce serveur : Nobody, Admins, Listed (administrateurs et Joueurs listés) ou Everyone. En solo et pour l'hôte, c'est toujours permis. Économie : créent ou économisent des matériaux (apparition d'objets, vitesse d'artisanat, multiplicateurs de butin, construction gratuite).",
                ["cfg_mischief__world_name"]="Monde",
                ["cfg_mischief__world_help"]="Qui peut utiliser les triches de Monde sur ce serveur : Nobody, Admins, Listed (administrateurs et Joueurs listés) ou Everyone. En solo et pour l'hôte, c'est toujours permis. Monde : modifient le monde partagé (météo, croissance sans restriction).",
                ["cfg_mischief__information_name"]="Information",
                ["cfg_mischief__information_help"]="Qui peut utiliser les triches d'Information sur ce serveur : Nobody, Admins, Listed (administrateurs et Joueurs listés) ou Everyone. En solo et pour l'hôte, c'est toujours permis. Information : révèlent ce qui est caché (ESP, révélation de la carte, points d'intérêt).",
                ["cfg_mischief__travel_name"]="Voyage",
                ["cfg_mischief__travel_help"]="Qui peut utiliser les triches de Voyage sur ce serveur : Nobody, Admins, Listed (administrateurs et Joueurs listés) ou Everyone. En solo et pour l'hôte, c'est toujours permis. Voyage : déplacent les joueurs (téléportation, portails).",
                ["cfg_mischief__progress_name"]="Progression",
                ["cfg_mischief__progress_help"]="Qui peut utiliser les triches de Progression sur ce serveur : Nobody, Admins, Listed (administrateurs et Joueurs listés) ou Everyone. En solo et pour l'hôte, c'est toujours permis. Progression : compétences, pouvoirs des boss, succès, améliorations garanties.",
            },
        };

        /// <summary>Shared instance; the plugin registers it with the C05 adapter so overrides apply here.</summary>
        public static ProductTranslations Default { get; }=Create();

        /// <summary>A fresh, independent translation set (tests and isolated callers).</summary>
        public static ProductTranslations Create(){
            var tables=new Dictionary<string,IReadOnlyDictionary<string,string>>(StringComparer.Ordinal);
            foreach(var row in Rows){
                var table=new Dictionary<string,string>(StringComparer.Ordinal);
                for(int i=0;i<Keys.Length;i++)table.Add(Keys[i],row.Value[i]);
                table.Add(OptionKey,row.Value[Keys.Length]);
                if(SettingRows.TryGetValue(row.Key,out var settings))foreach(var setting in settings)table.Add(setting.Key,setting.Value);
                tables.Add(row.Key,table);
            }
            return ProductTranslations.FromTables(Product,tables);
        }

        public static IReadOnlyList<string> ReasonKeys=>Keys;

        public static string KeyFor(ConfigBlockReason reason){
            int index=Array.IndexOf(Order,reason);
            if(index<0)throw new ArgumentException("No text for block reason "+reason+".",nameof(reason));
            return Keys[index];
        }

        /// <summary>Authoritative English text; empty for <see cref="ConfigBlockReason.None"/>.</summary>
        public static string English(ConfigBlockReason reason)=>reason==ConfigBlockReason.None?"":Rows["English"][Array.IndexOf(Keys,KeyFor(reason))];

        /// <summary>Reason text in a language (English fallback); empty when nothing is blocked.</summary>
        public static string Reason(ConfigBlockReason reason,string? language,ProductTranslations? translations=null)=>
            reason==ConfigBlockReason.None?"":(translations??Default).Text(KeyFor(reason),language);

        /// <summary>"Option: reason" for the player; empty when nothing is blocked.</summary>
        public static string Message(string option,ConfigBlockReason reason,string? language,ProductTranslations? translations=null){
            if(reason==ConfigBlockReason.None)return "";
            var t=translations??Default;
            return t.Format(OptionKey,language,option??"",Reason(reason,language,t));
        }
    }

    /// <summary>
    /// What a product asks before acting on an option: allowed or blocked with a reason. Blocked options are read-only
    /// in configuration managers and explain themselves to the player.
    /// </summary>
    public sealed class BlockedOption {
        public string Product { get; }
        public string Option { get; }
        public bool Allowed { get; }
        public ConfigBlockReason Reason { get; }
        public ConfigValueSource Source { get; }
        /// <summary>False when the product is off for this session.</summary>
        public bool ProductActive { get; }
        public bool RequiresServerAmendment { get; }
        public bool ReadOnly=>!Allowed;
        BlockedOption(string product,string option,bool allowed,ConfigBlockReason reason,ConfigValueSource source,bool active,bool amend){
            BlockedOptionNotices.ValidateName(product,nameof(product));BlockedOptionNotices.ValidateName(option,nameof(option));
            Product=product;Option=option;Allowed=allowed;Reason=reason;Source=source;ProductActive=active;RequiresServerAmendment=amend;
        }
        public static BlockedOption From(string product,string option,AuthorityDecision decision){
            if(decision==null)throw new ArgumentNullException(nameof(decision));
            return new BlockedOption(product,option,decision.Allowed,decision.Reason,decision.Source,decision.ProductActive,decision.RequiresServerAmendment);
        }
        /// <summary>Whether the player may use the option's value in effect (<see cref="AuthorityDecision.MayUse"/>), not whether they may edit it.</summary>
        public static BlockedOption ForUse(string product,string option,AuthorityDecision decision){
            if(decision==null)throw new ArgumentNullException(nameof(decision));
            bool use=decision.MayUse;
            return new BlockedOption(product,option,use,use?ConfigBlockReason.None:decision.Reason,decision.Source,decision.ProductActive,decision.RequiresServerAmendment);
        }
        /// <summary>A cheat's status from the Mischief gate (C13).</summary>
        public static BlockedOption From(string product,string option,MischiefDecision decision){
            if(decision==null)throw new ArgumentNullException(nameof(decision));
            return new BlockedOption(product,option,decision.Allowed,decision.Reason,decision.Source,decision.ProductActive,decision.RequiresServerAmendment);
        }
        public static BlockedOption From(string product,string option,ConfigEditDecision decision){
            if(decision==null)throw new ArgumentNullException(nameof(decision));
            return new BlockedOption(product,option,decision.Allowed,decision.Reason,decision.Source,true,decision.RequiresServerAmendment);
        }
        /// <summary>Localized reason; empty when allowed.</summary>
        public string Text(string? language,ProductTranslations? translations=null)=>Allowed?"":BlockedOptionText.Reason(Reason,language,translations);
        /// <summary>Localized player message naming the option; empty when allowed.</summary>
        public string Message(string label,string? language,ProductTranslations? translations=null)=>Allowed?"":BlockedOptionText.Message(label,Reason,language,translations);
        public override string ToString()=>Product+" "+Option+": "+(Allowed?"Allowed":"Blocked:"+Reason)+" source="+Source+(ProductActive?"":" product-off");
    }

    /// <summary>
    /// Read-only tagging for configuration managers, as Charter's ReadOnlyTags does it: one marker object per caller,
    /// added to a setting's description tags while the setting is blocked and removed afterwards. The marker goes last,
    /// so a ConfigurationManager-style attribute object carrying <c>ReadOnly = true</c> overrides earlier ones.
    /// </summary>
    public static class ConfigTags {
        public static object[] Apply(object[]? tags,object marker,bool readOnly){
            if(marker==null)throw new ArgumentNullException(nameof(marker));
            var rest=(tags??Array.Empty<object>()).Where(t=>!ReferenceEquals(t,marker));
            return readOnly?rest.Append(marker).ToArray():rest.ToArray();
        }
        public static bool Has(object[]? tags,object marker)=>tags!=null&&tags.Any(t=>ReferenceEquals(t,marker));
        /// <summary>Tags are rewritten only when the read-only state changes.</summary>
        public static bool Changed(bool applied,bool readOnly)=>applied!=readOnly;
    }

    public enum NoticeOutcome {
        /// <summary>Tell the player now.</summary>
        Show,
        /// <summary>Already told this session for this option and reason.</summary>
        Suppressed,
        /// <summary>The option was unblocked; the next block is shown again.</summary>
        Cleared,
        /// <summary>Nothing blocked and nothing remembered.</summary>
        Idle,
    }

    /// <summary>
    /// Rate limit for blocked-option player messages: once per product option per session, shown again only when the
    /// reason changes or after the option was unblocked. A new session generation forgets everything. Bounded; when
    /// full, new options are suppressed (never flood the player), and the log still carries the reason.
    /// </summary>
    public sealed class BlockedOptionNotices {
        readonly object gate=new object();
        readonly Dictionary<(string Product,string Option),ConfigBlockReason> shown=new Dictionary<(string,string),ConfigBlockReason>();
        readonly int capacity;
        long session;bool started;
        public BlockedOptionNotices(int capacity=4096){
            if(capacity<=0)throw new ArgumentOutOfRangeException(nameof(capacity),"Capacity must be positive.");
            this.capacity=capacity;
        }
        public int Count{get{lock(gate)return shown.Count;}}
        public void Reset(){lock(gate){shown.Clear();started=false;}}

        public NoticeOutcome Observe(long sessionGeneration,string product,string option,ConfigBlockReason reason){
            ValidateName(product,nameof(product));ValidateName(option,nameof(option));
            // A range check, not Enum.IsDefined (which boxes): products notify every frame. The reasons run 0..n-1.
            if((uint)reason>=(uint)ReasonCount)throw new ArgumentException("Unknown block reason.",nameof(reason));
            lock(gate){
                if(!started||session!=sessionGeneration){shown.Clear();session=sessionGeneration;started=true;}
                var key=(product,option);
                if(reason==ConfigBlockReason.None)return shown.Remove(key)?NoticeOutcome.Cleared:NoticeOutcome.Idle;
                if(shown.TryGetValue(key,out var previous)){
                    if(previous==reason)return NoticeOutcome.Suppressed;
                    shown[key]=reason;return NoticeOutcome.Show;
                }
                if(shown.Count>=capacity)return NoticeOutcome.Suppressed;
                shown.Add(key,reason);return NoticeOutcome.Show;
            }
        }

        static readonly char[] LineBreaks={'\r','\n','\0'};
        /// <summary>Whether <paramref name="value"/> is a valid product or option name: one line, 1 to 128 characters.</summary>
        static readonly int ReasonCount=Enum.GetValues(typeof(ConfigBlockReason)).Length;
        public static bool IsValidName(string? value)=>!string.IsNullOrWhiteSpace(value)&&value!.Length<=128&&value.IndexOfAny(LineBreaks)<0;
        internal static void ValidateName(string value,string name){
            if(!IsValidName(value))throw new ArgumentException("Expected a single-line name of at most 128 characters.",name);
        }
    }
}
