using System;
using System.Collections.Generic;
using System.Globalization;

namespace Encuadre
{
    // Textos de la interfaz. Cada fila: clave y un texto por idioma, en el orden de Codes.
    // Por defecto se usa el idioma de Windows; si no está traducido, inglés.
    public static class Lang
    {
        public const string AppName = "Encuadre";
        public const string Version = "1.1";

        public static readonly string[] Codes = { "en", "es", "pt", "fr", "de", "it" };
        public static readonly string[] Names = { "English", "Español", "Português", "Français", "Deutsch", "Italiano" };
        public const string Auto = "auto";

        static int current;
        static Dictionary<string, string[]> table;

        public static string Current { get { return Codes[current]; } }

        public static string Detect() { return Detect(CultureInfo.CurrentUICulture); }

        public static string Detect(CultureInfo culture)
        {
            for (var c = culture; c != null && c.Name.Length > 0; c = c.Parent)
            {
                int i = Array.IndexOf(Codes, c.TwoLetterISOLanguageName);
                if (i >= 0) return Codes[i];
            }
            return "en";
        }

        // setting: "auto" o un código de Codes
        public static void Set(string setting)
        {
            int i = Array.IndexOf(Codes, setting);
            if (i < 0) i = Array.IndexOf(Codes, Detect());
            current = Math.Max(0, i);
        }

        public static string T(string key)
        {
            if (table == null)
            {
                var t = new Dictionary<string, string[]>();
                foreach (var r in Rows) t[r[0]] = r;
                table = t;
            }
            string[] row;
            if (!table.TryGetValue(key, out row)) return key;
            string s = current + 1 < row.Length ? row[current + 1] : null;
            return string.IsNullOrEmpty(s) ? row[1] : s;
        }

        public static string F(string key, params object[] args) { return string.Format(T(key), args); }

        public static string[][] AllRows { get { return Rows; } }

