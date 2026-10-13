using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using System.Globalization;
using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("NeteaseLyricsTest")]

namespace MusicBeePlugin
{
    public partial class NeteaseConfig
    {
        // Retain existing stored values; the UI orders Original first without renumbering them.
        public enum AccompanyingText { Translation = 0, Pronunciation = 1, PronunciationAndTranslation = 2, Original = 3 }
        public bool EnableCantonesePronunciation { get; set; } = false;
        public AccompanyingText JapaneseText { get; set; } = AccompanyingText.Translation;
        public AccompanyingText KoreanText { get; set; } = AccompanyingText.Translation;
        public enum OutputFormat
        {
            Original = 0,
            Both = 1,
            Translation = 2,
            BothTwoLines = 3
        }

        public OutputFormat Format { get; set; } = OutputFormat.BothTwoLines;
        public bool? TwoLineTranslation { get; set; }
        public bool Fuzzy { get; set; }
        public bool UseLegacyMatch { get; set; }
        public bool AllowInsecureHttp { get; set; } = true;
        [OnDeserialized]
        private void RestoreTwoLineFormat(StreamingContext context)
        {
            if (Format == OutputFormat.Both && TwoLineTranslation == true) Format = OutputFormat.BothTwoLines;
        }
    }

    public partial class Plugin
    {
        private const string ProviderName = "Netease Cloud Music(网易云音乐)";
        private const string ConfigFilename = "netease_config";
        private const string NoTranslateFilename = "netease_notranslate";
        private NeteaseConfig _config = new NeteaseConfig();
        private NeteaseConfig _settingsDraft;
        private ComboBox _formatComboBox;
        private CheckBox _fuzzyCheckBox;
        private CheckBox _allowHttpCheckBox;
        private CheckBox _translationCheckBox, _pronunciationCheckBox;
        private ComboBox _interfaceLanguageComboBox, _cantoneseComboBox, _japaneseComboBox, _koreanComboBox, _englishComboBox;
        private readonly LanguagePreferences _languagePreferences = new LanguagePreferences();

        private MusicBeeApiInterface _mbApiInterface;
        private readonly PluginInfo _about = new PluginInfo();
        private readonly Func<LookupSession> _sessionFactory;
        internal static string BuildVersion => Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        internal static int SettingsPanelHeight => 630;
        internal static bool IsDevelopmentVersion(string version) => VersionRules.IsDevelopment(version);
        internal static string BuildDescription => "NetEase lyrics. Build " + BuildVersion +
            (IsDevelopmentVersion(BuildVersion) ? " (DEV BUILD)" : "");
        private readonly DevelopmentDiagnostics _diagnostics = new DevelopmentDiagnostics();
        internal Action<string> DiagnosticClipboardWriter { get; set; } = text => Clipboard.SetText(text);

        public Plugin()
        {
            _sessionFactory = () => new LookupSession(Trace);
            UpdateChecker = new UpdateChecker(trace: Trace);
        }

        internal Plugin(MusicBeeApiInterface api, Func<LookupSession> sessionFactory) : this()
        {
            _mbApiInterface = api;
            _sessionFactory = sessionFactory;
        }

