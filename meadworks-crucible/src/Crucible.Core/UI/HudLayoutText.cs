// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the HUD layout editor's words in all seven Meadworks languages.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Meadworks.Crucible {
    /// <summary>
    /// The HUD layout editor's words, served with the kit's words (<see cref="KitText"/>, namespace <c>crucible_ui</c>) so
    /// translators override them like any kit text. English is authoritative; the other six languages are best-effort and
    /// await native-speaker review.
    /// 
    public static class HudLayoutText {
        public const string Title="hud_title",Previous="hud_previous",Next="hud_next",Show="hud_show",Hide="hud_hide",
            Smaller="hud_smaller",Larger="hud_larger",LessBackground="hud_less_background",MoreBackground="hud_more_background",
            ProductBackground="hud_product_background",MoveLeft="hud_move_left",MoveRight="hud_move_right",MoveUp="hud_move_up",
            MoveDown="hud_move_down",AttachNext="hud_attach_next",ChangeEdge="hud_change_edge",Independent="hud_independent",
            GapLess="hud_gap_less",GapMore="hud_gap_more",Undo="hud_undo",AutomaticLayout="hud_automatic_layout",
            Visible="hud_visible",HiddenByMod="hud_hidden_by_mod",Placed="hud_placed",AutomaticPosition="hud_automatic_position",
            // {0} the size factor or <see cref="ProductValue"/>.
            Size="hud_size",
            // {0} the background opacity or <see cref="ProductValue"/>.
            Background="hud_background",
            ProductValue="hud_product_value",
            // {0} the side phrase (<see cref="SideRight"/> and the others), {1} the gap in pixels.
            Attached="hud_attached",
            // {0} the panel attached to.
            SideRight="hud_side_right",SideBelow="hud_side_below",SideLeft="hud_side_left",SideAbove="hud_side_above",
            UnavailablePanel="hud_unavailable_panel",NoAttachment="hud_no_attachment",Help="hud_help",
            ResetFirst="hud_reset_first",NothingToUndo="hud_nothing_to_undo",PanelUnavailable="hud_panel_unavailable",
            // {0} the error message.
            StorageFailed="hud_storage_failed",
            Unsupported="hud_unsupported",StorageUnavailable="hud_storage_unavailable",
            // {0} the storage decision.
            NotSaved="hud_not_saved",
            Saved="hud_saved",
            // {0} the error message.
            SaveFailed="hud_save_failed";

        static readonly string[] KeyOrder={Title,Previous,Next,Show,Hide,Smaller,Larger,LessBackground,MoreBackground,ProductBackground,
            MoveLeft,MoveRight,MoveUp,MoveDown,AttachNext,ChangeEdge,Independent,GapLess,GapMore,Undo,AutomaticLayout,
            Visible,HiddenByMod,Placed,AutomaticPosition,Size,Background,ProductValue,Attached,SideRight,SideBelow,SideLeft,SideAbove,
            UnavailablePanel,NoAttachment,Help,ResetFirst,NothingToUndo,PanelUnavailable,StorageFailed,Unsupported,StorageUnavailable,
            NotSaved,Saved,SaveFailed};

        public static IReadOnlyList<string> Keys=>KeyOrder;

        /// <summary>The side phrase for an attachment side.
        public static string SideKey(HudAttachSide side)=>side switch{
            HudAttachSide.Right=>SideRight,HudAttachSide.Below=>SideBelow,HudAttachSide.Left=>SideLeft,_=>SideAbove};

        // Columns follow KeyOrder.
        internal static readonly Dictionary<string,string[]> Rows=new Dictionary<string,string[]>(StringComparer.Ordinal){
            ["English"]=new[]{
                "HUD layout","Previous panel","Next panel","Show panel","Hide panel","Smaller","Larger",
                "Less background","More background","Product background","Move left","Move right","Move up","Move down",
                "Attach to next","Change edge","Independent position","Gap −","Gap +","Undo","Automatic layout",
                "Panel is visible","Hidden by its mod; layout cannot enable it","Placed","Automatic position",
                "size {0}","background {0}","product","Attached {0} · gap {1}",
                "right of {0}","below {0}","left of {0}","above {0}",
                "an unavailable panel","No attachment",
                "Drag the selected panel background to move; drag its lower-right corner to resize. Alt + arrows nudges. Escape closes this editor and cancels a drag.",
                "Reset unsupported saved layout explicitly before editing.","Nothing to undo.","That panel is not currently available.",
                "HUD layout storage unavailable: {0}","Saved layout is unsupported; automatic layout is used. Reset explicitly to replace it.",
                "Layout storage is unavailable.","Layout was not saved: {0}","Saved locally.","Layout save failed: {0}"},
            ["Spanish"]=new[]{
                "Diseño de la interfaz","Panel anterior","Panel siguiente","Mostrar panel","Ocultar panel","Más pequeño","Más grande",
                "Menos fondo","Más fondo","Fondo del mod","Mover a la izquierda","Mover a la derecha","Mover arriba","Mover abajo",
                "Unir al siguiente","Cambiar borde","Posición independiente","Separación −","Separación +","Deshacer","Diseño automático",
                "El panel está visible","Oculto por su mod; el diseño no puede mostrarlo","Colocado","Posición automática",
                "tamaño {0}","fondo {0}","del mod","Unido {0} · separación {1}",
                "a la derecha de {0}","debajo de {0}","a la izquierda de {0}","encima de {0}",
                "un panel no disponible","Sin unión",
                "Arrastra el fondo del panel seleccionado para moverlo; arrastra su esquina inferior derecha para cambiar su tamaño. Alt + flechas lo desplaza poco a poco. Escape cierra este editor y cancela un arrastre.",
                "Restablece de forma explícita el diseño guardado no compatible antes de editar.","No hay nada que deshacer.","Ese panel no está disponible ahora.",
                "Almacenamiento del diseño de la interfaz no disponible: {0}","El diseño guardado no es compatible; se usa el diseño automático. Restablécelo de forma explícita para sustituirlo.",
                "El almacenamiento del diseño no está disponible.","El diseño no se guardó: {0}","Guardado localmente.","Error al guardar el diseño: {0}"},
            ["French"]=new[]{
                "Disposition de l'interface","Panneau précédent","Panneau suivant","Afficher le panneau","Masquer le panneau","Plus petit","Plus grand",
                "Moins de fond","Plus de fond","Fond du mod","Déplacer à gauche","Déplacer à droite","Déplacer vers le haut","Déplacer vers le bas",
                "Attacher au suivant","Changer de bord","Position indépendante","Écart −","Écart +","Annuler","Disposition automatique",
                "Le panneau est visible","Masqué par son mod ; la disposition ne peut pas l'afficher","Placé","Position automatique",
                "taille {0}","fond {0}","du mod","Attaché {0} · écart {1}",
                "à droite de {0}","sous {0}","à gauche de {0}","au-dessus de {0}",
                "un panneau indisponible","Aucune attache",
                "Faites glisser le fond du panneau sélectionné pour le déplacer ; faites glisser son coin inférieur droit pour le redimensionner. Alt + flèches le décale. Échap ferme cet éditeur et annule un glissement.",
                "Réinitialisez explicitement la disposition enregistrée non prise en charge avant de la modifier.","Rien à annuler.","Ce panneau n'est pas disponible pour le moment.",
                "Stockage de la disposition de l'interface indisponible : {0}","La disposition enregistrée n'est pas prise en charge ; la disposition automatique est utilisée. Réinitialisez explicitement pour la remplacer.",
                "Le stockage de la disposition est indisponible.","La disposition n'a pas été enregistrée : {0}","Enregistré localement.","Échec de l'enregistrement de la disposition : {0}"},
            ["German"]=new[]{
                "HUD-Anordnung","Vorheriges Feld","Nächstes Feld","Feld anzeigen","Feld ausblenden","Kleiner","Größer",
                "Weniger Hintergrund","Mehr Hintergrund","Hintergrund der Mod","Nach links","Nach rechts","Nach oben","Nach unten",
                "An nächstes anheften","Kante wechseln","Freie Position","Abstand −","Abstand +","Rückgängig","Automatische Anordnung",
                "Feld ist sichtbar","Von seiner Mod ausgeblendet; die Anordnung kann es nicht einblenden","Platziert","Automatische Position",
                "Größe {0}","Hintergrund {0}","Mod","Angeheftet {0} · Abstand {1}",
                "rechts von {0}","unter {0}","links von {0}","über {0}",
                "einem nicht verfügbaren Feld","Nicht angeheftet",
                "Ziehe den Hintergrund des gewählten Felds, um es zu verschieben; ziehe seine untere rechte Ecke, um die Größe zu ändern. Alt + Pfeiltasten verschiebt es schrittweise. Escape schließt diesen Editor und bricht ein Ziehen ab.",
                "Setze die nicht unterstützte gespeicherte Anordnung ausdrücklich zurück, bevor du sie bearbeitest.","Nichts rückgängig zu machen.","Dieses Feld ist gerade nicht verfügbar.",
                "Speicher der HUD-Anordnung nicht verfügbar: {0}","Die gespeicherte Anordnung wird nicht unterstützt; die automatische Anordnung wird verwendet. Setze sie ausdrücklich zurück, um sie zu ersetzen.",
                "Der Speicher der Anordnung ist nicht verfügbar.","Die Anordnung wurde nicht gespeichert: {0}","Lokal gespeichert.","Speichern der Anordnung fehlgeschlagen: {0}"},
            ["Russian"]=new[]{
                "Раскладка интерфейса","Предыдущая панель","Следующая панель","Показать панель","Скрыть панель","Меньше","Больше",
                "Меньше фона","Больше фона","Фон мода","Сдвинуть влево","Сдвинуть вправо","Сдвинуть вверх","Сдвинуть вниз",
                "Прикрепить к следующей","Сменить край","Свободное положение","Отступ −","Отступ +","Отменить","Автоматическая раскладка",
                "Панель видна","Скрыта своим модом; раскладка не может её показать","Размещена","Автоматическое положение",
                "размер {0}","фон {0}","как в моде","Прикреплена {0} · отступ {1}",
                "справа от {0}","под {0}","слева от {0}","над {0}",
                "недоступной панели","Не прикреплена",
                "Перетащите фон выбранной панели, чтобы переместить её; перетащите её правый нижний угол, чтобы изменить размер. Alt + стрелки сдвигают её понемногу. Escape закрывает редактор и отменяет перетаскивание.",
                "Перед изменением явно сбросьте неподдерживаемую сохранённую раскладку.","Нечего отменять.","Эта панель сейчас недоступна.",
                "Хранилище раскладки интерфейса недоступно: {0}","Сохранённая раскладка не поддерживается; используется автоматическая. Сбросьте её явно, чтобы заменить.",
                "Хранилище раскладки недоступно.","Раскладка не сохранена: {0}","Сохранено локально.","Не удалось сохранить раскладку: {0}"},
            ["Chinese"]=new[]{
                "界面布局","上一个面板","下一个面板","显示面板","隐藏面板","缩小","放大",
                "减少背景","增加背景","模组背景","左移","右移","上移","下移",
                "附着到下一个","更换边","独立位置","间距 −","间距 +","撤销","自动布局",
                "面板可见","已被其模组隐藏；布局无法显示它","已放置","自动位置",
                "大小 {0}","背景 {0}","模组默认","附着于{0} · 间距 {1}",
                "{0} 右侧","{0} 下方","{0} 左侧","{0} 上方",
                "不可用的面板","未附着",
                "拖动所选面板的背景以移动；拖动其右下角以调整大小。Alt + 方向键微调。Esc 关闭此编辑器并取消拖动。",
                "编辑前请先明确重置不受支持的已保存布局。","没有可撤销的操作。","该面板当前不可用。",
                "界面布局存储不可用：{0}","已保存的布局不受支持；正在使用自动布局。请明确重置以替换它。",
                "布局存储不可用。","布局未保存：{0}","已保存到本地。","布局保存失败：{0}"},
            ["Korean"]=new[]{
                "HUD 배치","이전 패널","다음 패널","패널 표시","패널 숨기기","작게","크게",
                "배경 줄이기","배경 늘리기","모드 기본 배경","왼쪽으로 이동","오른쪽으로 이동","위로 이동","아래로 이동",
                "다음 패널에 붙이기","가장자리 변경","독립 위치","간격 −","간격 +","실행 취소","자동 배치",
                "패널이 보입니다","해당 모드가 숨겼습니다. 배치에서 표시할 수 없습니다","배치됨","자동 위치",
                "크기 {0}","배경 {0}","모드 기본값","{0}에 붙음 · 간격 {1}",
                "{0}의 오른쪽","{0}의 아래","{0}의 왼쪽","{0}의 위",
                "사용할 수 없는 패널","붙지 않음",
                "선택한 패널의 배경을 끌어 이동하고, 오른쪽 아래 모서리를 끌어 크기를 바꿉니다. Alt + 방향키로 조금씩 옮깁니다. Esc는 이 편집기를 닫고 끌기를 취소합니다.",
                "편집하기 전에 지원되지 않는 저장된 배치를 직접 초기화하세요.","실행 취소할 것이 없습니다.","해당 패널은 지금 사용할 수 없습니다.",
                "HUD 배치 저장소를 사용할 수 없습니다: {0}","저장된 배치가 지원되지 않아 자동 배치를 사용합니다. 바꾸려면 직접 초기화하세요.",
                "배치 저장소를 사용할 수 없습니다.","배치가 저장되지 않았습니다: {0}","로컬에 저장했습니다.","배치 저장 실패: {0}"},
        };

        internal static void AddTo(string language,Dictionary<string,string> table){
            var row=Rows.TryGetValue(language,out var found)?found:Rows["English"];
            for(int i=0;i<KeyOrder.Length;i++)table.Add(KeyOrder[i],row[i]);
        }
    }
}