        static readonly string[][] Rows =
        {
            //        clave                 en                                  es                                    pt                                     fr                                         de                                        it
            // ---- acciones ----
            new[] { "LeftHalf",          "Left Half",                        "Mitad izquierda",                    "Metade esquerda",                     "Moitié gauche",                           "Linke Hälfte",                           "Metà sinistra" },
            new[] { "RightHalf",         "Right Half",                       "Mitad derecha",                      "Metade direita",                      "Moitié droite",                           "Rechte Hälfte",                          "Metà destra" },
            new[] { "CenterHalf",        "Center Half",                      "Mitad central",                      "Metade central",                      "Moitié centrale",                         "Mittlere Hälfte",                        "Metà centrale" },
            new[] { "TopHalf",           "Top Half",                         "Mitad superior",                     "Metade superior",                     "Moitié supérieure",                       "Obere Hälfte",                           "Metà superiore" },
            new[] { "BottomHalf",        "Bottom Half",                      "Mitad inferior",                     "Metade inferior",                     "Moitié inférieure",                       "Untere Hälfte",                          "Metà inferiore" },
            new[] { "TopLeft",           "Top Left",                         "Arriba a la izquierda",              "Superior esquerdo",                   "En haut à gauche",                        "Oben links",                             "In alto a sinistra" },
            new[] { "TopRight",          "Top Right",                        "Arriba a la derecha",                "Superior direito",                    "En haut à droite",                        "Oben rechts",                            "In alto a destra" },
            new[] { "BottomLeft",        "Bottom Left",                      "Abajo a la izquierda",               "Inferior esquerdo",                   "En bas à gauche",                         "Unten links",                            "In basso a sinistra" },
            new[] { "BottomRight",       "Bottom Right",                     "Abajo a la derecha",                 "Inferior direito",                    "En bas à droite",                         "Unten rechts",                           "In basso a destra" },
            new[] { "FirstThird",        "First Third",                      "Primer tercio",                      "Primeiro terço",                      "Premier tiers",                           "Erstes Drittel",                         "Primo terzo" },
            new[] { "CenterThird",       "Center Third",                     "Tercio central",                     "Terço central",                       "Tiers central",                           "Mittleres Drittel",                      "Terzo centrale" },
            new[] { "LastThird",         "Last Third",                       "Último tercio",                      "Último terço",                        "Dernier tiers",                           "Letztes Drittel",                        "Ultimo terzo" },
            new[] { "FirstTwoThirds",    "First Two Thirds",                 "Primeros dos tercios",               "Primeiros dois terços",               "Deux premiers tiers",                     "Erste zwei Drittel",                     "Primi due terzi" },
            new[] { "CenterTwoThirds",   "Center Two Thirds",                "Dos tercios centrales",              "Dois terços centrais",                "Deux tiers centraux",                     "Mittlere zwei Drittel",                  "Due terzi centrali" },
            new[] { "LastTwoThirds",     "Last Two Thirds",                  "Últimos dos tercios",                "Últimos dois terços",                 "Deux derniers tiers",                     "Letzte zwei Drittel",                    "Ultimi due terzi" },
            new[] { "Maximize",          "Maximize",                         "Maximizar",                          "Maximizar",                           "Agrandir",                                "Maximieren",                             "Ingrandisci" },
            new[] { "AlmostMaximize",    "Almost Maximize",                  "Casi maximizar",                     "Quase maximizar",                     "Presque agrandir",                        "Fast maximieren",                        "Quasi ingrandisci" },
            new[] { "MaximizeHeight",    "Maximize Height",                  "Maximizar altura",                   "Maximizar altura",                    "Agrandir en hauteur",                     "Höhe maximieren",                        "Ingrandisci in altezza" },
            new[] { "Smaller",           "Make Smaller",                     "Más pequeña",                        "Menor",                               "Plus petite",                             "Kleiner",                                "Più piccola" },
            new[] { "Larger",            "Make Larger",                      "Más grande",                         "Maior",                               "Plus grande",                             "Größer",                                 "Più grande" },
            new[] { "Center",            "Center",                           "Centrar",                            "Centralizar",                         "Centrer",                                 "Zentrieren",                             "Centra" },
            new[] { "Restore",           "Restore",                          "Restaurar",                          "Restaurar",                           "Restaurer",                               "Wiederherstellen",                       "Ripristina" },
            new[] { "NextDisplay",       "Next Display",                     "Pantalla siguiente",                 "Próxima tela",                        "Écran suivant",                           "Nächster Bildschirm",                    "Schermo successivo" },
            new[] { "PreviousDisplay",   "Previous Display",                 "Pantalla anterior",                  "Tela anterior",                       "Écran précédent",                         "Vorheriger Bildschirm",                  "Schermo precedente" },
            new[] { "MoveLeft",          "Move Left",                        "Mover a la izquierda",               "Mover para a esquerda",               "Déplacer à gauche",                       "Nach links verschieben",                 "Sposta a sinistra" },
            new[] { "MoveRight",         "Move Right",                       "Mover a la derecha",                 "Mover para a direita",                "Déplacer à droite",                       "Nach rechts verschieben",                "Sposta a destra" },
            new[] { "MoveUp",            "Move Up",                          "Mover arriba",                       "Mover para cima",                     "Déplacer vers le haut",                   "Nach oben verschieben",                  "Sposta in alto" },
            new[] { "MoveDown",          "Move Down",                        "Mover abajo",                        "Mover para baixo",                    "Déplacer vers le bas",                    "Nach unten verschieben",                 "Sposta in basso" },
            new[] { "FirstFourth",       "First Fourth",                     "Primer cuarto",                      "Primeiro quarto",                     "Premier quart",                           "Erstes Viertel",                         "Primo quarto" },
            new[] { "SecondFourth",      "Second Fourth",                    "Segundo cuarto",                     "Segundo quarto",                      "Deuxième quart",                          "Zweites Viertel",                        "Secondo quarto" },
            new[] { "ThirdFourth",       "Third Fourth",                     "Tercer cuarto",                      "Terceiro quarto",                     "Troisième quart",                         "Drittes Viertel",                        "Terzo quarto" },
            new[] { "LastFourth",        "Last Fourth",                      "Último cuarto",                      "Último quarto",                       "Dernier quart",                           "Letztes Viertel",                        "Ultimo quarto" },
            new[] { "FirstThreeFourths", "First Three Fourths",              "Primeros tres cuartos",              "Primeiros três quartos",              "Trois premiers quarts",                   "Erste drei Viertel",                     "Primi tre quarti" },
            new[] { "LastThreeFourths",  "Last Three Fourths",               "Últimos tres cuartos",               "Últimos três quartos",                "Trois derniers quarts",                   "Letzte drei Viertel",                    "Ultimi tre quarti" },
            new[] { "TopLeftSixth",      "Top Left Sixth",                   "Sexto superior izquierdo",           "Sexto superior esquerdo",             "Sixième en haut à gauche",                "Sechstel oben links",                    "Sesto in alto a sinistra" },
            new[] { "TopCenterSixth",    "Top Center Sixth",                 "Sexto superior central",             "Sexto superior central",              "Sixième en haut au centre",               "Sechstel oben Mitte",                    "Sesto in alto al centro" },
            new[] { "TopRightSixth",     "Top Right Sixth",                  "Sexto superior derecho",             "Sexto superior direito",              "Sixième en haut à droite",                "Sechstel oben rechts",                   "Sesto in alto a destra" },
            new[] { "BottomLeftSixth",   "Bottom Left Sixth",                "Sexto inferior izquierdo",           "Sexto inferior esquerdo",             "Sixième en bas à gauche",                 "Sechstel unten links",                   "Sesto in basso a sinistra" },
            new[] { "BottomCenterSixth", "Bottom Center Sixth",              "Sexto inferior central",             "Sexto inferior central",              "Sixième en bas au centre",                "Sechstel unten Mitte",                   "Sesto in basso al centro" },
            new[] { "BottomRightSixth",  "Bottom Right Sixth",               "Sexto inferior derecho",             "Sexto inferior direito",              "Sixième en bas à droite",                 "Sechstel unten rechts",                  "Sesto in basso a destra" },

            // ---- nombres de teclas ----
            new[] { "KeyCtrl",           "Ctrl",                             "Ctrl",                               "Ctrl",                                "Ctrl",                                    "Strg",                                   "Ctrl" },
            new[] { "KeyShift",          "Shift",                            "Mayús",                              "Shift",                               "Maj",                                     "Umschalt",                               "Maiusc" },
            new[] { "KeyEnter",          "Enter",                            "Intro",                              "Enter",                               "Entrée",                                  "Eingabe",                                "Invio" },
            new[] { "KeyBackspace",      "Backspace",                        "Retroceso",                          "Backspace",                           "Retour arrière",                          "Rücktaste",                              "Backspace" },
            new[] { "KeyDelete",         "Del",                              "Supr",                               "Del",                                 "Suppr",                                   "Entf",                                   "Canc" },
            new[] { "KeyInsert",         "Ins",                              "Insert",                             "Ins",                                 "Inser",                                   "Einfg",                                  "Ins" },
            new[] { "KeySpace",          "Space",                            "Espacio",                            "Espaço",                              "Espace",                                  "Leertaste",                              "Spazio" },
            new[] { "KeyHome",           "Home",                             "Inicio",                             "Home",                                "Origine",                                 "Pos1",                                   "Home" },
            new[] { "KeyEnd",            "End",                              "Fin",                                "End",                                 "Fin",                                     "Ende",                                   "Fine" },
            new[] { "KeyPageUp",         "PgUp",                             "RePág",                              "PgUp",                                "Pg préc",                                 "Bild↑",                                  "Pag↑" },
            new[] { "KeyPageDown",       "PgDn",                             "AvPág",                              "PgDn",                                "Pg suiv",                                 "Bild↓",                                  "Pag↓" },

            // ---- áreas de ajuste ----
            new[] { "SnapNone",          "None",                             "Ninguna",                            "Nenhuma",                             "Aucune",                                  "Keine",                                  "Nessuna" },
            new[] { "SnapLeftCompound",  "Left Half + Top/Bottom",           "Mitad izq. + sup./inf.",             "Metade esq. + sup./inf.",             "Moitié gauche + haut/bas",                "Linke Hälfte + oben/unten",              "Metà sin. + sup./inf." },
            new[] { "SnapRightCompound", "Right Half + Top/Bottom",          "Mitad der. + sup./inf.",             "Metade dir. + sup./inf.",             "Moitié droite + haut/bas",                "Rechte Hälfte + oben/unten",             "Metà des. + sup./inf." },
            new[] { "SnapThirds",        "Thirds",                           "Tercios",                            "Terços",                              "Tiers",                                   "Drittel",                                "Terzi" },

            // ---- menú de la bandeja ----
            new[] { "MenuFourths",       "Fourths",                          "Cuartos",                            "Quartos",                             "Quarts",                                  "Viertel",                                "Quarti" },
            new[] { "MenuSixths",        "Sixths",                           "Sextos",                             "Sextos",                              "Sixièmes",                                "Sechstel",                               "Sesti" },
            new[] { "MenuMove",          "Move",                             "Mover",                              "Mover",                               "Déplacer",                                "Verschieben",                            "Sposta" },
            new[] { "MenuIgnore",        "Ignore app",                       "Ignorar app",                        "Ignorar app",                         "Ignorer l'app",                           "App ignorieren",                         "Ignora app" },
            new[] { "MenuIgnoreApp",     "Ignore “{0}”",                     "Ignorar «{0}»",                      "Ignorar “{0}”",                       "Ignorer « {0} »",                         "„{0}“ ignorieren",                       "Ignora «{0}»" },
            new[] { "MenuUnignoreApp",   "Stop Ignoring “{0}”",              "Dejar de ignorar «{0}»",             "Deixar de ignorar “{0}”",             "Ne plus ignorer « {0} »",                 "„{0}“ nicht mehr ignorieren",            "Non ignorare più «{0}»" },
            new[] { "ThisApp",           "this app",                         "esta app",                           "este app",                            "cette app",                               "diese App",                              "questa app" },
            new[] { "MenuPrefs",         "Preferences…",                     "Preferencias…",                      "Preferências…",                       "Préférences…",                            "Einstellungen…",                         "Preferenze…" },
            new[] { "Quit",              "Quit {0}",                         "Salir de {0}",                       "Sair do {0}",                         "Quitter {0}",                             "{0} beenden",                            "Esci da {0}" },
            new[] { "BalloonTitle",      "{0} is running",                   "{0} está funcionando",               "O {0} está em execução",              "{0} est lancé",                           "{0} läuft",                              "{0} è in esecuzione" },
            new[] { "BalloonText",
                "Win+Alt+← / → halves · Win+Alt+Enter maximize · or drag a window to a screen edge. Click the icon to see all actions.",
                "Win+Alt+← / → mitades · Win+Alt+Intro maximizar · o arrastra una ventana a un borde. Haz clic en el icono para ver todas las acciones.",
                "Win+Alt+← / → metades · Win+Alt+Enter maximizar · ou arraste uma janela até uma borda. Clique no ícone para ver todas as ações.",
                "Win+Alt+← / → moitiés · Win+Alt+Entrée agrandir · ou faites glisser une fenêtre vers un bord. Cliquez sur l'icône pour voir toutes les actions.",
                "Win+Alt+← / → Hälften · Win+Alt+Eingabe maximieren · oder ein Fenster an einen Rand ziehen. Klicke auf das Symbol, um alle Aktionen zu sehen.",
                "Win+Alt+← / → metà · Win+Alt+Invio ingrandisci · oppure trascina una finestra su un bordo. Fai clic sull'icona per vedere tutte le azioni." },

            // ---- preferencias ----
            new[] { "TabShortcuts",      "Keyboard Shortcuts",               "Atajos de teclado",                  "Atalhos de teclado",                  "Raccourcis clavier",                      "Tastenkürzel",                           "Scorciatoie" },
            new[] { "TabSnap",           "Snap Areas",                       "Áreas de ajuste",                    "Áreas de encaixe",                    "Zones d'ancrage",                         "Andockbereiche",                         "Aree di ancoraggio" },
            new[] { "TabSettings",       "Settings",                         "Ajustes",                            "Configurações",                       "Réglages",                                "Einstellungen",                          "Impostazioni" },
            new[] { "RecordPrompt",      "Press shortcut…",                  "Pulsa el atajo…",                    "Pressione o atalho…",                 "Appuyez sur le raccourci…",               "Kürzel drücken…",                        "Premi la scorciatoia…" },
            new[] { "RecordEmpty",       "Record Shortcut",                  "Grabar atajo",                       "Gravar atalho",                       "Enregistrer",                             "Kürzel aufnehmen",                       "Registra scorciatoia" },
            new[] { "ShortcutHint",
                "Click a shortcut and press the new key combination. Backspace clears it, Esc cancels. AltGr never counts as Ctrl, so characters like € and @ keep working.",
                "Haz clic en un atajo y pulsa la nueva combinación. Retroceso lo borra, Esc cancela. AltGr nunca cuenta como Ctrl, así que € y @ siguen funcionando.",
                "Clique num atalho e pressione a nova combinação. Backspace apaga, Esc cancela. AltGr nunca conta como Ctrl, então caracteres como € e @ continuam funcionando.",
                "Cliquez sur un raccourci et appuyez sur la nouvelle combinaison. Retour arrière l'efface, Échap annule. AltGr ne compte jamais comme Ctrl : € et @ fonctionnent toujours.",
                "Klicke auf ein Kürzel und drücke die neue Tastenkombination. Rücktaste löscht es, Esc bricht ab. AltGr zählt nie als Strg, daher funktionieren € und @ weiterhin.",
                "Fai clic su una scorciatoia e premi la nuova combinazione. Backspace la cancella, Esc annulla. AltGr non conta mai come Ctrl, quindi € e @ continuano a funzionare." },
            new[] { "SnapEnable",
                "Snap windows by dragging them to the edges and corners of the screen",
                "Ajustar ventanas arrastrándolas a los bordes y esquinas de la pantalla",
                "Encaixar janelas arrastando-as para as bordas e cantos da tela",
                "Ancrer les fenêtres en les faisant glisser vers les bords et les coins de l'écran",
                "Fenster durch Ziehen an die Ränder und Ecken des Bildschirms andocken",
                "Ancorare le finestre trascinandole ai bordi e agli angoli dello schermo" },
            new[] { "SnapRestore",
                "Restore the original size when a window is dragged out of an area",
                "Recuperar el tamaño original al sacar la ventana de un área",
                "Restaurar o tamanho original ao tirar a janela de uma área",
                "Rétablir la taille d'origine quand la fenêtre sort d'une zone",
                "Ursprüngliche Größe wiederherstellen, wenn das Fenster aus einem Bereich gezogen wird",
                "Ripristinare la dimensione originale quando la finestra esce da un'area" },
            new[] { "SnapAnimate",       "Animate the preview",              "Animar la vista previa",             "Animar a pré-visualização",           "Animer l'aperçu",                         "Vorschau animieren",                     "Animare l'anteprima" },
            new[] { "WinSnapTip",
                "Windows has its own Snap windows feature, which also reacts when you drag windows to the edges. To avoid two overlapping previews, turn it off in Settings › System › Multitasking.",
                "Windows tiene su propio «Ajustar ventanas», que también reacciona al arrastrar a los bordes. Para que no salgan dos vistas previas, desactívalo en Configuración › Sistema › Multitarea.",
                "O Windows tem o seu próprio “Ajustar janelas”, que também reage ao arrastar para as bordas. Para não aparecerem duas pré-visualizações, desative-o em Configurações › Sistema › Multitarefas.",
                "Windows a sa propre fonction « Ancrer les fenêtres », qui réagit aussi quand on fait glisser vers les bords. Pour éviter deux aperçus superposés, désactivez-la dans Paramètres › Système › Multitâche.",
                "Windows hat eine eigene Funktion „Fenster andocken“, die ebenfalls auf das Ziehen an die Ränder reagiert. Damit keine zwei Vorschauen erscheinen, deaktiviere sie unter Einstellungen › System › Multitasking.",
                "Windows ha una propria funzione «Ancoraggio finestre», che reagisce anche quando si trascina verso i bordi. Per evitare due anteprime sovrapposte, disattivala in Impostazioni › Sistema › Multitasking." },
            new[] { "WinSnapLink",       "Open Settings › Multitasking",     "Abrir Configuración › Multitarea",   "Abrir Configurações › Multitarefas",  "Ouvrir Paramètres › Multitâche",          "Einstellungen › Multitasking öffnen",    "Apri Impostazioni › Multitasking" },
            new[] { "LaunchAtLogin",     "Launch {0} at login",              "Abrir {0} al iniciar sesión en Windows", "Abrir o {0} ao entrar no Windows",  "Lancer {0} à l'ouverture de session",     "{0} bei der Anmeldung starten",          "Avvia {0} all'accesso a Windows" },
            new[] { "HideTray",
                "Hide the tray icon (open {0} again to see these preferences)",
                "Ocultar el icono de la bandeja (vuelve a abrir {0} para ver estas preferencias)",
                "Ocultar o ícone da bandeja (abra o {0} de novo para ver estas preferências)",
                "Masquer l'icône de la zone de notification (relancez {0} pour revoir ces préférences)",
                "Symbol im Infobereich ausblenden ({0} erneut öffnen, um diese Einstellungen zu sehen)",
                "Nascondi l'icona nell'area di notifica (riapri {0} per vedere queste preferenze)" },
            new[] { "Language",          "Language",                         "Idioma",                             "Idioma",                              "Langue",                                  "Sprache",                                "Lingua" },
            new[] { "LangAuto",          "Same as Windows",                  "Igual que Windows",                  "Igual ao Windows",                    "Comme Windows",                           "Wie Windows",                            "Come Windows" },
            new[] { "RepeatLabel",       "Repeated commands",                "Comandos repetidos",                 "Comandos repetidos",                  "Commandes répétées",                      "Wiederholte Befehle",                    "Comandi ripetuti" },
            new[] { "Repeat0",           "Cycle ½, ⅔ and ⅓ on halves",       "Alternar ½, ⅔ y ⅓ en las mitades",   "Alternar ½, ⅔ e ⅓ nas metades",       "Alterner ½, ⅔ et ⅓ sur les moitiés",      "Bei Hälften ½, ⅔ und ⅓ durchlaufen",     "Alternare ½, ⅔ e ⅓ sulle metà" },
            new[] { "Repeat1",           "Move to the adjacent display (left/right)", "Mover a la pantalla contigua (izq./der.)", "Mover para a tela vizinha (esq./dir.)", "Déplacer vers l'écran voisin (gauche/droite)", "Zum benachbarten Bildschirm (links/rechts)", "Sposta sullo schermo adiacente (sin./des.)" },
            new[] { "Repeat2",           "Do nothing",                       "No hacer nada",                      "Não fazer nada",                      "Ne rien faire",                           "Nichts tun",                             "Non fare nulla" },
            new[] { "GapLabel",          "Gap between windows",              "Espacio entre ventanas",             "Espaço entre janelas",                "Espace entre les fenêtres",               "Abstand zwischen Fenstern",              "Spazio tra le finestre" },
            new[] { "IgnoredLabel",      "Ignored apps",                     "Apps ignoradas",                     "Apps ignorados",                      "Apps ignorées",                           "Ignorierte Apps",                        "App ignorate" },
            new[] { "IgnoredHint",
                "{0} doesn't capture shortcuts in these apps. Add them from the tray menu.",
                "{0} no captura sus atajos. Añádelas desde el menú de la bandeja.",
                "O {0} não captura os atalhos nestes apps. Adicione-os pelo menu da bandeja.",
                "{0} ne capture pas les raccourcis dans ces apps. Ajoutez-les depuis le menu de l'icône.",
                "{0} fängt in diesen Apps keine Kürzel ab. Füge sie über das Menü im Infobereich hinzu.",
                "{0} non intercetta le scorciatoie in queste app. Aggiungile dal menu dell'icona." },
            new[] { "Unignore",          "Stop Ignoring",                    "Dejar de ignorar",                   "Deixar de ignorar",                   "Ne plus ignorer",                         "Nicht mehr ignorieren",                  "Non ignorare più" },
            new[] { "ResetKeys",         "Restore Default Shortcuts",        "Restaurar atajos por defecto",       "Restaurar atalhos padrão",            "Rétablir les raccourcis par défaut",      "Standardkürzel wiederherstellen",        "Ripristina scorciatoie predefinite" },
            new[] { "ResetSnap",         "Restore Default Areas",            "Restaurar áreas por defecto",        "Restaurar áreas padrão",              "Rétablir les zones par défaut",           "Standardbereiche wiederherstellen",      "Ripristina aree predefinite" },
            new[] { "About",
                "{0} {1} · window manager for Windows, inspired by Rectangle for macOS",
                "{0} {1} · gestor de ventanas para Windows, inspirado en Rectangle para macOS",
                "{0} {1} · gerenciador de janelas para Windows, inspirado no Rectangle para macOS",
                "{0} {1} · gestionnaire de fenêtres pour Windows, inspiré de Rectangle pour macOS",
                "{0} {1} · Fenstermanager für Windows, inspiriert von Rectangle für macOS",
                "{0} {1} · gestore di finestre per Windows, ispirato a Rectangle per macOS" },
        };
    }
}
