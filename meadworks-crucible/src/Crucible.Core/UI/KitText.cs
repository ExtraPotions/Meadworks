using System;
using System.Collections.Generic;
using System.Linq;
namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// The kit's own words (C08) in all seven Meadworks languages, served through the C05 service under the
    /// <c>crucible_ui</c> namespace so translators override them like any product text. English is authoritative; the
    /// other six are best-effort and await native-speaker review (task doc C08). PackPanel itself is English only.
    /// </summary>
    public static class KitText {
        public const string Product="crucible_ui";
        public const string Close="close";
        /// <summary>Tooltip topic over a disabled control that explains why (C03).</summary>
        public const string BlockedTopic="blocked_topic";
        public const string RefusalTitle="refusal_title";
        public const string RefusalIntro="refusal_intro";
        /// <summary>{0} product, {1} version.</summary>
        public const string RefusalInstall="refusal_install";
        /// <summary>{0} product, {1} the server's version.</summary>
        public const string RefusalUpdate="refusal_update";
        /// <summary>{0} product, {1} the server's version.</summary>
        public const string RefusalInstallOrAsk="refusal_install_or_ask";
        /// <summary>{0} the server's version, {1} the player's version or "not installed".</summary>
        public const string RefusalVersions="refusal_versions";
        public const string RefusalNotInstalled="refusal_not_installed";
        public const string RefusalFooter="refusal_footer";
        /// <summary>The breakdown remainder line (PackPanel <c>packpanel_tip_other</c>).</summary>
        public const string TipOther="tip_other";
        /// <summary>A breakdown with no named part (PackPanel <c>packpanel_tip_nothing</c>).</summary>
        public const string TipNothing="tip_nothing";
        /// <summary>A search field's placeholder (KX01, KX03).</summary>
        public const string Search="search";
        /// <summary>A pad player asked to type where no on-screen keyboard exists (KX01, Almanac AX16).</summary>
        public const string KeyboardUnavailable="keyboard_unavailable";
        /// <summary>The hint in the key-binding dialog.</summary>
        public const string KeyCapture="key_capture";
        /// <summary>The button that opens the long-text editor.</summary>
        public const string EditText="edit_text";
        /// <summary>A switch's default captions.</summary>
        public const string On="on";
        public const string Off="off";
        public const string MenuSettings="menu_settings";
        public const string MenuBack="menu_back";

        static readonly string[] KeyOrder={Close,BlockedTopic,RefusalTitle,RefusalIntro,RefusalInstall,RefusalUpdate,RefusalInstallOrAsk,RefusalVersions,RefusalNotInstalled,RefusalFooter,TipOther,TipNothing,Search,KeyboardUnavailable,KeyCapture,EditText,On,Off};
        /// <summary>Every kit key, the MeadWorks menu tips (<see cref="MeadworksMenuTips"/>) included.</summary>
        public static IReadOnlyList<string> Keys { get; }=KeyOrder.Concat(new[]{MenuSettings,MenuBack}).Concat(MeadworksMenuTips.Keys).ToArray();

        static readonly Dictionary<string,string[]> MenuRows=new Dictionary<string,string[]>{
            ["English"]=new[]{"Settings","Back"},["Korean"]=new[]{"설정","뒤로"},
            ["Spanish"]=new[]{"Ajustes","Volver"},["Chinese"]=new[]{"设置","返回"},
            ["Russian"]=new[]{"Настройки","Назад"},["French"]=new[]{"Paramètres","Retour"},["German"]=new[]{"Einstellungen","Zurück"}
        };

        // Columns follow KeyOrder.
        static readonly Dictionary<string,string[]> Rows=new Dictionary<string,string[]>(StringComparer.Ordinal){
            ["English"]=new[]{
                "Close","Unavailable","Join refused",
                "The server refused this connection because these mods do not match. Change them, then join again:",
                "Install {0} {1}.","Update {0} to {1}.","Install {0} {1}, or ask the host to update.",
                "Server {0}, you: {1}","not installed",
                "The server's log has a refusal code for this join; give it to the host if you need help.",
                "Other effects","Nothing you wear or carry gives this",
                "Search","No on-screen keyboard here; connect a keyboard to type.",
                "Press a key. Escape cancels; Backspace clears.","Edit text…","On","Off"},
            ["Korean"]=new[]{
                "닫기","사용할 수 없음","접속 거부됨",
                "다음 모드가 일치하지 않아 서버가 접속을 거부했습니다. 변경한 뒤 다시 접속하세요:",
                "{0} {1}을(를) 설치하세요.","{0}을(를) {1}(으)로 업데이트하세요.","{0} {1}을(를) 설치하거나 호스트에게 업데이트를 요청하세요.",
                "서버 {0}, 내 버전: {1}","설치 안 됨",
                "서버 로그에 이번 접속의 거부 코드가 있습니다. 도움이 필요하면 호스트에게 전달하세요.",
                "기타 효과","착용하거나 소지한 것 중 이 효과를 주는 것이 없습니다",
                "검색","화상 키보드를 사용할 수 없습니다. 입력하려면 키보드를 연결하세요.",
                "키를 누르세요. Esc는 취소, Backspace는 지우기입니다.","텍스트 편집…","켜짐","꺼짐"},
            ["Spanish"]=new[]{
                "Cerrar","No disponible","Conexión rechazada",
                "El servidor rechazó esta conexión porque estos mods no coinciden. Cámbialos y vuelve a entrar:",
                "Instala {0} {1}.","Actualiza {0} a {1}.","Instala {0} {1} o pide al anfitrión que actualice.",
                "Servidor {0}, tú: {1}","no instalado",
                "El registro del servidor tiene un código de rechazo para esta conexión; dáselo al anfitrión si necesitas ayuda.",
                "Otros efectos","Nada de lo que llevas te da esto",
                "Buscar","No hay teclado en pantalla; conecta un teclado para escribir.",
                "Pulsa una tecla. Escape cancela; Retroceso borra.","Editar texto…","Sí","No"},
            ["Chinese"]=new[]{
                "关闭","不可用","加入被拒绝",
                "由于以下模组不匹配，服务器拒绝了此连接。请调整后重新加入：",
                "安装 {0} {1}。","将 {0} 更新到 {1}。","安装 {0} {1}，或请主机更新。",
                "服务器 {0}，你：{1}","未安装",
                "服务器日志中有本次加入的拒绝代码；如需帮助，请提供给主机。",
                "其他效果","你穿戴或携带的物品都不提供此项",
                "搜索","此处没有屏幕键盘；请连接键盘输入。",
                "请按一个键。Esc 取消；Backspace 清除。","编辑文本…","开","关"},
            ["Russian"]=new[]{
                "Закрыть","Недоступно","Вход отклонён",
                "Сервер отклонил подключение: эти моды не совпадают. Измените их и подключитесь снова:",
                "Установите {0} {1}.","Обновите {0} до {1}.","Установите {0} {1} или попросите хоста обновиться.",
                "Сервер {0}, у вас: {1}","не установлен",
                "В журнале сервера есть код отказа для этого подключения; передайте его хосту, если нужна помощь.",
                "Другие эффекты","Ничто из надетого или носимого не даёт этого",
                "Поиск","Экранная клавиатура недоступна; подключите клавиатуру для ввода.",
                "Нажмите клавишу. Escape отменяет, Backspace очищает.","Изменить текст…","Вкл","Выкл"},
            ["French"]=new[]{
                "Fermer","Indisponible","Connexion refusée",
                "Le serveur a refusé cette connexion car ces mods ne correspondent pas. Modifiez-les, puis reconnectez-vous :",
                "Installez {0} {1}.","Mettez à jour {0} vers {1}.","Installez {0} {1}, ou demandez à l'hôte de mettre à jour.",
                "Serveur {0}, vous : {1}","non installé",
                "Le journal du serveur contient un code de refus pour cette connexion ; donnez-le à l'hôte si vous avez besoin d'aide.",
                "Autres effets","Rien de ce que vous portez ne donne cela",
                "Rechercher","Aucun clavier à l'écran ici ; branchez un clavier pour écrire.",
                "Appuyez sur une touche. Échap annule ; Retour arrière efface.","Modifier le texte…","Oui","Non"},
            ["German"]=new[]{
                "Schließen","Nicht verfügbar","Beitritt abgelehnt",
                "Der Server hat diese Verbindung abgelehnt, weil diese Mods nicht übereinstimmen. Ändere sie und tritt erneut bei:",
                "Installiere {0} {1}.","Aktualisiere {0} auf {1}.","Installiere {0} {1} oder bitte den Host, zu aktualisieren.",
                "Server {0}, du: {1}","nicht installiert",
                "Das Serverprotokoll enthält einen Ablehnungscode für diesen Beitritt; gib ihn dem Host, wenn du Hilfe brauchst.",
                "Weitere Effekte","Nichts, was du trägst, gibt dies",
                "Suchen","Keine Bildschirmtastatur verfügbar; schließe eine Tastatur an, um zu tippen.",
                "Drücke eine Taste. Escape bricht ab, Rücktaste löscht.","Text bearbeiten…","An","Aus"},
        };

        // Setting names, help and headings of Crucible.UI's own settings (SettingTextKeys); English equals the bound text.
        static readonly Dictionary<string,Dictionary<string,string>> SettingRows=new Dictionary<string,Dictionary<string,string>>(StringComparer.Ordinal){
            ["English"]=new Dictionary<string,string>(StringComparer.Ordinal){
                ["cfg_look_heading"]="Look",
                ["cfg_look__styled_panels_name"]="Styled Panels",
                ["cfg_look__styled_panels_help"]="The brown framed look of Meadworks panels, their cells and buttons. Off: the game's wood.",
                ["cfg_look__panel_theme_name"]="Panel Theme",
                ["cfg_look__panel_theme_help"]="Timber: shared warm wallpaper with softly chopped, beveled wood edges. Brown: the original brown artwork. Styled Panels off restores the game's panels. Cosmetic, local to this player.",
                ["cfg_look__timber_border_width_name"]="Timber Border Width",
                ["cfg_look__timber_border_width_help"]="How thick the wooden edge of Meadworks panels is.",
                ["cfg_look__timber_border_jaggedness_name"]="Timber Border Jaggedness",
                ["cfg_look__timber_border_jaggedness_help"]="How roughly the wooden edge of Meadworks panels is cut.",
                ["cfg_look__night_shade_name"]="Night Shade",
                ["cfg_look__night_shade_help"]="How dark Meadworks panels get at night, as the game's own wooden panels darken with the light around you (dark weather shades them too). 1 keeps them as bright as day; 0.65 at midnight in clear weather. Gray only, never the weather's color. Cells never change.",
                ["cfg_look__theme_game_panels_name"]="Theme Game Panels",
                ["cfg_look__theme_game_panels_help"]="Dress the game's own wood panels (crafting, character dialogs, settings) in the Timber look. Auto: when a Meadworks product that lays out the inventory asks for it. Always or Never override.",
                ["cfg_look__meadworks_map_frames_name"]="Meadworks Map Frames",
                ["cfg_look__meadworks_map_frames_help"]="Draw Meadworks wood-and-bronze frames over the game's minimap and large map. The map itself (terrain, pins, fog) is the game's own and stays untouched. Off: the game's maps exactly as they were. Cosmetic, local to this player.",
                ["cfg_look__meadworks_screen_layout_name"]="Meadworks Screen Layout",
                ["cfg_look__meadworks_screen_layout_help"]="Lay the inventory screen out in named regions: the grid at the top left, the equipment panel beside it, the character header and the crafting panel in a column at the right, and a dock for product panels under the equipment panel. Off: the game's own placement. Local to this player.",
                ["cfg_general_heading"]="General",
                ["cfg_general__meadworks_menu_key_name"]="Meadworks Menu Key",
                ["cfg_general__meadworks_menu_key_help"]="Opens the Meadworks menu (the window list of Esc > Meadworks) from anywhere in game, except while chat, the console or a text field has the keys. Empty: no key; Esc > Meadworks still works.",
                ["cfg_notices_heading"]="Notices",
                ["cfg_notices__stacked_notices_name"]="Stacked Notices",
                ["cfg_notices__stacked_notices_help"]="Show MeadWorks notices in a stack. Off uses the game's own message. Local to this player, drawn by Crucible UI, and the server does not override it. Takes effect at once.",
                ["cfg_notices__position_name"]="Position",
                ["cfg_notices__position_help"]="Corner of the screen for the MeadWorks notice stack: TopLeft, TopRight, BottomLeft or BottomRight. The stack stays clear of the compass and the status effect row. Local to this player. Takes effect at once.",
                ["cfg_notices__duration_name"]="Duration",
                ["cfg_notices__duration_help"]="How long each MeadWorks notice stays, in seconds. The same notice again restarts this time and raises its count. Local to this player. Applies to the next notice.",
                ["cfg_notices__maximum_visible_name"]="Maximum Visible",
                ["cfg_notices__maximum_visible_help"]="How many MeadWorks notices show at once. The newest sits at the top. Further notices wait until a space opens. Local to this player. Takes effect at once.",
            },
            // Every language is complete. Enum values stay as written.
            ["Spanish"]=new Dictionary<string,string>(StringComparer.Ordinal){
                ["cfg_look_heading"]="Aspecto",
                ["cfg_look__styled_panels_name"]="Paneles con estilo",
                ["cfg_look__styled_panels_help"]="El aspecto marrón enmarcado de los paneles de Meadworks, sus celdas y botones. Desactivado: la madera del juego.",
                ["cfg_look__panel_theme_name"]="Tema de los paneles",
                ["cfg_look__panel_theme_help"]="Timber: fondo cálido compartido con bordes de madera biselados y suavemente tallados. Brown: el arte marrón original. Con Paneles con estilo desactivado vuelven los paneles del juego. Estético, solo para este jugador.",
                ["cfg_look__timber_border_width_name"]="Grosor del borde Timber",
                ["cfg_look__timber_border_width_help"]="Grosor del borde de madera de los paneles de Meadworks.",
                ["cfg_look__timber_border_jaggedness_name"]="Irregularidad del borde Timber",
                ["cfg_look__timber_border_jaggedness_help"]="Lo toscamente tallado que está el borde de madera de los paneles de Meadworks.",
                ["cfg_look__night_shade_name"]="Sombra nocturna",
                ["cfg_look__night_shade_help"]="Cuánto se oscurecen de noche los paneles de Meadworks, igual que los paneles de madera del juego se oscurecen con la luz que te rodea (el mal tiempo también los oscurece). 1 los mantiene tan claros como de día; 0.65 a medianoche con cielo despejado. Solo gris, nunca el color del clima. Las celdas nunca cambian.",
                ["cfg_look__theme_game_panels_name"]="Tematizar paneles del juego",
                ["cfg_look__theme_game_panels_help"]="Viste los paneles de madera del propio juego (fabricación, diálogos del personaje, ajustes) con el aspecto Timber. Auto: cuando lo pide un producto de Meadworks que organiza el inventario. Always o Never lo fuerzan.",
                ["cfg_look__meadworks_map_frames_name"]="Marcos de mapa de Meadworks",
                ["cfg_look__meadworks_map_frames_help"]="Dibuja marcos de madera y bronce de Meadworks sobre el minimapa y el mapa grande del juego. El mapa en sí (terreno, marcadores, niebla) es del juego y no se toca. Desactivado: los mapas del juego tal como eran. Estético, solo para este jugador.",
                ["cfg_look__meadworks_screen_layout_name"]="Disposición de pantalla de Meadworks",
                ["cfg_look__meadworks_screen_layout_help"]="Organiza la pantalla de inventario en zonas con nombre: la cuadrícula arriba a la izquierda, el panel de equipo a su lado, la cabecera del personaje y el panel de fabricación en una columna a la derecha, y un espacio para paneles de productos bajo el panel de equipo. Desactivado: la colocación propia del juego. Solo para este jugador.",
                ["cfg_general_heading"]="General",
                ["cfg_general__meadworks_menu_key_name"]="Tecla del menú de Meadworks",
                ["cfg_general__meadworks_menu_key_help"]="Abre el menú de Meadworks (la lista de ventanas de Esc > Meadworks) desde cualquier lugar del juego, salvo mientras el chat, la consola o un campo de texto usan el teclado. Vacío: sin tecla; Esc > Meadworks sigue funcionando.",
                ["cfg_notices_heading"]="Avisos",
                ["cfg_notices__stacked_notices_name"]="Avisos apilados",
                ["cfg_notices__stacked_notices_help"]="Muestra los avisos de MeadWorks en una pila. Desactivado: el mensaje del propio juego. Solo para este jugador, lo dibuja Crucible UI y el servidor no lo cambia. Tiene efecto al momento.",
                ["cfg_notices__position_name"]="Posición",
                ["cfg_notices__position_help"]="Esquina de la pantalla para la pila de avisos de MeadWorks: TopLeft, TopRight, BottomLeft o BottomRight. La pila no tapa la brújula ni la fila de efectos de estado. Solo para este jugador. Tiene efecto al momento.",
                ["cfg_notices__duration_name"]="Duración",
                ["cfg_notices__duration_help"]="Cuánto permanece cada aviso de MeadWorks, en segundos. El mismo aviso otra vez reinicia este tiempo y aumenta su cuenta. Solo para este jugador. Se aplica al aviso siguiente.",
                ["cfg_notices__maximum_visible_name"]="Máximo visible",
                ["cfg_notices__maximum_visible_help"]="Cuántos avisos de MeadWorks se muestran a la vez. El más reciente queda arriba. Los demás esperan hasta que haya sitio. Solo para este jugador. Tiene efecto al momento.",
            },
            ["French"]=new Dictionary<string,string>(StringComparer.Ordinal){
                ["cfg_look_heading"]="Apparence",
                ["cfg_look__styled_panels_name"]="Panneaux stylisés",
                ["cfg_look__styled_panels_help"]="L'aspect brun encadré des panneaux Meadworks, de leurs cases et de leurs boutons. Désactivé : le bois du jeu.",
                ["cfg_look__panel_theme_name"]="Thème des panneaux",
                ["cfg_look__panel_theme_help"]="Timber : fond chaleureux partagé avec des bords en bois biseautés et doucement taillés. Brown : l'illustration brune d'origine. Panneaux stylisés désactivé rétablit les panneaux du jeu. Cosmétique, local à ce joueur.",
                ["cfg_look__timber_border_width_name"]="Épaisseur de la bordure Timber",
                ["cfg_look__timber_border_width_help"]="Épaisseur du bord en bois des panneaux Meadworks.",
                ["cfg_look__timber_border_jaggedness_name"]="Irrégularité de la bordure Timber",
                ["cfg_look__timber_border_jaggedness_help"]="À quel point le bord en bois des panneaux Meadworks est taillé grossièrement.",
                ["cfg_look__night_shade_name"]="Ombre nocturne",
                ["cfg_look__night_shade_help"]="À quel point les panneaux Meadworks s'assombrissent la nuit, comme les panneaux en bois du jeu s'assombrissent avec la lumière autour de vous (le mauvais temps les assombrit aussi). 1 les garde aussi clairs qu'en plein jour ; 0.65 à minuit par temps clair. Gris uniquement, jamais la couleur de la météo. Les cases ne changent jamais.",
                ["cfg_look__theme_game_panels_name"]="Thème des panneaux du jeu",
                ["cfg_look__theme_game_panels_help"]="Habille les panneaux en bois du jeu (artisanat, dialogues du personnage, paramètres) de l'aspect Timber. Auto : quand un produit Meadworks qui organise l'inventaire le demande. Always ou Never forcent le choix.",
                ["cfg_look__meadworks_map_frames_name"]="Cadres de carte Meadworks",
                ["cfg_look__meadworks_map_frames_help"]="Dessine des cadres Meadworks en bois et bronze sur la minicarte et la grande carte du jeu. La carte elle-même (terrain, repères, brouillard) appartient au jeu et reste intacte. Désactivé : les cartes du jeu exactement comme avant. Cosmétique, local à ce joueur.",
                ["cfg_look__meadworks_screen_layout_name"]="Disposition d'écran Meadworks",
                ["cfg_look__meadworks_screen_layout_help"]="Organise l'écran d'inventaire en zones nommées : la grille en haut à gauche, le panneau d'équipement à côté, l'en-tête du personnage et le panneau d'artisanat dans une colonne à droite, et un emplacement pour les panneaux de produits sous le panneau d'équipement. Désactivé : le placement d'origine du jeu. Local à ce joueur.",
                ["cfg_general_heading"]="Général",
                ["cfg_general__meadworks_menu_key_name"]="Touche du menu Meadworks",
                ["cfg_general__meadworks_menu_key_help"]="Ouvre le menu Meadworks (la liste des fenêtres de Échap > Meadworks) n'importe où en jeu, sauf quand le chat, la console ou un champ de texte utilise le clavier. Vide : aucune touche ; Échap > Meadworks fonctionne toujours.",
                ["cfg_notices_heading"]="Avis",
                ["cfg_notices__stacked_notices_name"]="Avis empilés",
                ["cfg_notices__stacked_notices_help"]="Affiche les avis MeadWorks en pile. Désactivé : le message du jeu. Local à ce joueur, dessiné par Crucible UI, et le serveur ne le remplace pas. Prend effet tout de suite.",
                ["cfg_notices__position_name"]="Position",
                ["cfg_notices__position_help"]="Coin de l'écran pour la pile d'avis MeadWorks : TopLeft, TopRight, BottomLeft ou BottomRight. La pile reste à l'écart de la boussole et de la rangée d'effets de statut. Local à ce joueur. Prend effet tout de suite.",
                ["cfg_notices__duration_name"]="Durée",
                ["cfg_notices__duration_help"]="Combien de temps chaque avis MeadWorks reste, en secondes. Le même avis relance ce temps et augmente son compte. Local à ce joueur. S'applique à l'avis suivant.",
                ["cfg_notices__maximum_visible_name"]="Maximum visible",
                ["cfg_notices__maximum_visible_help"]="Combien d'avis MeadWorks s'affichent à la fois. Le plus récent est en haut. Les suivants attendent qu'une place se libère. Local à ce joueur. Prend effet tout de suite.",
            },
            ["German"]=new Dictionary<string,string>(StringComparer.Ordinal){
                ["cfg_look_heading"]="Aussehen",
                ["cfg_look__styled_panels_name"]="Gestaltete Fenster",
                ["cfg_look__styled_panels_help"]="Der braune, gerahmte Look der Meadworks-Fenster, ihrer Felder und Schaltflächen. Aus: das Holz des Spiels.",
                ["cfg_look__panel_theme_name"]="Fensterdesign",
                ["cfg_look__panel_theme_help"]="Timber: gemeinsame warme Tapete mit sanft gehackten, abgeschrägten Holzkanten. Brown: die ursprüngliche braune Grafik. Mit Gestaltete Fenster aus kehren die Fenster des Spiels zurück. Rein optisch, nur für diesen Spieler.",
                ["cfg_look__timber_border_width_name"]="Timber-Randbreite",
                ["cfg_look__timber_border_width_help"]="Wie dick der Holzrand der Meadworks-Fenster ist.",
                ["cfg_look__timber_border_jaggedness_name"]="Timber-Randzacken",
                ["cfg_look__timber_border_jaggedness_help"]="Wie grob der Holzrand der Meadworks-Fenster geschnitten ist.",
                ["cfg_look__night_shade_name"]="Nachtschatten",
                ["cfg_look__night_shade_help"]="Wie dunkel Meadworks-Fenster nachts werden, so wie die Holzfenster des Spiels mit dem Licht um dich herum dunkler werden (dunkles Wetter dunkelt sie ebenfalls ab). 1 hält sie so hell wie am Tag; 0.65 um Mitternacht bei klarem Wetter. Nur Grau, nie die Farbe des Wetters. Felder ändern sich nie.",
                ["cfg_look__theme_game_panels_name"]="Spielfenster gestalten",
                ["cfg_look__theme_game_panels_help"]="Kleidet die eigenen Holzfenster des Spiels (Herstellung, Charakterdialoge, Einstellungen) im Timber-Look ein. Auto: wenn ein Meadworks-Produkt, das das Inventar anordnet, es anfordert. Always oder Never erzwingen es.",
                ["cfg_look__meadworks_map_frames_name"]="Meadworks-Kartenrahmen",
                ["cfg_look__meadworks_map_frames_help"]="Zeichnet Meadworks-Rahmen aus Holz und Bronze über die Minikarte und die große Karte des Spiels. Die Karte selbst (Gelände, Markierungen, Nebel) gehört zum Spiel und bleibt unberührt. Aus: die Karten des Spiels genau wie zuvor. Rein optisch, nur für diesen Spieler.",
                ["cfg_look__meadworks_screen_layout_name"]="Meadworks-Bildschirmaufteilung",
                ["cfg_look__meadworks_screen_layout_help"]="Teilt den Inventarbildschirm in benannte Bereiche auf: das Raster oben links, das Ausrüstungsfenster daneben, die Charakterkopfzeile und das Herstellungsfenster in einer Spalte rechts sowie ein Dock für Produktfenster unter dem Ausrüstungsfenster. Aus: die eigene Anordnung des Spiels. Nur für diesen Spieler.",
                ["cfg_general_heading"]="Allgemein",
                ["cfg_general__meadworks_menu_key_name"]="Meadworks-Menütaste",
                ["cfg_general__meadworks_menu_key_help"]="Öffnet das Meadworks-Menü (die Fensterliste unter Esc > Meadworks) überall im Spiel, außer während Chat, Konsole oder ein Textfeld die Tasten belegt. Leer: keine Taste; Esc > Meadworks funktioniert weiterhin.",
                ["cfg_notices_heading"]="Hinweise",
                ["cfg_notices__stacked_notices_name"]="Gestapelte Hinweise",
                ["cfg_notices__stacked_notices_help"]="Zeigt MeadWorks-Hinweise als Stapel. Aus: die eigene Meldung des Spiels. Nur für diesen Spieler, gezeichnet von Crucible UI, und der Server setzt es nicht außer Kraft. Wirkt sofort.",
                ["cfg_notices__position_name"]="Position",
                ["cfg_notices__position_help"]="Ecke des Bildschirms für den MeadWorks-Hinweisstapel: TopLeft, TopRight, BottomLeft oder BottomRight. Der Stapel bleibt frei von Kompass und Statuseffekt-Reihe. Nur für diesen Spieler. Wirkt sofort.",
                ["cfg_notices__duration_name"]="Dauer",
                ["cfg_notices__duration_help"]="Wie lange jeder MeadWorks-Hinweis bleibt, in Sekunden. Derselbe Hinweis startet diese Zeit neu und erhöht seine Zahl. Nur für diesen Spieler. Gilt für den nächsten Hinweis.",
                ["cfg_notices__maximum_visible_name"]="Maximal sichtbar",
                ["cfg_notices__maximum_visible_help"]="Wie viele MeadWorks-Hinweise zugleich zu sehen sind. Der neueste steht oben. Weitere warten, bis Platz frei wird. Nur für diesen Spieler. Wirkt sofort.",
            },
            ["Russian"]=new Dictionary<string,string>(StringComparer.Ordinal){
                ["cfg_look_heading"]="Внешний вид",
                ["cfg_look__styled_panels_name"]="Оформленные панели",
                ["cfg_look__styled_panels_help"]="Коричневый вид с рамками для панелей Meadworks, их ячеек и кнопок. Выкл.: дерево игры.",
                ["cfg_look__panel_theme_name"]="Тема панелей",
                ["cfg_look__panel_theme_help"]="Timber: общий тёплый фон с мягко отёсанными скошенными деревянными краями. Brown: исходное коричневое оформление. Если «Оформленные панели» выключены, возвращаются панели игры. Только внешний вид, локально для этого игрока.",
                ["cfg_look__timber_border_width_name"]="Ширина рамки Timber",
                ["cfg_look__timber_border_width_help"]="Толщина деревянного края панелей Meadworks.",
                ["cfg_look__timber_border_jaggedness_name"]="Неровность рамки Timber",
                ["cfg_look__timber_border_jaggedness_help"]="Насколько грубо обтёсан деревянный край панелей Meadworks.",
                ["cfg_look__night_shade_name"]="Ночное затемнение",
                ["cfg_look__night_shade_help"]="Насколько темнеют панели Meadworks ночью, как деревянные панели самой игры темнеют вместе со светом вокруг вас (тёмная погода тоже их затемняет). 1 оставляет их яркими, как днём; 0.65 в полночь при ясной погоде. Только серый, никогда цвет погоды. Ячейки не меняются.",
                ["cfg_look__theme_game_panels_name"]="Оформлять панели игры",
                ["cfg_look__theme_game_panels_help"]="Оформляет собственные деревянные панели игры (ремесло, окна персонажа, настройки) в стиле Timber. Auto: когда этого просит продукт Meadworks, который раскладывает инвентарь. Always или Never задают принудительно.",
                ["cfg_look__meadworks_map_frames_name"]="Рамки карты Meadworks",
                ["cfg_look__meadworks_map_frames_help"]="Рисует рамки Meadworks из дерева и бронзы поверх мини-карты и большой карты игры. Сама карта (местность, метки, туман) принадлежит игре и не меняется. Выкл.: карты игры в точности как были. Только внешний вид, локально для этого игрока.",
                ["cfg_look__meadworks_screen_layout_name"]="Раскладка экрана Meadworks",
                ["cfg_look__meadworks_screen_layout_help"]="Делит экран инвентаря на именованные области: сетка вверху слева, панель снаряжения рядом с ней, заголовок персонажа и панель ремесла в колонке справа, а под панелью снаряжения — док для панелей продуктов. Выкл.: собственное расположение игры. Локально для этого игрока.",
                ["cfg_general_heading"]="Общие",
                ["cfg_general__meadworks_menu_key_name"]="Клавиша меню Meadworks",
                ["cfg_general__meadworks_menu_key_help"]="Открывает меню Meadworks (список окон из Esc > Meadworks) в любом месте игры, кроме моментов, когда клавиши заняты чатом, консолью или текстовым полем. Пусто: без клавиши; Esc > Meadworks продолжает работать.",
                ["cfg_notices_heading"]="Уведомления",
                ["cfg_notices__stacked_notices_name"]="Стопка уведомлений",
                ["cfg_notices__stacked_notices_help"]="Показывает уведомления MeadWorks стопкой. Выкл.: сообщение самой игры. Только для этого игрока, рисует Crucible UI, сервер это не переопределяет. Действует сразу.",
                ["cfg_notices__position_name"]="Положение",
                ["cfg_notices__position_help"]="Угол экрана для стопки уведомлений MeadWorks: TopLeft, TopRight, BottomLeft или BottomRight. Стопка не закрывает компас и ряд эффектов состояния. Только для этого игрока. Действует сразу.",
                ["cfg_notices__duration_name"]="Длительность",
                ["cfg_notices__duration_help"]="Сколько секунд держится каждое уведомление MeadWorks. То же уведомление снова запускает это время и увеличивает счёт. Только для этого игрока. Применяется к следующему уведомлению.",
                ["cfg_notices__maximum_visible_name"]="Максимум на экране",
                ["cfg_notices__maximum_visible_help"]="Сколько уведомлений MeadWorks видно сразу. Новое сверху. Остальные ждут свободного места. Только для этого игрока. Действует сразу.",
            },
            ["Chinese"]=new Dictionary<string,string>(StringComparer.Ordinal){
                ["cfg_look_heading"]="外观",
                ["cfg_look__styled_panels_name"]="风格化面板",
                ["cfg_look__styled_panels_help"]="Meadworks 面板及其格子和按钮的棕色边框外观。关闭：使用游戏的木质外观。",
                ["cfg_look__panel_theme_name"]="面板主题",
                ["cfg_look__panel_theme_help"]="Timber：共享的温暖底纹，带有轻微劈砍感的斜面木质边缘。Brown：原始的棕色美术。关闭“风格化面板”会恢复游戏的面板。仅影响外观，仅对此玩家生效。",
                ["cfg_look__timber_border_width_name"]="Timber 边框宽度",
                ["cfg_look__timber_border_width_help"]="Meadworks 面板木质边缘的厚度。",
                ["cfg_look__timber_border_jaggedness_name"]="Timber 边框粗糙度",
                ["cfg_look__timber_border_jaggedness_help"]="Meadworks 面板木质边缘的切割粗糙程度。",
                ["cfg_look__night_shade_name"]="夜间暗化",
                ["cfg_look__night_shade_help"]="Meadworks 面板在夜间变暗的程度，就像游戏自身的木质面板会随周围光线变暗一样（阴暗天气也会使其变暗）。1 表示保持白天的亮度；晴朗天气午夜时为 0.65。仅为灰色，绝不使用天气的颜色。格子永不改变。",
                ["cfg_look__theme_game_panels_name"]="主题化游戏面板",
                ["cfg_look__theme_game_panels_help"]="用 Timber 外观装饰游戏自身的木质面板（制作、角色对话框、设置）。Auto：当排布物品栏的 Meadworks 产品请求时启用。Always 或 Never 强制指定。",
                ["cfg_look__meadworks_map_frames_name"]="Meadworks 地图边框",
                ["cfg_look__meadworks_map_frames_help"]="在游戏的小地图和大地图上绘制 Meadworks 木质和青铜边框。地图本身（地形、标记、迷雾）属于游戏且保持不变。关闭：游戏地图保持原样。仅影响外观，仅对此玩家生效。",
                ["cfg_look__meadworks_screen_layout_name"]="Meadworks 屏幕布局",
                ["cfg_look__meadworks_screen_layout_help"]="将物品栏界面划分为命名区域：网格位于左上，装备面板在其旁边，角色标题和制作面板在右侧一列，装备面板下方为产品面板的停靠区。关闭：使用游戏自身的布局。仅对此玩家生效。",
                ["cfg_general_heading"]="常规",
                ["cfg_general__meadworks_menu_key_name"]="Meadworks 菜单键",
                ["cfg_general__meadworks_menu_key_help"]="在游戏中任何位置打开 Meadworks 菜单（Esc > Meadworks 中的窗口列表），聊天、控制台或文本框占用按键时除外。留空：不设按键；Esc > Meadworks 仍然可用。",
                ["cfg_notices_heading"]="提示",
                ["cfg_notices__stacked_notices_name"]="堆叠提示",
                ["cfg_notices__stacked_notices_help"]="以堆叠方式显示 MeadWorks 提示。关闭：使用游戏自身的消息。仅对此玩家生效，由 Crucible UI 绘制，服务器不会覆盖。立即生效。",
                ["cfg_notices__position_name"]="位置",
                ["cfg_notices__position_help"]="MeadWorks 提示堆叠所在的屏幕角落：TopLeft、TopRight、BottomLeft 或 BottomRight。堆叠避开指南针和状态效果栏。仅对此玩家生效。立即生效。",
                ["cfg_notices__duration_name"]="持续时间",
                ["cfg_notices__duration_help"]="每条 MeadWorks 提示停留的秒数。相同提示再次出现会重新计时并增加计数。仅对此玩家生效。对下一条提示生效。",
                ["cfg_notices__maximum_visible_name"]="最多同时显示",
                ["cfg_notices__maximum_visible_help"]="同时显示多少条 MeadWorks 提示。最新的在最上方。多出的提示等待空位。仅对此玩家生效。立即生效。",
            },
            ["Korean"]=new Dictionary<string,string>(StringComparer.Ordinal){
                ["cfg_look_heading"]="외형",
                ["cfg_look__styled_panels_name"]="스타일 패널",
                ["cfg_look__styled_panels_help"]="Meadworks 패널과 그 칸, 버튼의 갈색 테두리 외형입니다. 끄면 게임의 나무 외형을 씁니다.",
                ["cfg_look__panel_theme_name"]="패널 테마",
                ["cfg_look__panel_theme_help"]="Timber: 부드럽게 깎인 경사진 나무 가장자리가 있는 따뜻한 공용 배경. Brown: 원래의 갈색 아트. 스타일 패널을 끄면 게임 패널로 돌아갑니다. 외형 전용이며 이 플레이어에게만 적용됩니다.",
                ["cfg_look__timber_border_width_name"]="Timber 테두리 두께",
                ["cfg_look__timber_border_width_help"]="Meadworks 패널 나무 가장자리의 두께입니다.",
                ["cfg_look__timber_border_jaggedness_name"]="Timber 테두리 거칠기",
                ["cfg_look__timber_border_jaggedness_help"]="Meadworks 패널 나무 가장자리가 얼마나 거칠게 깎였는지 정합니다.",
                ["cfg_look__night_shade_name"]="야간 어둡게",
                ["cfg_look__night_shade_help"]="게임 자체 나무 패널이 주변 빛에 따라 어두워지듯 Meadworks 패널이 밤에 어두워지는 정도입니다(어두운 날씨에도 어두워집니다). 1이면 낮처럼 밝게 유지하고, 맑은 날 자정에는 0.65입니다. 회색으로만 어두워지며 날씨 색은 쓰지 않습니다. 칸은 바뀌지 않습니다.",
                ["cfg_look__theme_game_panels_name"]="게임 패널 테마 적용",
                ["cfg_look__theme_game_panels_help"]="게임 자체 나무 패널(제작, 캐릭터 대화 상자, 설정)에 Timber 외형을 입힙니다. Auto: 인벤토리를 배치하는 Meadworks 제품이 요청할 때. Always 또는 Never로 강제합니다.",
                ["cfg_look__meadworks_map_frames_name"]="Meadworks 지도 테두리",
                ["cfg_look__meadworks_map_frames_help"]="게임의 미니맵과 큰 지도 위에 Meadworks 나무와 청동 테두리를 그립니다. 지도 자체(지형, 핀, 안개)는 게임의 것이며 건드리지 않습니다. 끄면 게임 지도가 원래 그대로입니다. 외형 전용이며 이 플레이어에게만 적용됩니다.",
                ["cfg_look__meadworks_screen_layout_name"]="Meadworks 화면 배치",
                ["cfg_look__meadworks_screen_layout_help"]="인벤토리 화면을 이름 붙은 영역으로 배치합니다: 왼쪽 위에 격자, 그 옆에 장비 패널, 오른쪽 열에 캐릭터 헤더와 제작 패널, 장비 패널 아래에 제품 패널용 도크. 끄면 게임 자체 배치를 씁니다. 이 플레이어에게만 적용됩니다.",
                ["cfg_general_heading"]="일반",
                ["cfg_general__meadworks_menu_key_name"]="Meadworks 메뉴 키",
                ["cfg_general__meadworks_menu_key_help"]="채팅, 콘솔 또는 텍스트 필드가 키 입력을 사용하는 동안을 제외하고 게임 어디서나 Meadworks 메뉴(Esc > Meadworks의 창 목록)를 엽니다. 비우면 키가 없으며, Esc > Meadworks는 계속 작동합니다.",
                ["cfg_notices_heading"]="알림",
                ["cfg_notices__stacked_notices_name"]="쌓인 알림",
                ["cfg_notices__stacked_notices_help"]="MeadWorks 알림을 쌓아서 보여 줍니다. 끄면 게임 자체의 메시지를 씁니다. 이 플레이어에게만 적용되며 Crucible UI가 그리고, 서버가 덮어쓰지 않습니다. 바로 적용됩니다.",
                ["cfg_notices__position_name"]="위치",
                ["cfg_notices__position_help"]="MeadWorks 알림 더미의 화면 모서리: TopLeft, TopRight, BottomLeft 또는 BottomRight. 더미는 나침반과 상태 효과 줄을 가리지 않습니다. 이 플레이어에게만 적용됩니다. 바로 적용됩니다.",
                ["cfg_notices__duration_name"]="지속 시간",
                ["cfg_notices__duration_help"]="각 MeadWorks 알림이 남는 시간(초)입니다. 같은 알림이 다시 오면 이 시간을 다시 시작하고 횟수가 늘어납니다. 이 플레이어에게만 적용됩니다. 다음 알림부터 적용됩니다.",
                ["cfg_notices__maximum_visible_name"]="최대 표시 수",
                ["cfg_notices__maximum_visible_help"]="한 번에 보이는 MeadWorks 알림 수입니다. 가장 최근 것이 위에 있습니다. 나머지는 자리가 날 때까지 기다립니다. 이 플레이어에게만 적용됩니다. 바로 적용됩니다.",
            },
        };

        /// <summary>Shared instance; Crucible.UI registers it with the C05 adapter so overrides apply here.</summary>
        public static ProductTranslations Default { get; }=Create();

        public static ProductTranslations Create(){
            var tables=new Dictionary<string,IReadOnlyDictionary<string,string>>(StringComparer.Ordinal);
            foreach(var row in Rows){
                var table=new Dictionary<string,string>(StringComparer.Ordinal);
                for(int i=0;i<KeyOrder.Length;i++)table.Add(KeyOrder[i],row.Value[i]);
                table.Add(MenuSettings,MenuRows[row.Key][0]);table.Add(MenuBack,MenuRows[row.Key][1]);
                MeadworksMenuTips.AddTo(row.Key,table);
                if(SettingRows.TryGetValue(row.Key,out var settings))foreach(var setting in settings)table.Add(setting.Key,setting.Value);
                tables.Add(row.Key,table);
            }
            return ProductTranslations.FromTables(Product,tables);
        }

        public static string Text(string key,string? language,ProductTranslations? translations=null)=>(translations??Default).Text(key,language);
        public static string Format(string key,string? language,ProductTranslations? translations,params object[] arguments)=>(translations??Default).Format(key,language,arguments);
    }

    public enum KitTabVisual { Lit, Dim }

    /// <summary>What a kit control shows: whether it takes clicks, whether it is dimmed, and the reason tooltip when blocked.</summary>
    public readonly struct KitControlDecision {
        public bool Interactable { get; }
        public bool Dimmed { get; }
        public string TooltipTopic { get; }
        public string Tooltip { get; }
        public KitControlDecision(bool interactable,bool dimmed,string topic,string tooltip){Interactable=interactable;Dimmed=dimmed;TooltipTopic=topic??"";Tooltip=tooltip??"";}
    }

    public static class KitControls {
        /// <summary>
        /// A control the product made <paramref name="usable"/> or not; a blocked option (C03) always disables it and
        /// explains why in its tooltip, in the player's language.
        /// </summary>
        public static KitControlDecision Decide(bool usable,BlockedOption? blocked,string? language,ProductTranslations? texts=null){
            if(blocked!=null&&!blocked.Allowed)return new KitControlDecision(false,true,KitText.Text(KitText.BlockedTopic,language,texts),blocked.Text(language));
            return new KitControlDecision(usable,!usable,"","");
        }

        /// <summary>A tab is lit while it is the shown one and can be used; otherwise dimmed.</summary>
        public static KitTabVisual Tab(bool shown,bool usable)=>shown&&usable?KitTabVisual.Lit:KitTabVisual.Dim;
        public static KitColor Colour(KitTabVisual visual)=>visual==KitTabVisual.Lit?KitPalette.TabLit:KitPalette.TabDim;
        public static KitColor TextColour(KitTabVisual visual)=>visual==KitTabVisual.Lit?KitPalette.TabLitText:KitPalette.TabDimText;
    }

    public enum RefusalAction { Install, Update, InstallOrAskHost }

    public sealed class RefusalRow {
        public string ProductId { get; }
        public string Product { get; }
        public string ServerVersion { get; }
        /// <summary>The player's version, or empty when the player does not have the product.</summary>
        public string ClientVersion { get; }
        public RefusalAction Action { get; }
        /// <summary>What to do, localized ("Install Wildroot 0.4.").</summary>
        public string Text { get; }
        /// <summary>Both versions, localized ("Server 0.4, you: not installed").</summary>
        public string Versions { get; }
        internal RefusalRow(string id,string product,string server,string client,RefusalAction action,string text,string versions){ProductId=id;Product=product;ServerVersion=server;ClientVersion=client;Action=action;Text=text;Versions=versions;}
    }

    /// <summary>
    /// The C04 refusal screen's content, built from the client's own verdict on the server's product set: a title, one
    /// row per refusing product with what to install or update, and a footer pointing at the refusal code in the
    /// server's log (the client does not know the code; the server makes it).
    /// </summary>
    public sealed class RefusalScreenModel {
        public string Title { get; }
        public string Intro { get; }
        public IReadOnlyList<RefusalRow> Rows { get; }
        public string Footer { get; }
        public string Close { get; }
        public bool Empty=>Rows.Count==0;

        RefusalScreenModel(string title,string intro,IReadOnlyList<RefusalRow> rows,string footer,string close){Title=title;Intro=intro;Rows=rows;Footer=footer;Close=close;}

        public static RefusalScreenModel From(PeerClassVerdict verdict,string? language,ProductTranslations? texts=null){
            if(verdict==null)throw new ArgumentNullException(nameof(verdict));
            var rows=new List<RefusalRow>();
            foreach(var result in verdict.Products.Where(p=>p.Refuses)){
                var server=result.Server?.VersionText??"";
                var client=result.Client?.VersionText??"";
                RefusalAction action;
                if(result.Client==null)action=RefusalAction.Install;
                else action=result.Client.Version!=null&&result.Server?.Version!=null&&result.Client.Version<result.Server.Version?RefusalAction.Update:RefusalAction.InstallOrAskHost;
                string key=action==RefusalAction.Install?KitText.RefusalInstall:action==RefusalAction.Update?KitText.RefusalUpdate:KitText.RefusalInstallOrAsk;
                var text=KitText.Format(key,language,texts,result.Name,server);
                var versions=KitText.Format(KitText.RefusalVersions,language,texts,server,client.Length>0?client:KitText.Text(KitText.RefusalNotInstalled,language,texts));
                rows.Add(new RefusalRow(result.Id,result.Name,server,client,action,text,versions));
            }
            return new RefusalScreenModel(
                KitText.Text(KitText.RefusalTitle,language,texts),
                KitText.Text(KitText.RefusalIntro,language,texts),
                rows.AsReadOnly(),
                KitText.Text(KitText.RefusalFooter,language,texts),
                KitText.Text(KitText.Close,language,texts));
        }
    }

    /// <summary>
    /// The client's last C04 refusal, kept until a screen shows it: a refusing verdict replaces any older one and is
    /// announced, an admitted verdict forgets it, and the screen takes it once.
    /// </summary>
    public sealed class RefusalInbox {
        readonly object gate=new object();
        PeerClassVerdict? pending;

        public event Action<PeerClassVerdict>? Refused;

        public PeerClassVerdict? Pending{get{lock(gate)return pending;}}

        public void Record(PeerClassVerdict verdict){
            if(verdict==null)throw new ArgumentNullException(nameof(verdict));
            lock(gate)pending=verdict.Admit?null:verdict;
            if(!verdict.Admit)Refused?.Invoke(verdict);
        }

        public PeerClassVerdict? Take(){lock(gate){var taken=pending;pending=null;return taken;}}

        public void Clear(){lock(gate)pending=null;}
    }
}
