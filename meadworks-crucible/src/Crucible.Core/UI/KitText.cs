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
            },
            // Language freeze: Spanish and French are complete; the other languages fall back to English. Enum values stay as written.
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
