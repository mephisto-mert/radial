using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RadialLauncher.Services.Themes;
using Serilog;

namespace RadialLauncher.Services.Localization
{
    public class LocalizationService : ILocalizationService
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RadialLauncher", "settings.json");

        private static LocalizationService? _instance;
        public static LocalizationService Instance => _instance ??= new LocalizationService();

        private string _currentLanguage = "en";
        public string CurrentLanguage => _currentLanguage;

        public event Action? OnLanguageChanged;

        public IReadOnlyList<LanguageOption> SupportedLanguages { get; } = new List<LanguageOption>
        {
            new("de", "Deutsch (German)", "Deutsch", "🇩🇪"),
            new("en", "English (English)", "English", "🇺🇸"),
            new("es", "Español (Spanish)", "Español", "🇪🇸"),
            new("fr", "Français (French)", "Français", "🇫🇷"),
            new("it", "Italiano (Italian)", "Italiano", "🇮🇹"),
            new("ja", "Japanese (日本語)", "日本語", "🇯🇵"),
            new("ko", "Korean (한국어)", "한국어", "🇰🇷"),
            new("pl", "Polski (Polish)", "Polski", "🇵🇱"),
            new("pt-BR", "Português (Brasil)", "Português (Brasil)", "🇧🇷"),
            new("ru", "Russian (Русский)", "Русский", "🇷🇺"),
            new("tr", "Türkçe (Turkish)", "Türkçe", "🇹🇷")
        }.OrderBy(x => x.DisplayName).ToList();

        private readonly Dictionary<string, Dictionary<string, string>> _translations = new();

        public LocalizationService()
        {
            InitializeTranslations();
            LoadLanguagePreference();
        }

        public string this[string key] => GetString(key);

        public string GetString(string key, string? fallback = null)
        {
            if (_translations.TryGetValue(_currentLanguage, out var dict) && dict.TryGetValue(key, out var val))
            {
                return val;
            }
            if (_currentLanguage != "en" && _translations.TryGetValue("en", out var enDict) && enDict.TryGetValue(key, out var enVal))
            {
                return enVal;
            }
            return fallback ?? key;
        }

