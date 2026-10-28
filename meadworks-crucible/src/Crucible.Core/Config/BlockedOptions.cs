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
                ["cfg_achievements__syncpaststats_name"]="Sync Past Stats",
                ["cfg_achievements__syncpaststats_help"]="When a character that has not used cheats loads, copy the stats the game recorded while achievements were not tracked into the achievement table. Characters that used cheats are never copied unless Hex allows it. Disabled while AchievementSync is installed. Crucible owns this local installation setting.",
                ["cfg_achievements__resynckey_name"]="Resync Key",
                ["cfg_achievements__resynckey_help"]="Key that rechecks your saved game statistics and grants any MeadWorks achievements already earned. Unassigned by default. Crucible owns this local installation setting.",
                ["cfg_authority_heading"]="Authority",
                ["cfg_authority__vanillaservertimeoutseconds_name"]="Vanilla Server Wait Time (Seconds)",
                ["cfg_authority__vanillaservertimeoutseconds_help"]="Seconds a client waits for the server's Crucible Hello before treating the server as vanilla (cheats locked, required products off). Crucible owns this local installation setting.",
                ["cfg_network_heading"]="Network",
                ["cfg_network__peerclasshandshake_name"]="Version Check When Joining",
                ["cfg_network__peerclasshandshake_help"]="Checks MeadWorks versions when a player joins: the server turns away clients missing a required MeadWorks mod or running a different major.minor version, and a client turns off required mods the server lacks. On by default; turn it off only to diagnose join problems. Restart the game after changing this setting. Crucible owns this local installation setting.",
                ["cfg_logging_heading"]="Logging",
                ["cfg_logging__level_name"]="Level",
                ["cfg_logging__level_help"]="MeadWorks log level: Quiet (errors and warnings), Normal, Verbose or Debug. Also: console 'meadworks log <level> [product]'. Crucible owns this local installation setting.",
                ["cfg_logging__productoverrides_name"]="Levels by Product",
                ["cfg_logging__productoverrides_help"]="Per-product levels, for example 'Wildroot=Debug,Burrow=Normal'. Crucible owns this local installation setting.",
                ["cfg_logging__repeatwindowseconds_name"]="Repeat Window (Seconds)",
                ["cfg_logging__repeatwindowseconds_help"]="Identical lines inside this window are written once and counted; 0 writes every line. Crucible owns this local installation setting.",
                ["cfg_logging__consolefilterenabled_name"]="Enable Console Filter",
                ["cfg_logging__consolefilterenabled_help"]="Filter the BepInEx console for all installed mods by the rules below. Crucible owns this local installation setting.",
                ["cfg_logging__consolefilterrules_name"]="Console Filter Rules",
                ["cfg_logging__consolefilterrules_help"]="Per-source console levels: 'Source=All|Info|Warning|Error|None', separated by ';'. '*' sets the level for unlisted sources. Crucible owns this local installation setting.",
                ["cfg_logging__consolefilterappliestodisklog_name"]="Apply Console Filter to Log File",
                ["cfg_logging__consolefilterappliestodisklog_help"]="Also apply the console filter rules to BepInEx's LogOutput.log. Crucible owns this local installation setting.",
                ["cfg_logging__showallconsolemessages_name"]="Show All Console Messages",
                ["cfg_logging__showallconsolemessages_help"]="Show every message on the BepInEx console. By default Crucible hides a short, exact list of known-harmless vanilla and Unity messages (world-generation hints, missing audio or video shaders on a dedicated server and similar); LogOutput.log always keeps everything. Crucible owns this local installation setting.",
                ["cfg_mischief_heading"]="Mischief",
                ["cfg_mischief__listedplayers_name"]="Listed Players",
                ["cfg_mischief__listedplayers_help"]="Platform user IDs granted Listed cheat categories, separated by commas (for example Steam_76561198000000000; a bare number means a Steam ID, as in adminlist.txt). Crucible owns this server setting; its value applies to joining clients.",
                ["cfg_skills_heading"]="Skills",
                ["cfg_skills__experiencemultiplier_name"]="Experience Multiplier",
                ["cfg_skills__experiencemultiplier_help"]="Multiplies experience in every MeadWorks custom skill (products' own skills, not the game's). The server's value applies in multiplayer. Crucible owns this server setting; its value applies to joining clients.",
                ["cfg_mischief__personal_name"]="Personal",
                ["cfg_mischief__personal_help"]="Who may use Personal cheats on this server: Nobody, Admins, Listed (admins and Listed Players) or Everyone. Single player and the host always may. Personal: affects only the user (god mode, speed, size). Crucible owns this server setting; its value applies to joining clients.",
                ["cfg_mischief__economy_name"]="Economy",
                ["cfg_mischief__economy_help"]="Who may use Economy cheats on this server: Nobody, Admins, Listed (admins and Listed Players) or Everyone. Single player and the host always may. Economy: creates or saves materials (spawning, craft speed, loot multipliers, free build). Crucible owns this server setting; its value applies to joining clients.",
                ["cfg_mischief__world_name"]="World",
                ["cfg_mischief__world_help"]="Who may use World cheats on this server: Nobody, Admins, Listed (admins and Listed Players) or Everyone. Single player and the host always may. World: changes the shared world (weather, unrestricted growth). Crucible owns this server setting; its value applies to joining clients.",
                ["cfg_mischief__information_name"]="Information",
                ["cfg_mischief__information_help"]="Who may use Information cheats on this server: Nobody, Admins, Listed (admins and Listed Players) or Everyone. Single player and the host always may. Information: reveals what is hidden (ESP, map reveal, points of interest). Crucible owns this server setting; its value applies to joining clients.",
                ["cfg_mischief__travel_name"]="Travel",
                ["cfg_mischief__travel_help"]="Who may use Travel cheats on this server: Nobody, Admins, Listed (admins and Listed Players) or Everyone. Single player and the host always may. Travel: moves players (teleport, portals). Crucible owns this server setting; its value applies to joining clients.",
                ["cfg_mischief__progress_name"]="Progress",
                ["cfg_mischief__progress_help"]="Who may use Progress cheats on this server: Nobody, Admins, Listed (admins and Listed Players) or Everyone. Single player and the host always may. Progress: skills, boss powers, achievements, guaranteed upgrades. Crucible owns this server setting; its value applies to joining clients.",
            },
            // Every language is complete. Enum values,
            // console commands, file names and Section.Key literals stay as written.
            ["Spanish"]=new Dictionary<string,string>(StringComparer.Ordinal){
                ["cfg_achievements_heading"]="Logros",
                ["cfg_achievements__syncpaststats_name"]="Sincronizar estadísticas pasadas",
                ["cfg_achievements__syncpaststats_help"]="Al cargar un personaje que no haya usado trucos, copia a la tabla de logros las estadísticas que el juego registró mientras no se seguían los logros. Nunca se copian las de personajes que hayan usado trucos, salvo que Hex lo permita. Se desactiva mientras AchievementSync está instalado. Crucible gestiona esta configuración local de la instalación.",
                ["cfg_achievements__resynckey_name"]="Tecla de resincronización",
                ["cfg_achievements__resynckey_help"]="Tecla que vuelve a revisar las estadísticas de tu partida guardada y concede los logros de MeadWorks ya conseguidos. Sin asignar por defecto. Crucible gestiona esta configuración local de la instalación.",
                ["cfg_authority_heading"]="Autoridad",
                ["cfg_authority__vanillaservertimeoutseconds_name"]="Espera de servidor vanilla (segundos)",
                ["cfg_authority__vanillaservertimeoutseconds_help"]="Segundos que un cliente espera el saludo (Hello) de Crucible del servidor antes de tratarlo como vanilla (trucos bloqueados, productos obligatorios desactivados). Crucible gestiona esta configuración local de la instalación.",
                ["cfg_network_heading"]="Red",
                ["cfg_network__peerclasshandshake_name"]="Comprobación de versiones al unirse",
                ["cfg_network__peerclasshandshake_help"]="Comprueba las versiones de MeadWorks cuando un jugador se une: el servidor rechaza a los clientes a los que les falta un mod obligatorio de MeadWorks o que usan otra versión mayor.menor, y un cliente desactiva los mods obligatorios que el servidor no tiene. Activado por defecto; desactívalo solo para diagnosticar problemas al unirse. Reinicia el juego después de cambiar este ajuste. Crucible gestiona esta configuración local de la instalación.",
                ["cfg_logging_heading"]="Registro",
                ["cfg_logging__level_name"]="Nivel",
                ["cfg_logging__level_help"]="Nivel de registro de MeadWorks: Quiet (errores y advertencias), Normal, Verbose o Debug. También: en la consola, 'meadworks log <level> [product]'. Crucible gestiona esta configuración local de la instalación.",
                ["cfg_logging__productoverrides_name"]="Niveles por producto",
                ["cfg_logging__productoverrides_help"]="Niveles por producto, por ejemplo 'Wildroot=Debug,Burrow=Normal'. Crucible gestiona esta configuración local de la instalación.",
                ["cfg_logging__repeatwindowseconds_name"]="Ventana de repetición (segundos)",
                ["cfg_logging__repeatwindowseconds_help"]="Las líneas idénticas dentro de este intervalo se escriben una vez y se cuentan; 0 escribe todas las líneas. Crucible gestiona esta configuración local de la instalación.",
                ["cfg_logging__consolefilterenabled_name"]="Filtro de consola activado",
                ["cfg_logging__consolefilterenabled_help"]="Filtra la consola de BepInEx para todos los mods instalados según las reglas de abajo. Crucible gestiona esta configuración local de la instalación.",
                ["cfg_logging__consolefilterrules_name"]="Reglas del filtro de consola",
                ["cfg_logging__consolefilterrules_help"]="Niveles de consola por origen: 'Source=All|Info|Warning|Error|None', separados por ';'. '*' fija el nivel de los orígenes no listados. Crucible gestiona esta configuración local de la instalación.",
                ["cfg_logging__consolefilterappliestodisklog_name"]="Filtro de consola también en el registro en disco",
                ["cfg_logging__consolefilterappliestodisklog_help"]="Aplica también las reglas del filtro de consola a LogOutput.log de BepInEx. Crucible gestiona esta configuración local de la instalación.",
                ["cfg_logging__showallconsolemessages_name"]="Mostrar todos los mensajes de consola",
                ["cfg_logging__showallconsolemessages_help"]="Muestra todos los mensajes en la consola de BepInEx. Por defecto, Crucible oculta una lista breve y exacta de mensajes inofensivos conocidos del juego base y de Unity (pistas de generación del mundo, shaders de audio o vídeo ausentes en un servidor dedicado y similares); LogOutput.log siempre lo conserva todo. Crucible gestiona esta configuración local de la instalación.",
                ["cfg_mischief_heading"]="Travesuras",
                ["cfg_mischief__listedplayers_name"]="Jugadores listados",
                ["cfg_mischief__listedplayers_help"]="ID de usuario de plataforma a los que se conceden las categorías de trucos Listed, separados por comas (por ejemplo Steam_76561198000000000; un número sin prefijo es un ID de Steam, como en adminlist.txt). Crucible gestiona esta configuración del servidor; su valor se aplica a los clientes que se unen.",
                ["cfg_skills_heading"]="Habilidades",
                ["cfg_skills__experiencemultiplier_name"]="Multiplicador de experiencia",
                ["cfg_skills__experiencemultiplier_help"]="Multiplica la experiencia de cada habilidad personalizada de MeadWorks (las habilidades propias de los productos, no las del juego). En multijugador se aplica el valor del servidor. Crucible gestiona esta configuración del servidor; su valor se aplica a los clientes que se unen.",
                ["cfg_mischief__personal_name"]="Personales",
                ["cfg_mischief__personal_help"]="Quién puede usar trucos Personales en este servidor: Nobody, Admins, Listed (administradores y Jugadores listados) o Everyone. En solitario y para el anfitrión, siempre está permitido. Personales: solo afectan a quien los usa (modo dios, velocidad, tamaño). Crucible gestiona esta configuración del servidor; su valor se aplica a los clientes que se unen.",
                ["cfg_mischief__economy_name"]="Economía",
                ["cfg_mischief__economy_help"]="Quién puede usar trucos de Economía en este servidor: Nobody, Admins, Listed (administradores y Jugadores listados) o Everyone. En solitario y para el anfitrión, siempre está permitido. Economía: crean o ahorran materiales (aparición de objetos, velocidad de fabricación, multiplicadores de botín, construcción gratis). Crucible gestiona esta configuración del servidor; su valor se aplica a los clientes que se unen.",
                ["cfg_mischief__world_name"]="Mundo",
                ["cfg_mischief__world_help"]="Quién puede usar trucos de Mundo en este servidor: Nobody, Admins, Listed (administradores y Jugadores listados) o Everyone. En solitario y para el anfitrión, siempre está permitido. Mundo: cambian el mundo compartido (clima, crecimiento sin restricciones). Crucible gestiona esta configuración del servidor; su valor se aplica a los clientes que se unen.",
                ["cfg_mischief__information_name"]="Información",
                ["cfg_mischief__information_help"]="Quién puede usar trucos de Información en este servidor: Nobody, Admins, Listed (administradores y Jugadores listados) o Everyone. En solitario y para el anfitrión, siempre está permitido. Información: revelan lo oculto (ESP, revelar el mapa, puntos de interés). Crucible gestiona esta configuración del servidor; su valor se aplica a los clientes que se unen.",
                ["cfg_mischief__travel_name"]="Viaje",
                ["cfg_mischief__travel_help"]="Quién puede usar trucos de Viaje en este servidor: Nobody, Admins, Listed (administradores y Jugadores listados) o Everyone. En solitario y para el anfitrión, siempre está permitido. Viaje: mueven a los jugadores (teletransporte, portales). Crucible gestiona esta configuración del servidor; su valor se aplica a los clientes que se unen.",
                ["cfg_mischief__progress_name"]="Progreso",
                ["cfg_mischief__progress_help"]="Quién puede usar trucos de Progreso en este servidor: Nobody, Admins, Listed (administradores y Jugadores listados) o Everyone. En solitario y para el anfitrión, siempre está permitido. Progreso: habilidades, poderes de jefes, logros, mejoras garantizadas. Crucible gestiona esta configuración del servidor; su valor se aplica a los clientes que se unen.",
            },
            ["French"]=new Dictionary<string,string>(StringComparer.Ordinal){
                ["cfg_achievements_heading"]="Succès",
                ["cfg_achievements__syncpaststats_name"]="Synchroniser les statistiques passées",
                ["cfg_achievements__syncpaststats_help"]="Au chargement d'un personnage qui n'a pas triché, copie dans la table des succès les statistiques enregistrées par le jeu pendant que les succès n'étaient pas suivis. Les personnages ayant triché sont toujours exclus, sauf si Hex l'autorise. Désactivé tant qu'AchievementSync est installé. Ce réglage local de l'installation est géré par Crucible.",
                ["cfg_achievements__resynckey_name"]="Touche de resynchronisation",
                ["cfg_achievements__resynckey_help"]="Touche qui revérifie les statistiques de votre partie sauvegardée et accorde les succès MeadWorks déjà obtenus. Non attribuée par défaut. Ce réglage local de l'installation est géré par Crucible.",
                ["cfg_authority_heading"]="Autorité",
                ["cfg_authority__vanillaservertimeoutseconds_name"]="Attente d'un serveur vanilla (secondes)",
                ["cfg_authority__vanillaservertimeoutseconds_help"]="Secondes pendant lesquelles un client attend le message Hello de Crucible du serveur avant de le traiter comme vanilla (triches verrouillées, produits requis désactivés). Ce réglage local de l'installation est géré par Crucible.",
                ["cfg_network_heading"]="Réseau",
                ["cfg_network__peerclasshandshake_name"]="Vérification des versions à la connexion",
                ["cfg_network__peerclasshandshake_help"]="Vérifie les versions de MeadWorks quand un joueur se connecte : le serveur refuse les clients auxquels il manque un mod MeadWorks requis ou qui utilisent une autre version majeure.mineure, et un client désactive les mods requis que le serveur n'a pas. Activé par défaut ; ne le désactivez que pour diagnostiquer des problèmes de connexion. Redémarrez le jeu après avoir modifié ce paramètre. Ce réglage local de l'installation est géré par Crucible.",
                ["cfg_logging_heading"]="Journalisation",
                ["cfg_logging__level_name"]="Niveau",
                ["cfg_logging__level_help"]="Niveau de journalisation de MeadWorks : Quiet (erreurs et avertissements), Normal, Verbose ou Debug. Aussi : dans la console, 'meadworks log <level> [product]'. Ce réglage local de l'installation est géré par Crucible.",
                ["cfg_logging__productoverrides_name"]="Niveaux par produit",
                ["cfg_logging__productoverrides_help"]="Niveaux par produit, par exemple 'Wildroot=Debug,Burrow=Normal'. Ce réglage local de l'installation est géré par Crucible.",
                ["cfg_logging__repeatwindowseconds_name"]="Fenêtre de répétition (secondes)",
                ["cfg_logging__repeatwindowseconds_help"]="Les lignes identiques dans cet intervalle sont écrites une fois et comptées ; 0 écrit chaque ligne. Ce réglage local de l'installation est géré par Crucible.",
                ["cfg_logging__consolefilterenabled_name"]="Filtre de console activé",
                ["cfg_logging__consolefilterenabled_help"]="Filtre la console BepInEx pour tous les mods installés selon les règles ci-dessous. Ce réglage local de l'installation est géré par Crucible.",
                ["cfg_logging__consolefilterrules_name"]="Règles du filtre de console",
                ["cfg_logging__consolefilterrules_help"]="Niveaux de console par source : 'Source=All|Info|Warning|Error|None', séparés par ';'. '*' fixe le niveau des sources non listées. Ce réglage local de l'installation est géré par Crucible.",
                ["cfg_logging__consolefilterappliestodisklog_name"]="Filtre de console aussi sur le journal disque",
                ["cfg_logging__consolefilterappliestodisklog_help"]="Applique aussi les règles du filtre de console au fichier LogOutput.log de BepInEx. Ce réglage local de l'installation est géré par Crucible.",
                ["cfg_logging__showallconsolemessages_name"]="Afficher tous les messages de console",
                ["cfg_logging__showallconsolemessages_help"]="Affiche chaque message dans la console BepInEx. Par défaut, Crucible masque une liste courte et exacte de messages inoffensifs connus du jeu de base et de Unity (indices de génération du monde, shaders audio ou vidéo absents sur un serveur dédié et similaires) ; LogOutput.log conserve toujours tout. Ce réglage local de l'installation est géré par Crucible.",
                ["cfg_mischief_heading"]="Malice",
                ["cfg_mischief__listedplayers_name"]="Joueurs listés",
                ["cfg_mischief__listedplayers_help"]="Identifiants utilisateur de plateforme auxquels sont accordées les catégories de triche Listed, séparés par des virgules (par exemple Steam_76561198000000000 ; un nombre seul est un identifiant Steam, comme dans adminlist.txt). Ce réglage serveur est géré par Crucible ; sa valeur s'applique aux clients qui le rejoignent.",
                ["cfg_skills_heading"]="Compétences",
                ["cfg_skills__experiencemultiplier_name"]="Multiplicateur d'expérience",
                ["cfg_skills__experiencemultiplier_help"]="Multiplie l'expérience de chaque compétence personnalisée de MeadWorks (les compétences propres aux produits, pas celles du jeu). En multijoueur, la valeur du serveur s'applique. Ce réglage serveur est géré par Crucible ; sa valeur s'applique aux clients qui le rejoignent.",
                ["cfg_mischief__personal_name"]="Personnelles",
                ["cfg_mischief__personal_help"]="Qui peut utiliser les triches Personnelles sur ce serveur : Nobody, Admins, Listed (administrateurs et Joueurs listés) ou Everyone. En solo et pour l'hôte, c'est toujours permis. Personnelles : n'affectent que l'utilisateur (mode dieu, vitesse, taille). Ce réglage serveur est géré par Crucible ; sa valeur s'applique aux clients qui le rejoignent.",
                ["cfg_mischief__economy_name"]="Économie",
                ["cfg_mischief__economy_help"]="Qui peut utiliser les triches d'Économie sur ce serveur : Nobody, Admins, Listed (administrateurs et Joueurs listés) ou Everyone. En solo et pour l'hôte, c'est toujours permis. Économie : créent ou économisent des matériaux (apparition d'objets, vitesse d'artisanat, multiplicateurs de butin, construction gratuite). Ce réglage serveur est géré par Crucible ; sa valeur s'applique aux clients qui le rejoignent.",
                ["cfg_mischief__world_name"]="Monde",
                ["cfg_mischief__world_help"]="Qui peut utiliser les triches de Monde sur ce serveur : Nobody, Admins, Listed (administrateurs et Joueurs listés) ou Everyone. En solo et pour l'hôte, c'est toujours permis. Monde : modifient le monde partagé (météo, croissance sans restriction). Ce réglage serveur est géré par Crucible ; sa valeur s'applique aux clients qui le rejoignent.",
                ["cfg_mischief__information_name"]="Information",
                ["cfg_mischief__information_help"]="Qui peut utiliser les triches d'Information sur ce serveur : Nobody, Admins, Listed (administrateurs et Joueurs listés) ou Everyone. En solo et pour l'hôte, c'est toujours permis. Information : révèlent ce qui est caché (ESP, révélation de la carte, points d'intérêt). Ce réglage serveur est géré par Crucible ; sa valeur s'applique aux clients qui le rejoignent.",
                ["cfg_mischief__travel_name"]="Voyage",
                ["cfg_mischief__travel_help"]="Qui peut utiliser les triches de Voyage sur ce serveur : Nobody, Admins, Listed (administrateurs et Joueurs listés) ou Everyone. En solo et pour l'hôte, c'est toujours permis. Voyage : déplacent les joueurs (téléportation, portails). Ce réglage serveur est géré par Crucible ; sa valeur s'applique aux clients qui le rejoignent.",
                ["cfg_mischief__progress_name"]="Progression",
                ["cfg_mischief__progress_help"]="Qui peut utiliser les triches de Progression sur ce serveur : Nobody, Admins, Listed (administrateurs et Joueurs listés) ou Everyone. En solo et pour l'hôte, c'est toujours permis. Progression : compétences, pouvoirs des boss, succès, améliorations garanties. Ce réglage serveur est géré par Crucible ; sa valeur s'applique aux clients qui le rejoignent.",
            },
            ["German"]=new Dictionary<string,string>(StringComparer.Ordinal){
                ["cfg_achievements_heading"]="Errungenschaften",
                ["cfg_achievements__syncpaststats_name"]="Frühere Statistiken übernehmen",
                ["cfg_achievements__syncpaststats_help"]="Überträgt beim Laden eines Charakters, der keine Cheats benutzt hat, die zuvor vom Spiel erfassten Statistiken in die Errungenschaftstabelle. Charaktere, die Cheats benutzt haben, werden nie berücksichtigt, außer Hex erlaubt es. Deaktiviert, solange AchievementSync installiert ist. Diese lokale Installationseinstellung wird von Crucible verwaltet.",
                ["cfg_achievements__resynckey_name"]="Taste zum Neuabgleich",
                ["cfg_achievements__resynckey_help"]="Taste, die die Statistiken deines Spielstands erneut prüft und bereits verdiente MeadWorks-Errungenschaften vergibt. Standardmäßig nicht belegt. Diese lokale Installationseinstellung wird von Crucible verwaltet.",
                ["cfg_authority_heading"]="Autorität",
                ["cfg_authority__vanillaservertimeoutseconds_name"]="Wartezeit auf Vanilla-Server (Sekunden)",
                ["cfg_authority__vanillaservertimeoutseconds_help"]="Sekunden, die ein Client auf das Crucible-Hello des Servers wartet, bevor er ihn als Vanilla-Server behandelt (Cheats gesperrt, erforderliche Produkte aus). Diese lokale Installationseinstellung wird von Crucible verwaltet.",
                ["cfg_network_heading"]="Netzwerk",
                ["cfg_network__peerclasshandshake_name"]="Versionsprüfung beim Beitritt",
                ["cfg_network__peerclasshandshake_help"]="Prüft die MeadWorks-Versionen, wenn ein Spieler beitritt: Der Server weist Clients ab, denen ein erforderlicher MeadWorks-Mod fehlt oder die eine andere Major.Minor-Version nutzen, und ein Client schaltet erforderliche Mods ab, die der Server nicht hat. Standardmäßig an; schalte es nur zur Diagnose von Beitrittsproblemen aus. Starte das Spiel nach dem Ändern dieser Einstellung neu. Diese lokale Installationseinstellung wird von Crucible verwaltet.",
                ["cfg_logging_heading"]="Protokoll",
                ["cfg_logging__level_name"]="Stufe",
                ["cfg_logging__level_help"]="MeadWorks-Protokollstufe: Quiet (Fehler und Warnungen), Normal, Verbose oder Debug. Auch über die Konsole: 'meadworks log <level> [product]'. Diese lokale Installationseinstellung wird von Crucible verwaltet.",
                ["cfg_logging__productoverrides_name"]="Stufen pro Produkt",
                ["cfg_logging__productoverrides_help"]="Stufen pro Produkt, zum Beispiel 'Wildroot=Debug,Burrow=Normal'. Diese lokale Installationseinstellung wird von Crucible verwaltet.",
                ["cfg_logging__repeatwindowseconds_name"]="Wiederholungsfenster (Sekunden)",
                ["cfg_logging__repeatwindowseconds_help"]="Identische Zeilen innerhalb dieses Zeitfensters werden einmal geschrieben und gezählt; 0 schreibt jede Zeile. Diese lokale Installationseinstellung wird von Crucible verwaltet.",
                ["cfg_logging__consolefilterenabled_name"]="Konsolenfilter aktiv",
                ["cfg_logging__consolefilterenabled_help"]="Filtert die BepInEx-Konsole für alle installierten Mods nach den Regeln unten. Diese lokale Installationseinstellung wird von Crucible verwaltet.",
                ["cfg_logging__consolefilterrules_name"]="Konsolenfilterregeln",
                ["cfg_logging__consolefilterrules_help"]="Konsolenstufen pro Quelle: 'Source=All|Info|Warning|Error|None', getrennt durch ';'. '*' legt die Stufe für nicht aufgeführte Quellen fest. Diese lokale Installationseinstellung wird von Crucible verwaltet.",
                ["cfg_logging__consolefilterappliestodisklog_name"]="Konsolenfilter auch für Protokolldatei",
                ["cfg_logging__consolefilterappliestodisklog_help"]="Wendet die Konsolenfilterregeln auch auf die LogOutput.log von BepInEx an. Diese lokale Installationseinstellung wird von Crucible verwaltet.",
                ["cfg_logging__showallconsolemessages_name"]="Alle Konsolenmeldungen anzeigen",
                ["cfg_logging__showallconsolemessages_help"]="Zeigt jede Meldung in der BepInEx-Konsole an. Standardmäßig blendet Crucible eine kurze, genaue Liste bekannter harmloser Meldungen des Grundspiels und von Unity aus (Hinweise zur Weltgenerierung, fehlende Audio- oder Video-Shader auf einem dedizierten Server und Ähnliches); LogOutput.log behält immer alles. Diese lokale Installationseinstellung wird von Crucible verwaltet.",
                ["cfg_mischief_heading"]="Unfug",
                ["cfg_mischief__listedplayers_name"]="Gelistete Spieler",
                ["cfg_mischief__listedplayers_help"]="Plattform-Benutzer-IDs, denen die Cheat-Kategorien mit Listed gewährt werden, durch Kommas getrennt (zum Beispiel Steam_76561198000000000; eine reine Zahl ist eine Steam-ID, wie in adminlist.txt). Diese Servereinstellung wird von Crucible verwaltet; ihr Wert gilt für beitretende Clients.",
                ["cfg_skills_heading"]="Fertigkeiten",
                ["cfg_skills__experiencemultiplier_name"]="Erfahrungsmultiplikator",
                ["cfg_skills__experiencemultiplier_help"]="Multipliziert die Erfahrung in jeder eigenen MeadWorks-Fertigkeit (die Fertigkeiten der Produkte, nicht die des Spiels). Im Mehrspielermodus gilt der Wert des Servers. Diese Servereinstellung wird von Crucible verwaltet; ihr Wert gilt für beitretende Clients.",
                ["cfg_mischief__personal_name"]="Persönlich",
                ["cfg_mischief__personal_help"]="Wer auf diesem Server persönliche Cheats nutzen darf: Nobody, Admins, Listed (Admins und gelistete Spieler) oder Everyone. Im Einzelspielermodus und als Host immer erlaubt. Persönlich: betrifft nur den Nutzer (Gottmodus, Tempo, Größe). Diese Servereinstellung wird von Crucible verwaltet; ihr Wert gilt für beitretende Clients.",
                ["cfg_mischief__economy_name"]="Wirtschaft",
                ["cfg_mischief__economy_help"]="Wer auf diesem Server Wirtschafts-Cheats nutzen darf: Nobody, Admins, Listed (Admins und gelistete Spieler) oder Everyone. Im Einzelspielermodus und als Host immer erlaubt. Wirtschaft: erzeugt oder spart Materialien (Spawnen, Herstellungstempo, Beute-Multiplikatoren, kostenloses Bauen). Diese Servereinstellung wird von Crucible verwaltet; ihr Wert gilt für beitretende Clients.",
                ["cfg_mischief__world_name"]="Welt",
                ["cfg_mischief__world_help"]="Wer auf diesem Server Welt-Cheats nutzen darf: Nobody, Admins, Listed (Admins und gelistete Spieler) oder Everyone. Im Einzelspielermodus und als Host immer erlaubt. Welt: verändert die gemeinsame Welt (Wetter, uneingeschränktes Wachstum). Diese Servereinstellung wird von Crucible verwaltet; ihr Wert gilt für beitretende Clients.",
                ["cfg_mischief__information_name"]="Information",
                ["cfg_mischief__information_help"]="Wer auf diesem Server Informations-Cheats nutzen darf: Nobody, Admins, Listed (Admins und gelistete Spieler) oder Everyone. Im Einzelspielermodus und als Host immer erlaubt. Information: deckt Verborgenes auf (ESP, Karte aufdecken, interessante Orte). Diese Servereinstellung wird von Crucible verwaltet; ihr Wert gilt für beitretende Clients.",
                ["cfg_mischief__travel_name"]="Reisen",
                ["cfg_mischief__travel_help"]="Wer auf diesem Server Reise-Cheats nutzen darf: Nobody, Admins, Listed (Admins und gelistete Spieler) oder Everyone. Im Einzelspielermodus und als Host immer erlaubt. Reisen: bewegt Spieler (Teleportieren, Portale). Diese Servereinstellung wird von Crucible verwaltet; ihr Wert gilt für beitretende Clients.",
                ["cfg_mischief__progress_name"]="Fortschritt",
                ["cfg_mischief__progress_help"]="Wer auf diesem Server Fortschritts-Cheats nutzen darf: Nobody, Admins, Listed (Admins und gelistete Spieler) oder Everyone. Im Einzelspielermodus und als Host immer erlaubt. Fortschritt: Fertigkeiten, Bosskräfte, Errungenschaften, garantierte Aufwertungen. Diese Servereinstellung wird von Crucible verwaltet; ihr Wert gilt für beitretende Clients.",
            },
            ["Russian"]=new Dictionary<string,string>(StringComparer.Ordinal){
                ["cfg_achievements_heading"]="Достижения",
                ["cfg_achievements__syncpaststats_name"]="Перенос прошлой статистики",
                ["cfg_achievements__syncpaststats_help"]="При загрузке персонажа, который не использовал читы, переносит в таблицу достижений статистику, которую игра записала, пока достижения не отслеживались. Статистика персонажей, использовавших читы, не переносится, если только Hex этого не разрешает. Отключено, пока установлен AchievementSync. Crucible управляет этой локальной настройкой установки.",
                ["cfg_achievements__resynckey_name"]="Клавиша повторной синхронизации",
                ["cfg_achievements__resynckey_help"]="Клавиша, которая заново проверяет статистику вашего сохранения и выдаёт уже заработанные достижения MeadWorks. По умолчанию не назначена. Crucible управляет этой локальной настройкой установки.",
                ["cfg_authority_heading"]="Полномочия",
                ["cfg_authority__vanillaservertimeoutseconds_name"]="Ожидание ванильного сервера (секунды)",
                ["cfg_authority__vanillaservertimeoutseconds_help"]="Сколько секунд клиент ждёт приветствия (Hello) Crucible от сервера, прежде чем считать сервер ванильным (читы заблокированы, обязательные продукты отключены). Crucible управляет этой локальной настройкой установки.",
                ["cfg_network_heading"]="Сеть",
                ["cfg_network__peerclasshandshake_name"]="Проверка версий при входе",
                ["cfg_network__peerclasshandshake_help"]="Проверяет версии MeadWorks при входе игрока: сервер отклоняет клиентов без обязательного мода MeadWorks или с другой версией major.minor, а клиент отключает обязательные моды, которых нет на сервере. Включено по умолчанию; отключайте только для диагностики проблем со входом. После изменения этой настройки перезапустите игру. Crucible управляет этой локальной настройкой установки.",
                ["cfg_logging_heading"]="Журнал",
                ["cfg_logging__level_name"]="Уровень",
                ["cfg_logging__level_help"]="Уровень журнала MeadWorks: Quiet (ошибки и предупреждения), Normal, Verbose или Debug. Также в консоли: 'meadworks log <level> [product]'. Crucible управляет этой локальной настройкой установки.",
                ["cfg_logging__productoverrides_name"]="Уровни по продуктам",
                ["cfg_logging__productoverrides_help"]="Уровни для отдельных продуктов, например 'Wildroot=Debug,Burrow=Normal'. Crucible управляет этой локальной настройкой установки.",
                ["cfg_logging__repeatwindowseconds_name"]="Окно повторов (секунды)",
                ["cfg_logging__repeatwindowseconds_help"]="Одинаковые строки в пределах этого окна записываются один раз с подсчётом; 0 записывает каждую строку. Crucible управляет этой локальной настройкой установки.",
                ["cfg_logging__consolefilterenabled_name"]="Фильтр консоли включён",
                ["cfg_logging__consolefilterenabled_help"]="Фильтрует консоль BepInEx для всех установленных модов по правилам ниже. Crucible управляет этой локальной настройкой установки.",
                ["cfg_logging__consolefilterrules_name"]="Правила фильтра консоли",
                ["cfg_logging__consolefilterrules_help"]="Уровни консоли по источникам: 'Source=All|Info|Warning|Error|None', через ';'. '*' задаёт уровень для источников вне списка. Crucible управляет этой локальной настройкой установки.",
                ["cfg_logging__consolefilterappliestodisklog_name"]="Фильтр консоли и для файла журнала",
                ["cfg_logging__consolefilterappliestodisklog_help"]="Применяет правила фильтра консоли также к LogOutput.log в BepInEx. Crucible управляет этой локальной настройкой установки.",
                ["cfg_logging__showallconsolemessages_name"]="Показывать все сообщения консоли",
                ["cfg_logging__showallconsolemessages_help"]="Показывает все сообщения в консоли BepInEx. По умолчанию Crucible скрывает короткий точный список известных безвредных сообщений игры и Unity (подсказки генерации мира, отсутствующие аудио- или видеошейдеры на выделенном сервере и т. п.); LogOutput.log всегда сохраняет всё. Crucible управляет этой локальной настройкой установки.",
                ["cfg_mischief_heading"]="Проказы",
                ["cfg_mischief__listedplayers_name"]="Игроки из списка",
                ["cfg_mischief__listedplayers_help"]="ID пользователей платформы, которым доступны категории читов Listed, через запятую (например Steam_76561198000000000; число без префикса означает Steam ID, как в adminlist.txt). Crucible управляет этой серверной настройкой; её значение применяется к подключающимся клиентам.",
                ["cfg_skills_heading"]="Навыки",
                ["cfg_skills__experiencemultiplier_name"]="Множитель опыта",
                ["cfg_skills__experiencemultiplier_help"]="Умножает опыт во всех собственных навыках MeadWorks (навыки продуктов, а не игры). В мультиплеере действует значение сервера. Crucible управляет этой серверной настройкой; её значение применяется к подключающимся клиентам.",
                ["cfg_mischief__personal_name"]="Личные",
                ["cfg_mischief__personal_help"]="Кто может использовать личные читы на этом сервере: Nobody, Admins, Listed (админы и игроки из списка) или Everyone. В одиночной игре и для хоста это всегда разрешено. Личные: влияют только на самого игрока (режим бога, скорость, размер). Crucible управляет этой серверной настройкой; её значение применяется к подключающимся клиентам.",
                ["cfg_mischief__economy_name"]="Экономика",
                ["cfg_mischief__economy_help"]="Кто может использовать экономические читы на этом сервере: Nobody, Admins, Listed (админы и игроки из списка) или Everyone. В одиночной игре и для хоста это всегда разрешено. Экономика: создают или экономят материалы (призыв предметов, скорость ремесла, множители добычи, бесплатное строительство). Crucible управляет этой серверной настройкой; её значение применяется к подключающимся клиентам.",
                ["cfg_mischief__world_name"]="Мир",
                ["cfg_mischief__world_help"]="Кто может использовать читы мира на этом сервере: Nobody, Admins, Listed (админы и игроки из списка) или Everyone. В одиночной игре и для хоста это всегда разрешено. Мир: меняют общий мир (погода, рост без ограничений). Crucible управляет этой серверной настройкой; её значение применяется к подключающимся клиентам.",
                ["cfg_mischief__information_name"]="Информация",
                ["cfg_mischief__information_help"]="Кто может использовать информационные читы на этом сервере: Nobody, Admins, Listed (админы и игроки из списка) или Everyone. В одиночной игре и для хоста это всегда разрешено. Информация: раскрывают скрытое (ESP, открытие карты, интересные места). Crucible управляет этой серверной настройкой; её значение применяется к подключающимся клиентам.",
                ["cfg_mischief__travel_name"]="Путешествия",
                ["cfg_mischief__travel_help"]="Кто может использовать читы путешествий на этом сервере: Nobody, Admins, Listed (админы и игроки из списка) или Everyone. В одиночной игре и для хоста это всегда разрешено. Путешествия: перемещают игроков (телепортация, порталы). Crucible управляет этой серверной настройкой; её значение применяется к подключающимся клиентам.",
                ["cfg_mischief__progress_name"]="Прогресс",
                ["cfg_mischief__progress_help"]="Кто может использовать читы прогресса на этом сервере: Nobody, Admins, Listed (админы и игроки из списка) или Everyone. В одиночной игре и для хоста это всегда разрешено. Прогресс: навыки, силы боссов, достижения, гарантированные улучшения. Crucible управляет этой серверной настройкой; её значение применяется к подключающимся клиентам.",
            },
            ["Chinese"]=new Dictionary<string,string>(StringComparer.Ordinal){
                ["cfg_achievements_heading"]="成就",
                ["cfg_achievements__syncpaststats_name"]="同步过往统计",
                ["cfg_achievements__syncpaststats_help"]="加载未作弊的角色时，将游戏在未追踪成就期间记录的统计数据复制到成就表中（使用过作弊的角色永不复制，除非 Hex 允许）。安装 AchievementSync 时关闭。这是由 Crucible 管理的本地安装设置。",
                ["cfg_achievements__resynckey_name"]="重新同步按键",
                ["cfg_achievements__resynckey_help"]="重新检查存档统计数据并授予已达成的 MeadWorks 成就的按键。默认未分配。这是由 Crucible 管理的本地安装设置。",
                ["cfg_authority_heading"]="权限",
                ["cfg_authority__vanillaservertimeoutseconds_name"]="原版服务器等待时间（秒）",
                ["cfg_authority__vanillaservertimeoutseconds_help"]="客户端等待服务器发送 Crucible Hello 的秒数，超时后将服务器视为原版（作弊锁定，必需产品关闭）。这是由 Crucible 管理的本地安装设置。",
                ["cfg_network_heading"]="网络",
                ["cfg_network__peerclasshandshake_name"]="加入时版本检查",
                ["cfg_network__peerclasshandshake_help"]="玩家加入时检查 MeadWorks 版本：服务器拒绝缺少必需 MeadWorks 模组或主.次版本号不同的客户端，客户端会关闭服务器上没有的必需模组。默认开启；仅在诊断加入问题时关闭。更改此设置后需重启游戏。这是由 Crucible 管理的本地安装设置。",
                ["cfg_logging_heading"]="日志",
                ["cfg_logging__level_name"]="级别",
                ["cfg_logging__level_help"]="MeadWorks 日志级别：Quiet（错误和警告）、Normal、Verbose 或 Debug。也可在控制台使用：'meadworks log <level> [product]'。这是由 Crucible 管理的本地安装设置。",
                ["cfg_logging__productoverrides_name"]="按产品设置级别",
                ["cfg_logging__productoverrides_help"]="按产品设置的级别，例如 'Wildroot=Debug,Burrow=Normal'。这是由 Crucible 管理的本地安装设置。",
                ["cfg_logging__repeatwindowseconds_name"]="重复窗口（秒）",
                ["cfg_logging__repeatwindowseconds_help"]="此时间窗口内的相同日志行只写入一次并计数；0 表示每行都写入。这是由 Crucible 管理的本地安装设置。",
                ["cfg_logging__consolefilterenabled_name"]="启用控制台过滤",
                ["cfg_logging__consolefilterenabled_help"]="按下方规则过滤所有已安装模组在 BepInEx 控制台中的输出。这是由 Crucible 管理的本地安装设置。",
                ["cfg_logging__consolefilterrules_name"]="控制台过滤规则",
                ["cfg_logging__consolefilterrules_help"]="按来源设置控制台级别：'Source=All|Info|Warning|Error|None'，以 ';' 分隔。'*' 设置未列出来源的级别。这是由 Crucible 管理的本地安装设置。",
                ["cfg_logging__consolefilterappliestodisklog_name"]="控制台过滤同样作用于日志文件",
                ["cfg_logging__consolefilterappliestodisklog_help"]="同时将控制台过滤规则应用于 BepInEx 的 LogOutput.log。这是由 Crucible 管理的本地安装设置。",
                ["cfg_logging__showallconsolemessages_name"]="显示所有控制台消息",
                ["cfg_logging__showallconsolemessages_help"]="在 BepInEx 控制台显示所有消息。默认情况下，Crucible 会隐藏一份简短而精确的已知无害原版和 Unity 消息列表（世界生成提示、专用服务器上缺少的音频或视频着色器等）；LogOutput.log 始终保留全部内容。这是由 Crucible 管理的本地安装设置。",
                ["cfg_mischief_heading"]="恶作剧",
                ["cfg_mischief__listedplayers_name"]="名单玩家",
                ["cfg_mischief__listedplayers_help"]="授予 Listed 作弊类别的平台用户 ID，以逗号分隔（例如 Steam_76561198000000000；纯数字表示 Steam ID，与 adminlist.txt 相同）。这是由 Crucible 管理的服务器设置；其值应用于加入的客户端。",
                ["cfg_skills_heading"]="技能",
                ["cfg_skills__experiencemultiplier_name"]="经验倍率",
                ["cfg_skills__experiencemultiplier_help"]="倍增每项 MeadWorks 自定义技能的经验（各产品自身的技能，而非游戏技能）。多人游戏中采用服务器的数值。这是由 Crucible 管理的服务器设置；其值应用于加入的客户端。",
                ["cfg_mischief__personal_name"]="个人",
                ["cfg_mischief__personal_help"]="谁可以在此服务器上使用个人作弊：Nobody、Admins、Listed（管理员和名单玩家）或 Everyone。单人游戏玩家和主机始终可以使用。个人：仅影响使用者（无敌模式、速度、体型）。这是由 Crucible 管理的服务器设置；其值应用于加入的客户端。",
                ["cfg_mischief__economy_name"]="经济",
                ["cfg_mischief__economy_help"]="谁可以在此服务器上使用经济作弊：Nobody、Admins、Listed（管理员和名单玩家）或 Everyone。单人游戏玩家和主机始终可以使用。经济：生成或节省材料（生成物品、制作速度、战利品倍率、免费建造）。这是由 Crucible 管理的服务器设置；其值应用于加入的客户端。",
                ["cfg_mischief__world_name"]="世界",
                ["cfg_mischief__world_help"]="谁可以在此服务器上使用世界作弊：Nobody、Admins、Listed（管理员和名单玩家）或 Everyone。单人游戏玩家和主机始终可以使用。世界：改变共享世界（天气、无限制生长）。这是由 Crucible 管理的服务器设置；其值应用于加入的客户端。",
                ["cfg_mischief__information_name"]="信息",
                ["cfg_mischief__information_help"]="谁可以在此服务器上使用信息作弊：Nobody、Admins、Listed（管理员和名单玩家）或 Everyone。单人游戏玩家和主机始终可以使用。信息：揭示隐藏内容（透视、显示地图、兴趣点）。这是由 Crucible 管理的服务器设置；其值应用于加入的客户端。",
                ["cfg_mischief__travel_name"]="旅行",
                ["cfg_mischief__travel_help"]="谁可以在此服务器上使用旅行作弊：Nobody、Admins、Listed（管理员和名单玩家）或 Everyone。单人游戏玩家和主机始终可以使用。旅行：移动玩家（传送、传送门）。这是由 Crucible 管理的服务器设置；其值应用于加入的客户端。",
                ["cfg_mischief__progress_name"]="进度",
                ["cfg_mischief__progress_help"]="谁可以在此服务器上使用进度作弊：Nobody、Admins、Listed（管理员和名单玩家）或 Everyone。单人游戏玩家和主机始终可以使用。进度：技能、首领之力、成就、必定成功的升级。这是由 Crucible 管理的服务器设置；其值应用于加入的客户端。",
            },
            ["Korean"]=new Dictionary<string,string>(StringComparer.Ordinal){
                ["cfg_achievements_heading"]="도전과제",
                ["cfg_achievements__syncpaststats_name"]="과거 통계 동기화",
                ["cfg_achievements__syncpaststats_help"]="치트를 사용하지 않은 캐릭터를 불러오면, 도전과제를 추적하지 않던 동안 게임이 집계한 통계를 도전과제 표에 복사합니다. 치트를 사용한 캐릭터에는 Hex가 허용한 경우에만 적용됩니다. AchievementSync가 설치되어 있는 동안에는 비활성화됩니다. Crucible이 관리하는 로컬 설치 설정입니다.",
                ["cfg_achievements__resynckey_name"]="재동기화 키",
                ["cfg_achievements__resynckey_help"]="저장된 게임 통계를 다시 확인하고 이미 달성한 MeadWorks 도전과제를 지급하는 키입니다. 기본값은 할당 안 됨입니다. Crucible이 관리하는 로컬 설치 설정입니다.",
                ["cfg_authority_heading"]="권한",
                ["cfg_authority__vanillaservertimeoutseconds_name"]="바닐라 서버 대기 시간(초)",
                ["cfg_authority__vanillaservertimeoutseconds_help"]="클라이언트가 서버를 바닐라로 간주하기 전에 서버의 Crucible Hello를 기다리는 시간(초)입니다(치트 잠김, 필수 제품 꺼짐). Crucible이 관리하는 로컬 설치 설정입니다.",
                ["cfg_network_heading"]="네트워크",
                ["cfg_network__peerclasshandshake_name"]="접속 시 버전 확인",
                ["cfg_network__peerclasshandshake_help"]="플레이어가 접속할 때 MeadWorks 버전을 확인합니다. 서버는 필수 MeadWorks 모드가 없거나 major.minor 버전이 다른 클라이언트를 거부하고, 클라이언트는 서버에 없는 필수 모드를 끕니다. 기본값은 켜짐이며, 접속 문제를 진단할 때만 끄세요. 이 설정을 바꾼 후에는 게임을 다시 시작하세요. Crucible이 관리하는 로컬 설치 설정입니다.",
                ["cfg_logging_heading"]="로그",
                ["cfg_logging__level_name"]="수준",
                ["cfg_logging__level_help"]="MeadWorks 로그 수준: Quiet(오류 및 경고), Normal, Verbose 또는 Debug. 콘솔에서도 가능: 'meadworks log <level> [product]'. Crucible이 관리하는 로컬 설치 설정입니다.",
                ["cfg_logging__productoverrides_name"]="제품별 수준",
                ["cfg_logging__productoverrides_help"]="제품별 수준입니다. 예: 'Wildroot=Debug,Burrow=Normal'. Crucible이 관리하는 로컬 설치 설정입니다.",
                ["cfg_logging__repeatwindowseconds_name"]="반복 구간(초)",
                ["cfg_logging__repeatwindowseconds_help"]="이 구간 안의 동일한 줄은 한 번만 기록하고 횟수를 셉니다. 0이면 모든 줄을 기록합니다. Crucible이 관리하는 로컬 설치 설정입니다.",
                ["cfg_logging__consolefilterenabled_name"]="콘솔 필터 사용",
                ["cfg_logging__consolefilterenabled_help"]="아래 규칙에 따라 설치된 모든 모드의 BepInEx 콘솔 출력을 필터링합니다. Crucible이 관리하는 로컬 설치 설정입니다.",
                ["cfg_logging__consolefilterrules_name"]="콘솔 필터 규칙",
                ["cfg_logging__consolefilterrules_help"]="출처별 콘솔 수준: 'Source=All|Info|Warning|Error|None', ';'로 구분합니다. '*'는 목록에 없는 출처의 수준을 정합니다. Crucible이 관리하는 로컬 설치 설정입니다.",
                ["cfg_logging__consolefilterappliestodisklog_name"]="로그 파일에도 콘솔 필터 적용",
                ["cfg_logging__consolefilterappliestodisklog_help"]="콘솔 필터 규칙을 BepInEx의 LogOutput.log에도 적용합니다. Crucible이 관리하는 로컬 설치 설정입니다.",
                ["cfg_logging__showallconsolemessages_name"]="모든 콘솔 메시지 표시",
                ["cfg_logging__showallconsolemessages_help"]="BepInEx 콘솔에 모든 메시지를 표시합니다. 기본적으로 Crucible은 알려진 무해한 바닐라 및 Unity 메시지의 짧고 정확한 목록(월드 생성 힌트, 전용 서버에서 누락된 오디오 또는 비디오 셰이더 등)을 숨깁니다. LogOutput.log에는 항상 모든 내용이 남습니다. Crucible이 관리하는 로컬 설치 설정입니다.",
                ["cfg_mischief_heading"]="장난",
                ["cfg_mischief__listedplayers_name"]="등록된 플레이어",
                ["cfg_mischief__listedplayers_help"]="Listed 치트 범주를 허용할 플랫폼 사용자 ID, 쉼표로 구분합니다(예: Steam_76561198000000000. 접두사 없는 숫자는 adminlist.txt처럼 Steam ID를 뜻합니다). Crucible이 관리하는 서버 설정이며, 그 값은 접속하는 클라이언트에 적용됩니다.",
                ["cfg_skills_heading"]="기술",
                ["cfg_skills__experiencemultiplier_name"]="경험치 배율",
                ["cfg_skills__experiencemultiplier_help"]="모든 MeadWorks 사용자 지정 기술(게임 기술이 아닌 제품 자체 기술)의 경험치를 배수로 늘립니다. 멀티플레이에서는 서버 값이 적용됩니다. Crucible이 관리하는 서버 설정이며, 그 값은 접속하는 클라이언트에 적용됩니다.",
                ["cfg_mischief__personal_name"]="개인",
                ["cfg_mischief__personal_help"]="이 서버에서 개인 치트를 쓸 수 있는 사람: Nobody, Admins, Listed(관리자와 등록된 플레이어) 또는 Everyone. 싱글 플레이어와 호스트는 항상 사용할 수 있습니다. 개인: 사용자 본인에게만 영향(무적 모드, 속도, 크기). Crucible이 관리하는 서버 설정이며, 그 값은 접속하는 클라이언트에 적용됩니다.",
                ["cfg_mischief__economy_name"]="경제",
                ["cfg_mischief__economy_help"]="이 서버에서 경제 치트를 쓸 수 있는 사람: Nobody, Admins, Listed(관리자와 등록된 플레이어) 또는 Everyone. 싱글 플레이어와 호스트는 항상 사용할 수 있습니다. 경제: 재료를 만들거나 아낌(아이템 생성, 제작 속도, 전리품 배율, 무료 건설). Crucible이 관리하는 서버 설정이며, 그 값은 접속하는 클라이언트에 적용됩니다.",
                ["cfg_mischief__world_name"]="월드",
                ["cfg_mischief__world_help"]="이 서버에서 월드 치트를 쓸 수 있는 사람: Nobody, Admins, Listed(관리자와 등록된 플레이어) 또는 Everyone. 싱글 플레이어와 호스트는 항상 사용할 수 있습니다. 월드: 공유 월드를 바꿈(날씨, 제한 없는 성장). Crucible이 관리하는 서버 설정이며, 그 값은 접속하는 클라이언트에 적용됩니다.",
                ["cfg_mischief__information_name"]="정보",
                ["cfg_mischief__information_help"]="이 서버에서 정보 치트를 쓸 수 있는 사람: Nobody, Admins, Listed(관리자와 등록된 플레이어) 또는 Everyone. 싱글 플레이어와 호스트는 항상 사용할 수 있습니다. 정보: 숨겨진 것을 드러냄(ESP, 지도 공개, 관심 지점). Crucible이 관리하는 서버 설정이며, 그 값은 접속하는 클라이언트에 적용됩니다.",
                ["cfg_mischief__travel_name"]="이동",
                ["cfg_mischief__travel_help"]="이 서버에서 이동 치트를 쓸 수 있는 사람: Nobody, Admins, Listed(관리자와 등록된 플레이어) 또는 Everyone. 싱글 플레이어와 호스트는 항상 사용할 수 있습니다. 이동: 플레이어를 옮김(순간이동, 포탈). Crucible이 관리하는 서버 설정이며, 그 값은 접속하는 클라이언트에 적용됩니다.",
                ["cfg_mischief__progress_name"]="진행",
                ["cfg_mischief__progress_help"]="이 서버에서 진행 치트를 쓸 수 있는 사람: Nobody, Admins, Listed(관리자와 등록된 플레이어) 또는 Everyone. 싱글 플레이어와 호스트는 항상 사용할 수 있습니다. 진행: 기술, 보스 힘, 도전과제, 확정 강화. Crucible이 관리하는 서버 설정이며, 그 값은 접속하는 클라이언트에 적용됩니다.",
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