        private void Trace(string message)
        {
            try { _mbApiInterface.MB_Trace?.Invoke("[NeteaseMusic] " + message); }
            catch { /* Host diagnostics may be unavailable during shutdown. */ }
        }

        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public PluginInfo Initialise(IntPtr apiInterfacePtr)
        {
            var versions = Assembly.GetExecutingAssembly().GetName().Version.ToString().Split('.');

            _mbApiInterface = new MusicBeeApiInterface();
            _mbApiInterface.Initialise(apiInterfacePtr);
            _about.PluginInfoVersion = PluginInfoVersion;
            _about.Name = "LyricBee — NetEase Lyrics";
            _about.Description = BuildDescription;
            _about.Author = "Tony Y; original plugin by Charlie Jiang";
            _about.TargetApplication = "";   // current only applies to artwork, lyrics or instant messenger name that appears in the provider drop down selector or target Instant Messenger
            _about.Type = PluginType.LyricsRetrieval;
            _about.VersionMajor = short.Parse(versions[0]);  // your plugin version
            _about.VersionMinor = short.Parse(versions[1]);
            _about.Revision = short.Parse(versions[2]);
            _about.MinInterfaceVersion = MinInterfaceVersion;
            _about.MinApiRevision = MinApiRevision;
            _about.ReceiveNotifications = ReceiveNotificationFlags.DownloadEvents;
            _about.ConfigurationPanelHeight = SettingsPanelHeight;

            ReadConfig();
            MigrateLegacySetting();
            return _about;
        }

        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public bool Configure(IntPtr panelHandle)
        {
            _settingsDraft = _config.Copy();
            return RenderSettings(panelHandle);
        }