        public void SetLanguage(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode)) return;
            var match = SupportedLanguages.FirstOrDefault(l => l.Code.Equals(languageCode, StringComparison.OrdinalIgnoreCase));
            if (match == null) match = SupportedLanguages.FirstOrDefault(l => l.Code == "en") ?? SupportedLanguages.First();

            if (_currentLanguage != match.Code)
            {
                _currentLanguage = match.Code;
                SaveLanguagePreference(_currentLanguage);
                Log.Information("Language changed to: {Lang}", _currentLanguage);
                OnLanguageChanged?.Invoke();
            }
        }
        private void LoadLanguagePreference()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Language", out var langProp))
                    {
                        string? savedLang = langProp.GetString();
                        if (!string.IsNullOrEmpty(savedLang) && SupportedLanguages.Any(l => l.Code.Equals(savedLang, StringComparison.OrdinalIgnoreCase)))
                        {
                            _currentLanguage = savedLang;
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load language preference from settings.json");
            }
            _currentLanguage = "en";
        }

        private void SaveLanguagePreference(string langCode)
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new();
                    dict["Language"] = langCode;
                    string updatedJson = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(SettingsPath, updatedJson);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to save language preference to settings.json");
            }
        }

        private void InitializeTranslations()
        {
            _translations["en"] = new Dictionary<string, string>
            {
                ["App_Title"] = "Radial Launcher — Settings & Management",
                ["Nav_Apps"] = "📋  Applications & Shortcuts",
                ["Nav_Themes"] = "🎨  Themes & Appearance",
                ["Nav_Shortcuts"] = "⚙️  Shortcuts & Startup",
                ["Nav_Backups"] = "💾  Backup & Data",
                ["Nav_System"] = "ℹ️  System & Diagnostics",
                ["Scan_PC"] = "🔍 Scan PC",
                ["Add_Item"] = "➕ Add New Item",
                ["Category"] = "Category:",
                ["Search"] = "Search:",
                ["Search_Placeholder"] = "Search apps, games, actions...",
                ["All"] = "All",
                ["Games"] = "Games",
                ["Apps"] = "Applications",
                ["Web_Tools"] = "Web & Tools",
                ["System"] = "System",
                ["Most_Used"] = "Most Used",
                ["Edit"] = "Edit",
                ["Delete"] = "Delete",
                ["Save"] = "Save",
                ["Cancel"] = "Cancel",
                ["Close"] = "Close",
                ["Export"] = "📤 Export (JSON)",
                ["Import"] = "📥 Import (JSON)",
                ["Backup_Now"] = "💾 Create Local Backup",
                ["Restore_Backup"] = "📂 Restore from Backup",
                ["Check_Updates"] = "🔄 Check for Updates Now",
                ["Open_Logs"] = "📁 Open Logs Folder",
                ["Copy_Diag"] = "📋 Copy Diagnostics",
                ["Reset_Factory"] = "⚠️ Reset to Factory Defaults",
                ["Language"] = "🌐 Display Language",
                ["Language_Desc"] = "Select your preferred application interface language.",
                ["Startup_Title"] = "Windows Startup",
                ["Startup_Check"] = "Automatically start Radial Launcher in tray on Windows startup",
                ["Trigger_Title"] = "Activation Hotkey & Shortcut",
                ["Trigger_Desc"] = "Select mouse button or keyboard shortcut to summon radial menu.",
                ["Assign_Shortcut"] = "🎯 Assign Custom Shortcut",
                ["Opacity_Title"] = "Radial Menu Opacity",
                ["Opacity_Desc"] = "Adjust background transparency level of the circular overlay.",
                ["Density_Title"] = "Ring Density Mode",
                ["Density_Desc"] = "Number of items displayed per circular ring page.",
                ["Density_Expanded"] = "Expanded (15 Items)",
                ["Density_Compact"] = "Compact (18 Items)",
                ["Reduce_Motion"] = "Reduce Motion / Simplified Animations",
                ["Palette_Title"] = "Active Theme Color Palette",
                ["Primary_Accent"] = "Primary Accent",
                ["Secondary_Accent"] = "Secondary Accent",
                ["Background"] = "Background",
                ["Icon_Bubble"] = "Icon Bubble",
                ["Play"] = "▶ Play",
                ["Store"] = "🛒 Store",
                ["Community"] = "👥 Community",
                ["Location"] = "📁 Location",
                ["Run_Admin"] = "⚡ Run as Admin",
                ["Status_Ready"] = "Ready.",
                ["Status_Saved"] = "Settings saved successfully.",
                ["Installed_Version"] = "Installed Version: v1.0.0 (Production Release)"
            };
            _translations["tr"] = new Dictionary<string, string>
            {
                ["App_Title"] = "Radial Launcher — Yönetim & Ayarlar",
                ["Nav_Apps"] = "📋  Uygulamalar & Kısayollar",
                ["Nav_Themes"] = "🎨  Tema & Görünüm",
                ["Nav_Shortcuts"] = "⚙️  Kısayol & Başlangıç",
                ["Nav_Backups"] = "💾  Yedekleme & Veri",
                ["Nav_System"] = "ℹ️  Güncelleme & Tanılama",
                ["Scan_PC"] = "🔍 Bilgisayarı Tara",
                ["Add_Item"] = "➕ Yeni Öğe Ekle",
                ["Category"] = "Kategori:",
                ["Search"] = "Ara:",
                ["Search_Placeholder"] = "Uygulama, oyun veya eylem ara...",
                ["All"] = "Tümü",
                ["Games"] = "Oyunlar",
                ["Apps"] = "Uygulamalar",
                ["Web_Tools"] = "Web & Araçlar",
                ["System"] = "Sistem",
                ["Most_Used"] = "En Çok Kullanılanlar",
                ["Edit"] = "Düzenle",
                ["Delete"] = "Sil",
                ["Save"] = "Kaydet",
                ["Cancel"] = "İptal",
                ["Close"] = "Kapat",
                ["Export"] = "📤 Dışa Aktar (JSON)",
                ["Import"] = "📥 İçe Aktar (JSON)",
                ["Backup_Now"] = "💾 Şimdi Yerel Yedekle",
                ["Restore_Backup"] = "📂 Yerel Yedekten Geri Yükle",
                ["Check_Updates"] = "🔄 Güncellemeleri Şimdi Kontrol Et",
                ["Open_Logs"] = "📁 Log Klasörünü Aç",
                ["Copy_Diag"] = "📋 Tanılamayı Kopyala",
                ["Reset_Factory"] = "⚠️ Fabrika Ayarlarına Sıfırla",
                ["Language"] = "🌐 Arayüz Dili",
                ["Language_Desc"] = "Radial Launcher arayüzü için tercih ettiğiniz dili seçin.",
                ["Startup_Title"] = "Windows Başlangıcı",
                ["Startup_Check"] = "Windows açıldığında Radial Launcher'ı otomatik başlat (Tepsi Modu)",
                ["Trigger_Title"] = "Menüyü Tetikleme Kısayolu",
                ["Trigger_Desc"] = "Radial Launcher'ı ekranda açmak için fare veya klavye kısayolu seçin.",
                ["Assign_Shortcut"] = "🎯 Yeni Kısayol Ata",
                ["Opacity_Title"] = "Radyal Menü Saydamlığı (Opacity)",
                ["Opacity_Desc"] = "Dairesel menü arkaplanının cam saydamlık seviyesini ayarlayın.",
                ["Density_Title"] = "Halka Yoğunluğu (Density)",
                ["Density_Desc"] = "Tek bir halka sayfasında kaç öğe yerleştirileceğini belirler.",
                ["Density_Expanded"] = "Geniş (15 Öğeli)",
                ["Density_Compact"] = "Kompakt (18 Öğeli)",
                ["Reduce_Motion"] = "Hareketi Azalt / Animasyonları Sadeleştir (Reduce Motion)",
                ["Palette_Title"] = "Seçili Tema Renk Paleti",
                ["Primary_Accent"] = "Birincil Vurgu",
                ["Secondary_Accent"] = "İkincil Vurgu",
                ["Background"] = "Arkaplan",
                ["Icon_Bubble"] = "İkon Baloncuğu",
                ["Play"] = "▶ Oyna",
                ["Store"] = "🛒 Mağaza",
                ["Community"] = "👥 Topluluk",
                ["Location"] = "📁 Konum",
                ["Run_Admin"] = "⚡ Yönetici",
                ["Status_Ready"] = "Hazır.",
                ["Status_Saved"] = "Ayarlar başarıyla kaydedildi.",
                ["Installed_Version"] = "Yüklü Sürüm: v1.0.0 (Final Release)"
            };

            _translations["de"] = new Dictionary<string, string>
            {
                ["App_Title"] = "Radial Launcher — Einstellungen & Verwaltung",
                ["Nav_Apps"] = "📋  Anwendungen & Verknüpfungen",
                ["Nav_Themes"] = "🎨  Designs & Aussehen",
                ["Nav_Shortcuts"] = "⚙️  Tastenkombinationen & Autostart",
                ["Nav_Backups"] = "💾  Sicherung & Daten",
                ["Nav_System"] = "ℹ️  System & Diagnose",
                ["Scan_PC"] = "🔍 PC Scannen",
                ["Add_Item"] = "➕ Neues Element Hinzufügen",
                ["Category"] = "Kategorie:",
                ["Search"] = "Suchen:",
                ["Search_Placeholder"] = "Apps, Spiele, Aktionen suchen...",
                ["All"] = "Alle",
                ["Games"] = "Spiele",
                ["Apps"] = "Anwendungen",
                ["Web_Tools"] = "Web & Werkzeuge",
                ["System"] = "System",
                ["Most_Used"] = "Meistgenutzt",
                ["Edit"] = "Bearbeiten",
                ["Delete"] = "Löschen",
                ["Save"] = "Speichern",
                ["Cancel"] = "Abbrechen",
                ["Close"] = "Schließen",
                ["Export"] = "📤 Exportieren (JSON)",
                ["Import"] = "📥 Importieren (JSON)",
                ["Backup_Now"] = "💾 Lokale Sicherung Erstellen",
                ["Restore_Backup"] = "📂 Aus Sicherung Wiederherstellen",
                ["Check_Updates"] = "🔄 Jetzt Nach Updates Suchen",
                ["Open_Logs"] = "📁 Protokollordner Öffnen",
                ["Copy_Diag"] = "📋 Diagnose Kopieren",
                ["Reset_Factory"] = "⚠️ Auf Werkseinstellungen Zurücksetzen",
                ["Language"] = "🌐 Anzeigesprache",
                ["Language_Desc"] = "Wählen Sie Ihre bevorzugte Programmsprache.",
                ["Startup_Title"] = "Windows-Autostart",
                ["Startup_Check"] = "Radial Launcher beim Windows-Start automatisch im Infobereich ausführen",
                ["Trigger_Title"] = "Aktivierungs-Tastenkombination",
                ["Trigger_Desc"] = "Wählen Sie die Maus- oder Tastaturtaste zum Öffnen des Radialmenüs.",
                ["Assign_Shortcut"] = "🎯 Benutzerdefinierte Tastenkombination",
                ["Opacity_Title"] = "Radialmenü-Transparenz",
                ["Opacity_Desc"] = "Passen Sie die Hintergrundtransparenz des Menüs an.",
                ["Density_Title"] = "Ringdichte-Modus",
                ["Density_Desc"] = "Anzahl der angezeigten Elemente pro Ringseite.",
                ["Density_Expanded"] = "Erweitert (15 Elemente)",
                ["Density_Compact"] = "Kompakt (18 Elemente)",
                ["Reduce_Motion"] = "Animationen Reduzieren",
                ["Palette_Title"] = "Farbpalette Des Aktiven Designs",
                ["Primary_Accent"] = "Primärer Akzent",
                ["Secondary_Accent"] = "Sekundärer Akzent",
                ["Background"] = "Hintergrund",
                ["Icon_Bubble"] = "Symbolblase",
                ["Play"] = "▶ Spielen",
                ["Store"] = "🛒 Shop",
                ["Community"] = "👥 Gemeinschaft",
                ["Location"] = "📁 Ort",
                ["Run_Admin"] = "⚡ Als Admin Ausführen",
                ["Status_Ready"] = "Bereit.",
                ["Status_Saved"] = "Einstellungen erfolgreich gespeichert.",
                ["Installed_Version"] = "Installierte Version: v1.0.0"
            };

            _translations["es"] = new Dictionary<string, string>
            {
                ["App_Title"] = "Radial Launcher — Configuración y Gestión",
                ["Nav_Apps"] = "📋  Aplicaciones y Accesos",
                ["Nav_Themes"] = "🎨  Temas y Apariencia",
                ["Nav_Shortcuts"] = "⚙️  Atajos e Inicio",
                ["Nav_Backups"] = "💾  Copias de Seguridad",
                ["Nav_System"] = "ℹ️  Sistema y Diagnóstico",
                ["Scan_PC"] = "🔍 Escanear PC",
                ["Add_Item"] = "➕ Agregar Nuevo Elemento",
                ["Category"] = "Categoría:",
                ["Search"] = "Buscar:",
                ["Search_Placeholder"] = "Buscar aplicaciones, juegos, acciones...",
                ["All"] = "Todo",
                ["Games"] = "Juegos",
                ["Apps"] = "Aplicaciones",
                ["Web_Tools"] = "Web y Herramientas",
                ["System"] = "Sistema",
                ["Most_Used"] = "Más Usados",
                ["Edit"] = "Editar",
                ["Delete"] = "Eliminar",
                ["Save"] = "Guardar",
                ["Cancel"] = "Cancelar",
                ["Close"] = "Cerrar",
                ["Export"] = "📤 Exportar (JSON)",
                ["Import"] = "📥 Importar (JSON)",
                ["Backup_Now"] = "💾 Crear Copia Local",
                ["Restore_Backup"] = "📂 Restaurar Copia",
                ["Check_Updates"] = "🔄 Buscar Actualizaciones",
                ["Open_Logs"] = "📁 Abrir Carpeta de Registros",
                ["Copy_Diag"] = "📋 Copiar Diagnóstico",
                ["Reset_Factory"] = "⚠️ Restablecer Valores de Fábrica",
                ["Language"] = "🌐 Idioma de Visualización",
                ["Language_Desc"] = "Seleccione el idioma preferido para la interfaz.",
                ["Startup_Title"] = "Inicio de Windows",
                ["Startup_Check"] = "Iniciar automáticamente Radial Launcher en la bandeja al iniciar Windows",
                ["Trigger_Title"] = "Atajo de Activación",
                ["Trigger_Desc"] = "Elija el botón del ratón o teclado para abrir el menú radial.",
                ["Assign_Shortcut"] = "🎯 Asignar Atajo Personalizado",
                ["Opacity_Title"] = "Opacidad del Menú Radial",
                ["Opacity_Desc"] = "Ajuste la transparencia del fondo de cristal.",
                ["Density_Title"] = "Modo de Densidad de Anillo",
                ["Density_Desc"] = "Número de elementos mostrados por página de anillo.",
                ["Density_Expanded"] = "Expandido (15 Elementos)",
                ["Density_Compact"] = "Compacto (18 Elementos)",
                ["Reduce_Motion"] = "Reducir Movimiento / Animaciones Simples",
                ["Palette_Title"] = "Paleta de Colores del Tema",
                ["Primary_Accent"] = "Acento Primario",
                ["Secondary_Accent"] = "Acento Secundario",
                ["Background"] = "Fondo",
                ["Icon_Bubble"] = "Burbuja de Icono",
                ["Play"] = "▶ Jugar",
                ["Store"] = "🛒 Tienda",
                ["Community"] = "👥 Comunidad",
                ["Location"] = "📁 Ubicación",
                ["Run_Admin"] = "⚡ Ejecutar como Admin",
                ["Status_Ready"] = "Listo.",
                ["Status_Saved"] = "Configuración guardada con éxito.",
                ["Installed_Version"] = "Versión Instalada: v1.0.0"
            };

            _translations["fr"] = new Dictionary<string, string>
            {
                ["App_Title"] = "Radial Launcher — Paramètres & Gestion",
                ["Nav_Apps"] = "📋  Applications & Raccourcis",
                ["Nav_Themes"] = "🎨  Thèmes & Apparence",
                ["Nav_Shortcuts"] = "⚙️  Raccourcis & Démarrage",
                ["Nav_Backups"] = "💾  Sauvegarde & Données",
                ["Nav_System"] = "ℹ️  Système & Diagnostics",
                ["Scan_PC"] = "🔍 Analyser le PC",
                ["Add_Item"] = "➕ Ajouter un Élément",
                ["Category"] = "Catégorie :",
                ["Search"] = "Rechercher :",
                ["Search_Placeholder"] = "Rechercher des applications, jeux...",
                ["All"] = "Tous",
                ["Games"] = "Jeux",
                ["Apps"] = "Applications",
                ["Web_Tools"] = "Web & Outils",
                ["System"] = "Système",
                ["Most_Used"] = "Les Plus Utilisés",
                ["Edit"] = "Modifier",
                ["Delete"] = "Supprimer",
                ["Save"] = "Enregistrer",
                ["Cancel"] = "Annuler",
                ["Close"] = "Fermer",
                ["Export"] = "📤 Exporter (JSON)",
                ["Import"] = "📥 Importer (JSON)",
                ["Backup_Now"] = "💾 Créer une Sauvegarde",
                ["Restore_Backup"] = "📂 Restaurer une Sauvegarde",
                ["Check_Updates"] = "🔄 Vérifier les Mises à Jour",
                ["Open_Logs"] = "📁 Ouvrir le Dossier des Journaux",
                ["Copy_Diag"] = "📋 Copier les Diagnostics",
                ["Reset_Factory"] = "⚠️ Réinitialiser aux Paramètres d'Usine",
                ["Language"] = "🌐 Langue d'Affichage",
                ["Language_Desc"] = "Sélectionnez votre langue d'interface préférée.",
                ["Startup_Title"] = "Démarrage Windows",
                ["Startup_Check"] = "Lancer automatiquement Radial Launcher au démarrage de Windows",
                ["Trigger_Title"] = "Raccourci d'Activation",
                ["Trigger_Desc"] = "Choisissez le bouton de souris ou le raccourci pour ouvrir le menu.",
                ["Assign_Shortcut"] = "🎯 Assigner un Raccourci",
                ["Opacity_Title"] = "Opacité du Menu Radial",
                ["Opacity_Desc"] = "Réglez la transparence de l'arrière-plan.",
                ["Density_Title"] = "Densité de l'Anneau",
                ["Density_Desc"] = "Nombre d'éléments affichés par page d'anneau.",
                ["Density_Expanded"] = "Étendu (15 Éléments)",
                ["Density_Compact"] = "Compact (18 Éléments)",
                ["Reduce_Motion"] = "Réduire les Animations",
                ["Palette_Title"] = "Palette de Couleurs Active",
                ["Primary_Accent"] = "Accent Primaire",
                ["Secondary_Accent"] = "Accent Secondaire",
                ["Background"] = "Arrière-plan",
                ["Icon_Bubble"] = "Bulle d'Icône",
                ["Play"] = "▶ Jouer",
                ["Store"] = "🛒 Boutique",
                ["Community"] = "👥 Communauté",
                ["Location"] = "📁 Emplacement",
                ["Run_Admin"] = "⚡ Exécuter en Admin",
                ["Status_Ready"] = "Prêt.",
                ["Status_Saved"] = "Paramètres enregistrés avec succès.",
                ["Installed_Version"] = "Version Installée : v1.0.0"
            };
            _translations["it"] = new Dictionary<string, string>
            {
                ["App_Title"] = "Radial Launcher — Impostazioni e Gestione",
                ["Nav_Apps"] = "📋  Applicazioni e Scorciatoie",
                ["Nav_Themes"] = "🎨  Temi e Aspetto",
                ["Nav_Shortcuts"] = "⚙️  Scorciatoie e Avvio",
                ["Nav_Backups"] = "💾  Backup e Dati",
                ["Nav_System"] = "ℹ️  Sistema e Diagnostica",
                ["Scan_PC"] = "🔍 Scansiona PC",
                ["Add_Item"] = "➕ Aggiungi Elemento",
                ["Category"] = "Categoria:",
                ["Search"] = "Cerca:",
                ["Search_Placeholder"] = "Cerca app, giochi, azioni...",
                ["All"] = "Tutti",
                ["Games"] = "Giochi",
                ["Apps"] = "Applicazioni",
                ["Web_Tools"] = "Web e Strumenti",
                ["System"] = "Sistema",
                ["Most_Used"] = "Più Usati",
                ["Edit"] = "Modifica",
                ["Delete"] = "Elimina",
                ["Save"] = "Salva",
                ["Cancel"] = "Annulla",
                ["Close"] = "Chiudi",
                ["Export"] = "📤 Esporta (JSON)",
                ["Import"] = "📥 Importa (JSON)",
                ["Backup_Now"] = "💾 Crea Backup Locale",
                ["Restore_Backup"] = "📂 Ripristina Backup",
                ["Check_Updates"] = "🔄 Controlla Aggiornamenti",
                ["Open_Logs"] = "📁 Apri Cartella Log",
                ["Copy_Diag"] = "📋 Copia Diagnostica",
                ["Reset_Factory"] = "⚠️ Ripristina Impostazioni di Fabbrica",
                ["Language"] = "🌐 Lingua di Visualizzazione",
                ["Language_Desc"] = "Seleziona la lingua per l'interfaccia.",
                ["Startup_Title"] = "Avvio di Windows",
                ["Startup_Check"] = "Avvia automaticamente Radial Launcher nella barra delle applicazioni all'avvio",
                ["Trigger_Title"] = "Scorciatoia di Attivazione",
                ["Trigger_Desc"] = "Scegli il pulsante del mouse o la scorciatoia da tastiera.",
                ["Assign_Shortcut"] = "🎯 Assegna Scorciatoia Personalizzata",
                ["Opacity_Title"] = "Opacità Menu Radiale",
                ["Opacity_Desc"] = "Regola il livello di trasparenza dello sfondo.",
                ["Density_Title"] = "Densità dell'Anello",
                ["Density_Desc"] = "Numero di elementi mostrati per pagina.",
                ["Density_Expanded"] = "Espanso (15 Elementi)",
                ["Density_Compact"] = "Compatto (18 Elementi)",
                ["Reduce_Motion"] = "Riduci Movimento",
                ["Palette_Title"] = "Tavolozza Colori del Tema",
                ["Primary_Accent"] = "Accento Primario",
                ["Secondary_Accent"] = "Accento Secondario",
                ["Background"] = "Sfondo",
                ["Icon_Bubble"] = "Bolla Icona",
                ["Play"] = "▶ Gioca",
                ["Store"] = "🛒 Negozio",
                ["Community"] = "👥 Comunità",
                ["Location"] = "📁 Posizione",
                ["Run_Admin"] = "⚡ Esegui come Admin",
                ["Status_Ready"] = "Pronto.",
                ["Status_Saved"] = "Impostazioni salvate con successo.",
                ["Installed_Version"] = "Versione Installata: v1.0.0"
            };

            _translations["ja"] = new Dictionary<string, string>
            {
                ["App_Title"] = "Radial Launcher — 設定と管理",
                ["Nav_Apps"] = "📋  アプリとショートカット",
                ["Nav_Themes"] = "🎨  テーマと外観",
                ["Nav_Shortcuts"] = "⚙️  ショートカットとスタートアップ",
                ["Nav_Backups"] = "💾  バックアップとデータ",
                ["Nav_System"] = "ℹ️  システムと診断",
                ["Scan_PC"] = "🔍 PCをスキャン",
                ["Add_Item"] = "➕ 新規アイテム追加",
                ["Category"] = "カテゴリ:",
                ["Search"] = "検索:",
                ["Search_Placeholder"] = "アプリ、ゲーム、アクションを検索...",
                ["All"] = "すべて",
                ["Games"] = "ゲーム",
                ["Apps"] = "アプリケーション",
                ["Web_Tools"] = "Webとツール",
                ["System"] = "システム",
                ["Most_Used"] = "よく使う項目",
                ["Edit"] = "編集",
                ["Delete"] = "削除",
                ["Save"] = "保存",
                ["Cancel"] = "キャンセル",
                ["Close"] = "閉じる",
                ["Export"] = "📤 エクスポート (JSON)",
                ["Import"] = "📥 インポート (JSON)",
                ["Backup_Now"] = "💾 ローカルバックアップ作成",
                ["Restore_Backup"] = "📂 バックアップから復元",
                ["Check_Updates"] = "🔄 アップデートを確認",
                ["Open_Logs"] = "📁 ログフォルダを開く",
                ["Copy_Diag"] = "📋 診断情報をコピー",
                ["Reset_Factory"] = "⚠️ 初期設定にリセット",
                ["Language"] = "🌐 表示言語",
                ["Language_Desc"] = "Radial Launcherの表示言語を選択します。",
                ["Startup_Title"] = "Windows スタートアップ",
                ["Startup_Check"] = "Windows起動時にトレイで自動起動する",
                ["Trigger_Title"] = "起動ショートカット",
                ["Trigger_Desc"] = "ラジアルメニューを開くマウスまたはキーボードを選択します。",
                ["Assign_Shortcut"] = "🎯 カスタムショートカット割り当て",
                ["Opacity_Title"] = "メニューの不透明度",
                ["Opacity_Desc"] = "円形メニューの背景の透明度を調整します。",
                ["Density_Title"] = "リング密度モード",
                ["Density_Desc"] = "1ページあたりに配置するアイテム数。",
                ["Density_Expanded"] = "標準 (15 アイテム)",
                ["Density_Compact"] = "コンパクト (18 アイテム)",
                ["Reduce_Motion"] = "アニメーションを簡略化",
                ["Palette_Title"] = "テーマのカラーパレット",
                ["Primary_Accent"] = "プライマリアクセント",
                ["Secondary_Accent"] = "セカンダリアクセント",
                ["Background"] = "背景",
                ["Icon_Bubble"] = "アイコンバブル",
                ["Play"] = "▶ プレイ",
                ["Store"] = "🛒 ストア",
                ["Community"] = "👥 コミュニティ",
                ["Location"] = "📁 場所",
                ["Run_Admin"] = "⚡ 管理者として実行",
                ["Status_Ready"] = "準備完了。",
                ["Status_Saved"] = "設定が正常に保存されました。",
                ["Installed_Version"] = "インストール済みバージョン: v1.0.0"
            };

            _translations["ko"] = new Dictionary<string, string>
            {
                ["App_Title"] = "Radial Launcher — 설정 및 관리",
                ["Nav_Apps"] = "📋  앱 및 바로가기",
                ["Nav_Themes"] = "🎨  테마 및 디자인",
                ["Nav_Shortcuts"] = "⚙️  단축키 및 시작 프로그램",
                ["Nav_Backups"] = "💾  백업 및 데이터",
                ["Nav_System"] = "ℹ️  시스템 및 진단",
                ["Scan_PC"] = "🔍 PC 검사",
                ["Add_Item"] = "➕ 새 항목 추가",
                ["Category"] = "카테고리:",
                ["Search"] = "검색:",
                ["Search_Placeholder"] = "앱, 게임, 작업 검색...",
                ["All"] = "전체",
                ["Games"] = "게임",
                ["Apps"] = "애플리케이션",
                ["Web_Tools"] = "웹 및 도구",
                ["System"] = "시스템",
                ["Most_Used"] = "자주 사용하는 항목",
                ["Edit"] = "편집",
                ["Delete"] = "삭제",
                ["Save"] = "저장",
                ["Cancel"] = "취소",
                ["Close"] = "닫기",
                ["Export"] = "📤 내보내기 (JSON)",
                ["Import"] = "📥 가져오기 (JSON)",
                ["Backup_Now"] = "💾 로컬 백업 생성",
                ["Restore_Backup"] = "📂 백업에서 복원",
                ["Check_Updates"] = "🔄 지금 업데이트 확인",
                ["Open_Logs"] = "📁 로그 폴더 열기",
                ["Copy_Diag"] = "📋 진단 정보 복사",
                ["Reset_Factory"] = "⚠️ 초기 설정으로 복원",
                ["Language"] = "🌐 표시 언어",
                ["Language_Desc"] = "기본 표시 언어를 선택합니다.",
                ["Startup_Title"] = "Windows 시작 프로그램",
                ["Startup_Check"] = "Windows 시작 시 트레이 모드로 자동 실행",
                ["Trigger_Title"] = "실행 단축키",
                ["Trigger_Desc"] = "원형 메뉴를 열 마우스 버튼 또는 키보드 단축키를 선택하세요.",
                ["Assign_Shortcut"] = "🎯 사용자 지정 단축키 할당",
                ["Opacity_Title"] = "메뉴 불투명도",
                ["Opacity_Desc"] = "배경 유리 투명도를 조절합니다.",
                ["Density_Title"] = "원형 밀도 모드",
                ["Density_Desc"] = "한 페이지에 표시할 항목 수를 설정합니다.",
                ["Density_Expanded"] = "확장 (15개 항목)",
                ["Density_Compact"] = "컴팩트 (18개 항목)",
                ["Reduce_Motion"] = "동작 줄이기",
                ["Palette_Title"] = "테마 색상 팔레트",
                ["Primary_Accent"] = "기본 강조색",
                ["Secondary_Accent"] = "보조 강조색",
                ["Background"] = "배경색",
                ["Icon_Bubble"] = "아이콘 버블",
                ["Play"] = "▶ 플레이",
                ["Store"] = "🛒 상점",
                ["Community"] = "👥 커뮤니티",
                ["Location"] = "📁 위치",
                ["Run_Admin"] = "⚡ 관리자 권한 실행",
                ["Status_Ready"] = "준비 완료.",
                ["Status_Saved"] = "설정이 성공적으로 저장되었습니다.",
                ["Installed_Version"] = "설치된 버전: v1.0.0"
            };

            _translations["pl"] = new Dictionary<string, string>
            {
                ["App_Title"] = "Radial Launcher — Ustawienia i Zarządzanie",
                ["Nav_Apps"] = "📋  Aplikacje i Skróty",
                ["Nav_Themes"] = "🎨  Motywy i Wygląd",
                ["Nav_Shortcuts"] = "⚙️  Skróty i Autostart",
                ["Nav_Backups"] = "💾  Kopia Zapasowa i Dane",
                ["Nav_System"] = "ℹ️  System i Diagnostyka",
                ["Scan_PC"] = "🔍 Skanuj Komputer",
                ["Add_Item"] = "➕ Dodaj Nowy Element",
                ["Category"] = "Kategoria:",
                ["Search"] = "Szukaj:",
                ["Search_Placeholder"] = "Szukaj aplikacji, gier...",
                ["All"] = "Wszystkie",
                ["Games"] = "Gry",
                ["Apps"] = "Aplikacje",
                ["Web_Tools"] = "Sieć i Narzędzia",
                ["System"] = "System",
                ["Most_Used"] = "Najczęściej Używane",
                ["Edit"] = "Edytuj",
                ["Delete"] = "Usuń",
                ["Save"] = "Zapisz",
                ["Cancel"] = "Anuluj",
                ["Close"] = "Zamknij",
                ["Export"] = "📤 Eksportuj (JSON)",
                ["Import"] = "📥 Importuj (JSON)",
                ["Backup_Now"] = "💾 Utwórz Kopię Zapasową",
                ["Restore_Backup"] = "📂 Przywróć z Kopii",
                ["Check_Updates"] = "🔄 Sprawdź Aktualizacje",
                ["Open_Logs"] = "📁 Otwórz Folder Dzienników",
                ["Copy_Diag"] = "📋 Kopiuj Diagnostykę",
                ["Reset_Factory"] = "⚠️ Przywróć Ustawienia Fabryczne",
                ["Language"] = "🌐 Język Interfejsu",
                ["Language_Desc"] = "Wybierz preferowany język interfejsu.",
                ["Startup_Title"] = "Autostart Windows",
                ["Startup_Check"] = "Uruchamiaj Radial Launcher automatycznie w zasobniku systemowym",
                ["Trigger_Title"] = "Skrót Aktywacji",
                ["Trigger_Desc"] = "Wybierz przycisk myszy lub skrót klawiszowy.",
                ["Assign_Shortcut"] = "🎯 Przypisz Nowy Skrót",
                ["Opacity_Title"] = "Przezroczystość Menu",
                ["Opacity_Desc"] = "Dostosuj poziom przezroczystości tła.",
                ["Density_Title"] = "Gęstość Pierścienia",
                ["Density_Desc"] = "Liczba elementów na stronę pierścienia.",
                ["Density_Expanded"] = "Rozszerzony (15 Elementów)",
                ["Density_Compact"] = "Kompaktowy (18 Elementów)",
                ["Reduce_Motion"] = "Zredukuj Ruch",
                ["Palette_Title"] = "Paleta Kolorów Motywu",
                ["Primary_Accent"] = "Główny Akcent",
                ["Secondary_Accent"] = "Drugi Akcent",
                ["Background"] = "Tło",
                ["Icon_Bubble"] = "Bąbel Ikony",
                ["Play"] = "▶ Graj",
                ["Store"] = "🛒 Sklep",
                ["Community"] = "👥 Społeczność",
                ["Location"] = "📁 Lokalizacja",
                ["Run_Admin"] = "⚡ Uruchom jako Administrator",
                ["Status_Ready"] = "Gotowy.",
                ["Status_Saved"] = "Ustawienia zapisane pomyślnie.",
                ["Installed_Version"] = "Zainstalowana Wersja: v1.0.0"
            };

            _translations["pt-BR"] = new Dictionary<string, string>
            {
                ["App_Title"] = "Radial Launcher — Configurações e Gerenciamento",
                ["Nav_Apps"] = "📋  Aplicativos e Atalhos",
                ["Nav_Themes"] = "🎨  Temas e Aparência",
                ["Nav_Shortcuts"] = "⚙️  Atalhos e Inicialização",
                ["Nav_Backups"] = "💾  Backup e Dados",
                ["Nav_System"] = "ℹ️  Sistema e Diagnósticos",
                ["Scan_PC"] = "🔍 Escanear PC",
                ["Add_Item"] = "➕ Adicionar Novo Item",
                ["Category"] = "Categoria:",
                ["Search"] = "Pesquisar:",
                ["Search_Placeholder"] = "Pesquisar aplicativos, jogos, ações...",
                ["All"] = "Todos",
                ["Games"] = "Jogos",
                ["Apps"] = "Aplicativos",
                ["Web_Tools"] = "Web e Ferramentas",
                ["System"] = "Sistema",
                ["Most_Used"] = "Mais Usados",
                ["Edit"] = "Editar",
                ["Delete"] = "Excluir",
                ["Save"] = "Salvar",
                ["Cancel"] = "Cancelar",
                ["Close"] = "Fechar",
                ["Export"] = "📤 Exportar (JSON)",
                ["Import"] = "📥 Importar (JSON)",
                ["Backup_Now"] = "💾 Criar Backup Local",
                ["Restore_Backup"] = "📂 Restaurar do Backup",
                ["Check_Updates"] = "🔄 Verificar Atualizações",
                ["Open_Logs"] = "📁 Abrir Pasta de Logs",
                ["Copy_Diag"] = "📋 Copiar Diagnóstico",
                ["Reset_Factory"] = "⚠️ Restaurar Padrões de Fábrica",
                ["Language"] = "🌐 Idioma de Exibição",
                ["Language_Desc"] = "Selecione o idioma de sua preferência.",
                ["Startup_Title"] = "Inicialização do Windows",
                ["Startup_Check"] = "Iniciar automaticamente o Radial Launcher na bandeja com o Windows",
                ["Trigger_Title"] = "Atalho de Ativação",
                ["Trigger_Desc"] = "Escolha o botão do mouse ou atalho de teclado para abrir o menu.",
                ["Assign_Shortcut"] = "🎯 Atribuir Novo Atalho",
                ["Opacity_Title"] = "Opacidade do Menu Radial",
                ["Opacity_Desc"] = "Ajuste o nível de transparência do fundo de vidro.",
                ["Density_Title"] = "Densidade do Anel",
                ["Density_Desc"] = "Número de itens exibidos por página.",
                ["Density_Expanded"] = "Expandido (15 Itens)",
                ["Density_Compact"] = "Compacto (18 Itens)",
                ["Reduce_Motion"] = "Reduzir Movimento",
                ["Palette_Title"] = "Paleta de Cores do Tema",
                ["Primary_Accent"] = "Destaque Primário",
                ["Secondary_Accent"] = "Destaque Secundário",
                ["Background"] = "Fundo",
                ["Icon_Bubble"] = "Bolha de Ícone",
                ["Play"] = "▶ Jogar",
                ["Store"] = "🛒 Loja",
                ["Community"] = "👥 Comunidade",
                ["Location"] = "📁 Localização",
                ["Run_Admin"] = "⚡ Executar como Administrador",
                ["Status_Ready"] = "Pronto.",
                ["Status_Saved"] = "Configurações salvas com sucesso.",
                ["Installed_Version"] = "Versão Instalada: v1.0.0"
            };

            _translations["ru"] = new Dictionary<string, string>
            {
                ["App_Title"] = "Radial Launcher — Настройки и Управление",
                ["Nav_Apps"] = "📋  Приложения и Ярлыки",
                ["Nav_Themes"] = "🎨  Темы и Внешний Вид",
                ["Nav_Shortcuts"] = "⚙️  Горячие Клавиши и Автозапуск",
                ["Nav_Backups"] = "💾  Резервные Копии и Данные",
                ["Nav_System"] = "ℹ️  Система и Диагностика",
                ["Scan_PC"] = "🔍 Сканировать ПК",
                ["Add_Item"] = "➕ Добавить Элемент",
                ["Category"] = "Категория:",
                ["Search"] = "Поиск:",
                ["Search_Placeholder"] = "Поиск приложений, игр, действий...",
                ["All"] = "Все",
                ["Games"] = "Игры",
                ["Apps"] = "Приложения",
                ["Web_Tools"] = "Веб и Утилиты",
                ["System"] = "Система",
                ["Most_Used"] = "Часто Используемые",
                ["Edit"] = "Редактировать",
                ["Delete"] = "Удалить",
                ["Save"] = "Сохранить",
                ["Cancel"] = "Отмена",
                ["Close"] = "Закрыть",
                ["Export"] = "📤 Экспорт (JSON)",
                ["Import"] = "📥 Импорт (JSON)",
                ["Backup_Now"] = "💾 Создать Резервную Копию",
                ["Restore_Backup"] = "📂 Восстановить из Копии",
                ["Check_Updates"] = "🔄 Проверить Обновления",
                ["Open_Logs"] = "📁 Открыть Папку Логов",
                ["Copy_Diag"] = "📋 Копировать Диагностику",
                ["Reset_Factory"] = "⚠️ Сбросить к Заводским Настройкам",
                ["Language"] = "🌐 Язык Интерфейса",
                ["Language_Desc"] = "Выберите предпочитаемый язык интерфейса.",
                ["Startup_Title"] = "Автозапуск Windows",
                ["Startup_Check"] = "Запускать Radial Launcher в трее при старте Windows",
                ["Trigger_Title"] = "Клавиша Активации",
                ["Trigger_Desc"] = "Выберите кнопку мыши или клавиатуры для вызова меню.",
                ["Assign_Shortcut"] = "🎯 Назначить Свою Клавишу",
                ["Opacity_Title"] = "Прозрачность Меню",
                ["Opacity_Desc"] = "Настройте уровень прозрачности фона.",
                ["Density_Title"] = "Плотность Кольца",
                ["Density_Desc"] = "Количество элементов на одной странице кольца.",
                ["Density_Expanded"] = "Расширенный (15 Элементов)",
                ["Density_Compact"] = "Компактный (18 Элементов)",
                ["Reduce_Motion"] = "Упростить Анимации",
                ["Palette_Title"] = "Палитра Цветов Темы",
                ["Primary_Accent"] = "Основной Акцент",
                ["Secondary_Accent"] = "Дополнительный Акцент",
                ["Background"] = "Фон",
                ["Icon_Bubble"] = "Пузырек Иконки",
                ["Play"] = "▶ Играть",
                ["Store"] = "🛒 Магазин",
                ["Community"] = "👥 Сообщество",
                ["Location"] = "📁 Расположение",
                ["Run_Admin"] = "⚡ Запуск от Имени Администратора",
                ["Status_Ready"] = "Готово.",
                ["Status_Saved"] = "Настройки успешно сохранены.",
                ["Installed_Version"] = "Установленная Версия: v1.0.0"
            };
        }
    }
}