        private bool RenderSettings(IntPtr panelHandle)
        {
            if (panelHandle == IntPtr.Zero) return false;
            var configPanel = (Panel)Control.FromHandle(panelHandle);
            if (configPanel == null) return false;
            // Components are automatically disposed when this is called.
            foreach (Control control in configPanel.Controls.Cast<Control>().ToArray()) control.Dispose();
            configPanel.Controls.Clear();

            configPanel.AutoScrollPosition = Point.Empty;
            var config = _settingsDraft;
            string T(SettingsLabel label) => SettingsText.Get(label, config.InterfaceLanguage);
            var layout = new SettingsLayout(configPanel);
            Label Caption(string name, SettingsLabel caption, bool heading = false)
            {
                var label = new Label { Name = name, Text = T(caption), AutoSize = true, ForeColor = configPanel.ForeColor };
                if (heading)
                {
                    var font = new Font(configPanel.Font, FontStyle.Bold);
                    label.Font = font;
                    EventHandler changedFont = (sender, e) =>
                    {
                        if (configPanel.Disposing || configPanel.IsDisposed || label.Disposing || label.IsDisposed) return;
                        var replacement = new Font(configPanel.Font, FontStyle.Bold);
                        label.Font = replacement; font.Dispose(); font = replacement;
                    };
                    configPanel.FontChanged += changedFont;
                    label.Disposed += (sender, e) => { configPanel.FontChanged -= changedFont; font.Dispose(); };
                }
                return label;
            }
            _interfaceLanguageComboBox = new ComboBox { Name = "InterfaceLanguage", DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
            _interfaceLanguageComboBox.Items.AddRange(new object[] { "Automatic / 自动", "English", "简体中文" });
            _interfaceLanguageComboBox.SelectedIndex = (int)config.InterfaceLanguage;
            layout.Inline(Caption("InterfaceLanguageLabel", SettingsLabel.InterfaceLanguage), _interfaceLanguageComboBox, 9);
            layout.Add(Caption("DefaultDisplayHeading", SettingsLabel.Defaults, true), 4);
            layout.Add(Caption("OriginalAlways", SettingsLabel.OriginalAlways), 4);
            _translationCheckBox = new CheckBox { Name = "ShowTranslation", Text = T(SettingsLabel.Translation),
                AutoSize = true, Checked = NeteaseConfig.HasTranslation(config.DefaultContent), ForeColor = configPanel.ForeColor };
            _pronunciationCheckBox = new CheckBox { Name = "ShowPronunciation", Text = T(SettingsLabel.Pronunciation),
                AutoSize = true, Checked = NeteaseConfig.HasPronunciation(config.DefaultContent), ForeColor = configPanel.ForeColor };
            layout.Extras(_translationCheckBox, _pronunciationCheckBox);
            _formatComboBox = new ComboBox { Name = "OutputLayout", DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
            _formatComboBox.Items.AddRange(new object[] { T(SettingsLabel.SeparateLines), T(SettingsLabel.SameLine) });
            _formatComboBox.SelectedIndex = config.UseSeparateLines ? 0 : 1;
            layout.Pair(Caption("LayoutLabel", SettingsLabel.Layout), _formatComboBox, 11);
            layout.Add(Caption("LanguageOverridesHeading", SettingsLabel.Overrides, true), 6);
            _japaneseComboBox = AddLanguageChoice(layout, SettingsLabel.Japanese, "JapaneseContent", config.JapaneseOverride);
            _koreanComboBox = AddLanguageChoice(layout, SettingsLabel.Korean, "KoreanContent", config.KoreanOverride);
            _cantoneseComboBox = AddLanguageChoice(layout, SettingsLabel.Cantonese, "CantoneseContent", config.CantoneseOverride, cantonese: true);
            _englishComboBox = AddLanguageChoice(layout, SettingsLabel.English, "EnglishContent", config.EnglishOverride, english: true, gap: 9);
            var note = Caption("RefetchHint", SettingsLabel.Refetch);
            layout.Add(note, 8, 38);
            layout.Add(Caption("MatchingNetworkHeading", SettingsLabel.MatchingNetwork, true), 6);
            _fuzzyCheckBox = new CheckBox { Name = "SmallSpellingDifferences", Text = T(SettingsLabel.SmallDifferences),
                Checked = config.AllowSpelling, AutoSize = true, ForeColor = configPanel.ForeColor };
            layout.Add(_fuzzyCheckBox, 10);
            _allowHttpCheckBox = new CheckBox { Name = "AllowHttpFallback", Text = T(SettingsLabel.HttpFallback),
                Checked = config.AllowInsecureHttp, AutoSize = true, ForeColor = configPanel.ForeColor };
            layout.Add(_allowHttpCheckBox, 14);
            AddUpdateControls(layout, T);
            var advanced = new LinkLabel { Name = "AdvancedDiagnostics", Text = T(SettingsLabel.Advanced), AutoSize = true,
                LinkColor = configPanel.ForeColor, ActiveLinkColor = configPanel.ForeColor, VisitedLinkColor = configPanel.ForeColor };
            layout.Add(advanced, 4);
            var copyDiagnostics = new Button { Name = "CopyDiagnosticReport", Text = T(SettingsLabel.CopyDiagnostics),
                AutoSize = true, Visible = false, Enabled = _diagnostics.HasLookups };
            layout.Add(copyDiagnostics, 7);
            var diagnosticNote = Caption("DiagnosticPrivacyNote", SettingsLabel.DiagnosticPrivacy);
            diagnosticNote.Visible = false; layout.Add(diagnosticNote, 0, 44);
            var advancedOpen = false;
            advanced.LinkClicked += (sender, e) =>
            {
                advancedOpen = !advancedOpen;
                copyDiagnostics.Visible = advancedOpen; diagnosticNote.Visible = advancedOpen;
                advanced.Text = T(advancedOpen ? SettingsLabel.HideAdvanced : SettingsLabel.Advanced); layout.Reflow();
            };
            copyDiagnostics.Click += (sender, e) =>
            {
                try
                {
                    if (!_diagnostics.HasLookups) { diagnosticNote.Text = T(SettingsLabel.DiagnosticEmpty); return; }
                    if (System.Threading.Thread.CurrentThread.GetApartmentState() != System.Threading.ApartmentState.STA)
                        throw new InvalidOperationException("Clipboard requires the UI thread");
                    DiagnosticClipboardWriter(_diagnostics.Report());
                    diagnosticNote.Text = T(SettingsLabel.DiagnosticCopied) + " " + T(SettingsLabel.DiagnosticPrivacy);
                }
                catch { diagnosticNote.Text = T(SettingsLabel.DiagnosticCopyFailed); }
            };
            var diagnosticTimer = new System.Windows.Forms.Timer { Interval = 500 };
            diagnosticTimer.Tick += (sender, e) => { if (!copyDiagnostics.IsDisposed) copyDiagnostics.Enabled = _diagnostics.HasLookups; };
            copyDiagnostics.Disposed += (sender, e) => { diagnosticTimer.Stop(); diagnosticTimer.Dispose(); };
            diagnosticTimer.Start(); layout.Reflow();
            _interfaceLanguageComboBox.SelectedIndexChanged += (sender, e) =>
            {
                CaptureSettings();
                foreach (Control control in configPanel.Controls.Cast<Control>().ToArray()) control.Dispose();
                RenderSettings(panelHandle);
            };
            return false;
        }

        private static readonly NeteaseConfig.AccompanyingText[] ContentChoices = {
            NeteaseConfig.AccompanyingText.Original, NeteaseConfig.AccompanyingText.Translation,
            NeteaseConfig.AccompanyingText.Pronunciation, NeteaseConfig.AccompanyingText.PronunciationAndTranslation };

        private ComboBox AddLanguageChoice(SettingsLayout layout, SettingsLabel caption, string name,
            NeteaseConfig.AccompanyingText? value, bool english = false, bool cantonese = false, int gap = 3)
        {
            var labelControl = new Label { Name = name + "Label", Text = SettingsText.Get(caption, _settingsDraft.InterfaceLanguage),
                AutoSize = true, ForeColor = layout.Host.ForeColor };
            var choice = new ComboBox { Name = name, DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
            var values = cantonese ? new[] { NeteaseConfig.AccompanyingText.Original, NeteaseConfig.AccompanyingText.Pronunciation } :
                english ? ContentChoices.Take(2).ToArray() : ContentChoices;
            choice.Tag = values;
            choice.Items.Add(SettingsText.Get(SettingsLabel.FollowDefault, _settingsDraft.InterfaceLanguage));
            foreach (var content in values)
            {
                var label = content == NeteaseConfig.AccompanyingText.Original ? SettingsLabel.Original :
                    content == NeteaseConfig.AccompanyingText.Translation ? SettingsLabel.OriginalTranslation :
                    content == NeteaseConfig.AccompanyingText.Pronunciation ? SettingsLabel.OriginalPronunciation : SettingsLabel.AllThree;
                choice.Items.Add(SettingsText.Get(label, _settingsDraft.InterfaceLanguage));
            }
            choice.SelectedIndex = value.HasValue ? Array.IndexOf(values, value.Value) + 1 : 0;
            layout.Pair(labelControl, choice, gap);
            return choice;
        }

        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public void SaveSettings()
        {
            if (!CaptureSettings()) return;
            _config = _settingsDraft.Copy();
            SaveSettingsInternal();
        }

        private bool CaptureSettings()
        {
            if (_formatComboBox == null || _formatComboBox.IsDisposed ||
                _translationCheckBox == null || _translationCheckBox.IsDisposed ||
                _pronunciationCheckBox == null || _pronunciationCheckBox.IsDisposed ||
                _fuzzyCheckBox == null || _fuzzyCheckBox.IsDisposed) return false;
            var config = _settingsDraft;
            config.ShowTranslation = _translationCheckBox.Checked;
            config.ShowPronunciation = _pronunciationCheckBox.Checked;
            config.SeparateLines = _formatComboBox.SelectedIndex != 1;
            config.DisplaySettingsVersion = 1;
            config.InterfaceLanguage = _interfaceLanguageComboBox.SelectedIndex >= 0 ?
                (NeteaseConfig.UiLanguage)_interfaceLanguageComboBox.SelectedIndex : NeteaseConfig.UiLanguage.Automatic;
            config.CantoneseOverride = SelectedContent(_cantoneseComboBox);
            config.JapaneseOverride = SelectedContent(_japaneseComboBox);
            config.KoreanOverride = SelectedContent(_koreanComboBox);
            config.EnglishOverride = SelectedContent(_englishComboBox);
            config.AllowSmallSpellingDifferences = _fuzzyCheckBox.Checked;
            config.Fuzzy = config.UseLegacyMatch = false;
            config.AllowInsecureHttp = _allowHttpCheckBox.Checked;
            return true;
        }

        private static NeteaseConfig.AccompanyingText? SelectedContent(ComboBox control) =>
            control.SelectedIndex > 0 && control.SelectedIndex < control.Items.Count ?
                (NeteaseConfig.AccompanyingText?)((NeteaseConfig.AccompanyingText[])control.Tag)[control.SelectedIndex - 1] : null;

        private static NeteaseConfig.AccompanyingText ReadChoice(JToken value) =>
            value != null && (value.Type == JTokenType.Integer || value.Type == JTokenType.String) &&
            Enum.TryParse(value.ToString(), true, out NeteaseConfig.AccompanyingText choice) &&
            Enum.IsDefined(typeof(NeteaseConfig.AccompanyingText), choice) ? choice : NeteaseConfig.AccompanyingText.Translation;

        private bool SaveSettingsInternal()
        {
            string temporaryPath = null;
            try
            {
                var configPath = Path.Combine(_mbApiInterface.Setting_GetPersistentStoragePath(), ConfigFilename);
                temporaryPath = configPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                var stored = _config.StoredDisplaySettings();
                File.WriteAllText(temporaryPath, stored.ToString(Formatting.None), Encoding.UTF8);
                if (File.Exists(configPath))
                {
                    var backup = configPath + ".pre-display.bak";
                    var legacy = true;
                    try { legacy = JObject.Parse(File.ReadAllText(configPath, Encoding.UTF8))["DisplaySettingsVersion"]?.ToString() != "1"; }
                    catch (JsonException) { /* Preserve malformed settings as well before replacing them. */ }
                    if (legacy && !File.Exists(backup)) File.Copy(configPath, backup);
                    File.Replace(temporaryPath, configPath, null);
                }
                else File.Move(temporaryPath, configPath);
                return true;
            }
            catch (Exception ex)
            {
                Trace("Failed to save config: " + ex.GetType().Name);
                return false;
            }
            finally
            {
                try { if (temporaryPath != null && File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                catch { /* Retain the old configuration even if temporary-file cleanup fails. */ }
            }
        }

        // MusicBee is closing the plugin (plugin is being disabled by user or MusicBee is shutting down)
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        [SuppressMessage("ReSharper", "UnusedParameter.Global")]
        public void Close(PluginCloseReason reason)
        {
            _diagnostics.Clear();
        }

        // uninstall this plugin - clean up any persisted files
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public void Uninstall()
        {
            var dataPath = _mbApiInterface.Setting_GetPersistentStoragePath();
            var p = Path.Combine(dataPath, NoTranslateFilename);
            if (File.Exists(p)) File.Delete(p);
            var configPath = Path.Combine(_mbApiInterface.Setting_GetPersistentStoragePath(), ConfigFilename);
            if (File.Exists(configPath)) File.Delete(configPath);
            foreach (var suffix in new[] { ".pre9.bak", ".pre-display.bak", ".pre-spelling.bak" })
                if (File.Exists(configPath + suffix)) File.Delete(configPath + suffix);
            foreach (var temporary in Directory.EnumerateFiles(dataPath, ConfigFilename + ".*.tmp"))
                if (Regex.IsMatch(Path.GetFileName(temporary), @"^netease_config\.[a-fA-F0-9]{32}\.tmp$")) File.Delete(temporary);
        }

        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        [SuppressMessage("ReSharper", "UnusedParameter.Global")]
        public string RetrieveLyrics(string sourceFileUrl, 
            string artist, string trackTitle, string album,
            bool synchronisedPreferred, string provider)
        {
            if (provider != ProviderName) return null;
            var config = _config;
            var diagnosticLookup = _diagnostics.Begin(config);
            var diagnosticStatus = DiagnosticLookupStatus.NoLyrics;
            try
            {
                // Only the requested file owns this override, even during background library scans.
                var id = TryParseNeteaseUrl(ReadTag(sourceFileUrl, MetaDataType.Custom10));
                var realTitle = FirstNonBlank(ReadTag(sourceFileUrl, MetaDataType.TrackTitle), trackTitle);
                if (id == 0 && string.IsNullOrWhiteSpace(realTitle))
                {
                    Trace("Lookup skipped: no title or song ID");
                    diagnosticLookup.Add(DiagnosticKind.NoTitle);
                    return null;
                }
                using (var session = _sessionFactory())
                {
                    session.DiagnosticLookup = diagnosticLookup;
                    if (id > 0) session.Diagnostic(DiagnosticKind.Custom10Override, songId: id);
                    session.AllowInsecureHttp = config.AllowInsecureHttp;
                    var candidates = id > 0 ? new[] { id } : new long[0];
                    if (id == 0)
                    {
                        var realArtist = FirstNonBlank(ReadTag(sourceFileUrl, MetaDataType.Artist), artist);
                        var realAlbum = FirstNonBlank(ReadTag(sourceFileUrl, MetaDataType.Album), album);
                        string durationStr = null;
                        try { durationStr = _mbApiInterface.Library_GetFileProperty?.Invoke(sourceFileUrl, FilePropertyType.Duration); }
                        catch (Exception ex) { Trace("Failed to read duration: " + ex.GetType().Name); }
                        candidates = SearchMatch.SearchCandidates(realTitle, realArtist, realAlbum,
                            ParseDurationString(durationStr), session, config.AllowSpelling,
                            ReadTag(sourceFileUrl, MetaDataType.TrackNo));
                    }
                    string result = null;
                    var bestExtras = -1;
                    var extraAttempts = 0;
                    foreach (var candidate in candidates)
                    {
                        // Missing originals get all three candidates; missing extras get one more listing.
                        if (result != null)
                        {
                            if (extraAttempts >= 1)
                            {
                                session.Report("Lyrics: extra-content retry limit reached; retained available text");
                                session.Diagnostic(DiagnosticKind.ExtrasRetryLimit);
                                break;
                            }
                            extraAttempts++;
                        }
                        if (candidate != candidates[0]) session.Report("Lyrics: trying compatible song ID " + candidate);
                        if (candidate != candidates[0]) session.Diagnostic(DiagnosticKind.RetryCompatibleListing, songId: candidate);
                        var attempt = NeteaseApi.RequestLyric(candidate, session);
                        if (NeteaseApi.IsInstrumental(attempt))
                        {
                            session.Diagnostic(DiagnosticKind.InstrumentalListing, songId: candidate);
                            if (id > 0 || candidates.Length == 1) diagnosticStatus = result == null ? DiagnosticLookupStatus.Instrumental : DiagnosticLookupStatus.Lyrics;
                            if (id > 0 || candidates.Length == 1) return result;
                            continue;
                        }
                        if (!LyricProcessor.HasLyricText(attempt?.lrc?.lyric))
                        {
                            session.Diagnostic(DiagnosticKind.OriginalMissing, songId: candidate);
                            continue;
                        }
                        var choice = config.DefaultContent;
                        if (config.NeedsLanguageChoice)
                            choice = LanguagePreferences.Choice(config, _languagePreferences.Resolve(candidate, attempt, session,
                                // Changed content overrides also apply to songs without a pronunciation track.
                                new[] { config.CantoneseOverride, config.JapaneseOverride, config.KoreanOverride, config.EnglishOverride }
                                    .AnyDifferent(config.DefaultContent)));
                        var formatted = FormatLyrics(attempt, choice, config.UseSeparateLines, session.Report, out var usableExtras);
                        session.Diagnostic(DiagnosticKind.AuxiliaryOutput, songId: candidate, count: usableExtras);
                        if (usableExtras > bestExtras) { result = formatted; bestExtras = usableExtras; }
                        var wanted = choice == NeteaseConfig.AccompanyingText.Original ? 0 :
                            choice == NeteaseConfig.AccompanyingText.PronunciationAndTranslation ? 2 : 1;
                        if (NeteaseConfig.HasTranslation(choice) && !LyricProcessor.HasLyricText(attempt.tlyric?.lyric) &&
                            _languagePreferences.TryGetKnownLanguage(candidate, out var knownLanguage) && knownLanguage == SongLanguage.Mandarin)
                        {
                            session.Report("Lyrics: known Mandarin label; skipped missing-translation retries");
                            session.Diagnostic(DiagnosticKind.CachedMandarin);
                            wanted = NeteaseConfig.HasPronunciation(choice) ? 1 : 0;
                        }
                        if (usableExtras >= wanted) break;
                    }
                    diagnosticStatus = result == null ? DiagnosticLookupStatus.NoLyrics : DiagnosticLookupStatus.Lyrics;
                    return result;
                }
            }
            catch (Exception ex)
            {
                Trace("Lookup failed: " + ex.GetType().Name);
                diagnosticLookup.Add(DiagnosticKind.LookupException); diagnosticStatus = DiagnosticLookupStatus.Error;
                return null;
            }
            finally { diagnosticLookup.Complete(diagnosticStatus); }
        }

        private string FormatLyrics(LyricResult result, NeteaseConfig.AccompanyingText choice, bool separateLines, Action<string> trace, out int usableExtras)
        {
            usableExtras = 0;
            var original = result?.lrc?.lyric;
            if (string.IsNullOrWhiteSpace(original)) return null;
            if (choice == NeteaseConfig.AccompanyingText.Original) return original;
            return LyricProcessor.MergePronunciation(original,
                NeteaseConfig.HasPronunciation(choice) ? result.romalrc?.lyric : null,
                NeteaseConfig.HasTranslation(choice) ? result.tlyric?.lyric : null,
                separateLines, trace, out usableExtras);
        }

        private string ReadTag(string source, MetaDataType tag)
        {
            try { return _mbApiInterface.Library_GetFileTag?.Invoke(source, tag); }
            catch (Exception ex) { Trace("Failed to read tag: " + ex.GetType().Name); return null; }
        }

        private static string FirstNonBlank(string tag, string callback)
        {
            return (string.IsNullOrWhiteSpace(tag) ? callback : tag)?.Trim() ?? "";
        }

        internal void ReadConfig()
        {
            try
            {
                var configPath = Path.Combine(_mbApiInterface.Setting_GetPersistentStoragePath(), ConfigFilename);
                if (!File.Exists(configPath)) return;
                var configText = File.ReadAllText(configPath, Encoding.UTF8);
                // Bad new preferences must not discard older valid settings, especially HTTP opt-out.
                var storedConfig = JObject.Parse(configText);
                var displayConfig = (JObject)storedConfig.DeepClone();
                foreach (var key in new[] { "DisplaySettingsVersion", "ShowTranslation", "ShowPronunciation", "SeparateLines", "InterfaceLanguage",
                    "CantoneseOverride", "JapaneseOverride", "KoreanOverride", "EnglishOverride", "AllowSmallSpellingDifferences" }) storedConfig.Remove(key);
                var japanese = storedConfig["JapaneseText"];
                var korean = storedConfig["KoreanText"];
                var cantonese = storedConfig["EnableCantonesePronunciation"];
                storedConfig.Remove("JapaneseText"); storedConfig.Remove("KoreanText"); storedConfig.Remove("EnableCantonesePronunciation");
                var config = storedConfig.ToObject<NeteaseConfig>();
                _config = config ?? new NeteaseConfig();
                if (!Enum.IsDefined(typeof(NeteaseConfig.OutputFormat), _config.Format))
                    _config.Format = NeteaseConfig.OutputFormat.BothTwoLines;
                _config.JapaneseText = ReadChoice(japanese);
                _config.KoreanText = ReadChoice(korean);
                _config.EnableCantonesePronunciation = cantonese != null &&
                    (cantonese.Type == JTokenType.Boolean || cantonese.Type == JTokenType.String) &&
                    bool.TryParse(cantonese.ToString(), out var enabled) && enabled;
                _config.LoadDisplaySettings(displayConfig);
                var repairSpelling = displayConfig["AllowSmallSpellingDifferences"] == null || _config.Fuzzy || _config.UseLegacyMatch;
                var repairFormat = storedConfig["Format"]?.ToString() == "3";
                if (repairSpelling || repairFormat)
                {
                    try
                    {
                        // Make all required backups before the first write; failure retains loaded preferences.
                        foreach (var suffix in new[] { repairSpelling ? ".pre-spelling.bak" : null, repairFormat ? ".pre9.bak" : null }.Where(s => s != null))
                            if (!File.Exists(configPath + suffix)) File.Copy(configPath, configPath + suffix);
                        if (SaveSettingsInternal()) Trace("Settings: repaired rollback storage; backups retained");
                    }
                    catch (Exception ex) { Trace("Settings rollback repair deferred: " + ex.GetType().Name); }
                }
            }
            catch (Exception ex)
            {
                _config = new NeteaseConfig();
                Trace("Failed to load config: " + ex.GetType().Name);
            }
        }

        internal void MigrateLegacySetting()
        {
            try
            {
                var noTranslatePath = Path.Combine(_mbApiInterface.Setting_GetPersistentStoragePath(), NoTranslateFilename);
                if (!File.Exists(noTranslatePath)) return;
                _config.Format = NeteaseConfig.OutputFormat.Original;
                _config.ShowTranslation = _config.ShowPronunciation = false;
                _config.CantoneseOverride = _config.JapaneseOverride = _config.KoreanOverride = _config.EnglishOverride = null;
                if (SaveSettingsInternal()) File.Delete(noTranslatePath);
            }
            catch (Exception ex) { Trace("Failed to migrate legacy config: " + ex.GetType().Name); }
        }

        internal static long TryParseNeteaseUrl(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0;
            input = input.Trim();
            if (input.StartsWith("netease=", StringComparison.OrdinalIgnoreCase))
            {
                return PositiveId(input.Substring("netease=".Length).Trim());
            }
            if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
                (!string.Equals(uri.Host, "music.163.com", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(uri.Host, "y.music.163.com", StringComparison.OrdinalIgnoreCase))) return 0;
            var route = string.IsNullOrEmpty(uri.Fragment) ? uri.PathAndQuery : uri.Fragment.Substring(1);
            var match = Regex.Match(route, @"^/(?:m/)?song\?(?:[^&#]*&)*id=([0-9]+)(?:&[^#]*)?$", RegexOptions.IgnoreCase);
            return match.Success ? PositiveId(match.Groups[1].Value) : 0;
        }

        private static long PositiveId(string value)
        {
            return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : 0;
        }

        internal static long? ParseDurationString(string durationStr)
        {
            if (string.IsNullOrWhiteSpace(durationStr)) return null;
            var parts = durationStr.Trim().Split(':');
            if (parts.Length > 3) return null;
            decimal seconds = 0;
            for (var index = 0; index < parts.Length; index++)
            {
                var style = index == parts.Length - 1 ? NumberStyles.AllowDecimalPoint : NumberStyles.None;
                if (!decimal.TryParse(parts[index], style, CultureInfo.InvariantCulture, out var part) ||
                    part < 0 || (index > 0 && part >= 60)) return null;
                // Guard before multiplying, including unusually large or corrupt tag values.
                if (seconds > long.MaxValue / 60000m || part > long.MaxValue / 1000m) return null;
                seconds = seconds * 60 + part;
            }
            var milliseconds = decimal.Round(seconds * 1000, 0, MidpointRounding.AwayFromZero);
            return milliseconds > 0 && milliseconds <= long.MaxValue ? (long?)milliseconds : null;
        }

        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public string[] GetProviders()
        {
            return new []{ProviderName};
        }
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public void ReceiveNotification(string sourceFileUrl, NotificationType type)
        {
        }
    }
}
