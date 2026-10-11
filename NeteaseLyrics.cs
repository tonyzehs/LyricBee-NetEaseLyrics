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
        private ComboBox _formatComboBox;
        private CheckBox _fuzzyCheckBox;
        private CheckBox _useLegacyCheckBox;
        private Label _spellingDefaultLabel;
        private CheckBox _allowHttpCheckBox;
        private CheckBox _translationCheckBox, _pronunciationCheckBox;
        private ComboBox _interfaceLanguageComboBox, _cantoneseComboBox, _japaneseComboBox, _koreanComboBox, _englishComboBox;
        private readonly LanguagePreferences _languagePreferences = new LanguagePreferences();

        private MusicBeeApiInterface _mbApiInterface;
        private readonly PluginInfo _about = new PluginInfo();
        private readonly Func<LookupSession> _sessionFactory;

        public Plugin()
        {
            _sessionFactory = () => new LookupSession(Trace);
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
            _about.Description = "NetEase lyrics. Build " + Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            _about.Author = "Tony Y; original plugin by Charlie Jiang";
            _about.TargetApplication = "";   // current only applies to artwork, lyrics or instant messenger name that appears in the provider drop down selector or target Instant Messenger
            _about.Type = PluginType.LyricsRetrieval;
            _about.VersionMajor = short.Parse(versions[0]);  // your plugin version
            _about.VersionMinor = short.Parse(versions[1]);
            _about.Revision = short.Parse(versions[2]);
            _about.MinInterfaceVersion = MinInterfaceVersion;
            _about.MinApiRevision = MinApiRevision;
            _about.ReceiveNotifications = ReceiveNotificationFlags.DownloadEvents;
            _about.ConfigurationPanelHeight = 455;

            ReadConfig();
            MigrateLegacySetting();
            return _about;
        }

        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public bool Configure(IntPtr panelHandle)
        {
            if (panelHandle == IntPtr.Zero) return false;
            var configPanel = (Panel)Control.FromHandle(panelHandle);
            if (configPanel == null) return false;
            // Components are automatically disposed when this is called.
            foreach (Control control in configPanel.Controls.Cast<Control>().ToArray()) control.Dispose();
            configPanel.Controls.Clear();

            string T(SettingsLabel label) => SettingsText.Get(label, _config.InterfaceLanguage);
            void Label(string name, SettingsLabel caption, int y, bool heading = false)
            {
                var label = new Label { Name = name, Text = T(caption), Location = new Point(0, y),
                    AutoSize = true, ForeColor = configPanel.ForeColor };
                if (heading)
                {
                    var font = new Font(configPanel.Font, FontStyle.Bold);
                    label.Font = font;
                    label.Disposed += (sender, e) => font.Dispose();
                }
                configPanel.Controls.Add(label);
            }
            Label("InterfaceLanguageLabel", SettingsLabel.InterfaceLanguage, 3);
            _interfaceLanguageComboBox = new ComboBox { Name = "InterfaceLanguage", DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(130, 0), Width = 260 };
            _interfaceLanguageComboBox.Items.AddRange(new object[] { "Automatic / 自动", "English", "简体中文" });
            _interfaceLanguageComboBox.SelectedIndex = (int)_config.InterfaceLanguage;
            configPanel.Controls.Add(_interfaceLanguageComboBox);

            Label("DefaultDisplayHeading", SettingsLabel.Defaults, 36, true);
            Label("OriginalAlways", SettingsLabel.OriginalAlways, 58);
            _translationCheckBox = new CheckBox { Name = "ShowTranslation", Text = T(SettingsLabel.Translation),
                Location = new Point(0, 80), AutoSize = true, Checked = NeteaseConfig.HasTranslation(_config.DefaultContent), ForeColor = configPanel.ForeColor };
            _pronunciationCheckBox = new CheckBox { Name = "ShowPronunciation", Text = T(SettingsLabel.Pronunciation),
                Location = new Point(160, 80), AutoSize = true, Checked = NeteaseConfig.HasPronunciation(_config.DefaultContent), ForeColor = configPanel.ForeColor };
            configPanel.Controls.Add(_translationCheckBox); configPanel.Controls.Add(_pronunciationCheckBox);
            Label("LayoutLabel", SettingsLabel.Layout, 111);
            _formatComboBox = new ComboBox { Name = "OutputLayout", DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(130, 108), Width = 260 };
            _formatComboBox.Items.AddRange(new object[] { T(SettingsLabel.SeparateLines), T(SettingsLabel.SameLine) });
            _formatComboBox.SelectedIndex = _config.UseSeparateLines ? 0 : 1;
            configPanel.Controls.Add(_formatComboBox);

            Label("LanguageOverridesHeading", SettingsLabel.Overrides, 146, true);
            _cantoneseComboBox = AddLanguageChoice(configPanel, SettingsLabel.Cantonese, "CantoneseContent", 170, _config.CantoneseOverride);
            _japaneseComboBox = AddLanguageChoice(configPanel, SettingsLabel.Japanese, "JapaneseContent", 200, _config.JapaneseOverride);
            _koreanComboBox = AddLanguageChoice(configPanel, SettingsLabel.Korean, "KoreanContent", 230, _config.KoreanOverride);
            _englishComboBox = AddLanguageChoice(configPanel, SettingsLabel.English, "EnglishContent", 260, _config.EnglishOverride, true);
            var note = new Label { Name = "RefetchHint", Text = T(SettingsLabel.Refetch), Location = new Point(0, 296),
                Size = new Size(430, 38), ForeColor = configPanel.ForeColor, AutoSize = false };
            configPanel.Controls.Add(note);
            Label("MatchingNetworkHeading", SettingsLabel.MatchingNetwork, 342, true);
            _useLegacyCheckBox = new CheckBox { Name = "CustomizeSpelling", Text = T(SettingsLabel.CustomizeSpelling),
                Location = new Point(0, 366), Checked = _config.UseLegacyMatch, AutoSize = true, ForeColor = configPanel.ForeColor };
            _fuzzyCheckBox = new CheckBox { Name = "SmallSpellingDifferences", Text = T(SettingsLabel.SmallDifferences),
                Location = new Point(18, 390), Checked = _config.Fuzzy, AutoSize = true, ForeColor = configPanel.ForeColor };
            _spellingDefaultLabel = new Label { Name = "SpellingDefault", Text = T(SettingsLabel.DefaultSpelling),
                Location = new Point(18, 390), AutoSize = true, ForeColor = configPanel.ForeColor };
            EventHandler updateSpellingOption = (sender, e) =>
            {
                _fuzzyCheckBox.Enabled = _fuzzyCheckBox.Visible = _useLegacyCheckBox.Checked;
                _spellingDefaultLabel.Visible = !_useLegacyCheckBox.Checked;
            };
            _useLegacyCheckBox.CheckedChanged += updateSpellingOption;
            updateSpellingOption(null, EventArgs.Empty);
            configPanel.Controls.Add(_useLegacyCheckBox); configPanel.Controls.Add(_fuzzyCheckBox); configPanel.Controls.Add(_spellingDefaultLabel);
            _allowHttpCheckBox = new CheckBox { Name = "AllowHttpFallback", Text = T(SettingsLabel.HttpFallback),
                Location = new Point(0, 420), Checked = _config.AllowInsecureHttp, AutoSize = true, ForeColor = configPanel.ForeColor };
            configPanel.Controls.Add(_allowHttpCheckBox);

            Action resize = () =>
            {
                foreach (var combo in configPanel.Controls.OfType<ComboBox>())
                    combo.Width = Math.Max(140, Math.Min(360, configPanel.ClientSize.Width - combo.Left - 8));
                note.Width = Math.Max(200, configPanel.ClientSize.Width - 8);
            };
            // The handler owns only this set of controls; remove it when rebuilding the language UI.
            EventHandler resized = (sender, e) => resize();
            configPanel.SizeChanged += resized;
            note.Disposed += (sender, e) => configPanel.SizeChanged -= resized;
            resize();
            _interfaceLanguageComboBox.SelectedIndexChanged += (sender, e) =>
            {
                CaptureSettings();
                foreach (Control control in configPanel.Controls.Cast<Control>().ToArray()) control.Dispose();
                Configure(panelHandle);
            };
            return false;
        }

        private static readonly NeteaseConfig.AccompanyingText[] ContentChoices = {
            NeteaseConfig.AccompanyingText.Original, NeteaseConfig.AccompanyingText.Translation,
            NeteaseConfig.AccompanyingText.Pronunciation, NeteaseConfig.AccompanyingText.PronunciationAndTranslation };

        private ComboBox AddLanguageChoice(Panel panel, SettingsLabel caption, string name, int y,
            NeteaseConfig.AccompanyingText? value, bool english = false)
        {
            panel.Controls.Add(new Label { Text = SettingsText.Get(caption, _config.InterfaceLanguage),
                Location = new Point(0, y + 3), AutoSize = true, ForeColor = panel.ForeColor });
            var choice = new ComboBox { Name = name, DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(130, y), Width = 300 };
            var labels = new[] { SettingsLabel.FollowDefault, SettingsLabel.Original, SettingsLabel.OriginalTranslation,
                SettingsLabel.OriginalPronunciation, SettingsLabel.AllThree };
            choice.Items.AddRange(labels.Take(english ? 3 : 5).Select(l => (object)SettingsText.Get(l, _config.InterfaceLanguage)).ToArray());
            choice.SelectedIndex = value.HasValue ? Array.IndexOf(ContentChoices, value.Value) + 1 : 0;
            choice.DropDownWidth = Math.Max(choice.Width, choice.Items.Cast<string>()
                .Max(s => TextRenderer.MeasureText(s, panel.Font).Width) + SystemInformation.VerticalScrollBarWidth + 12);
            panel.Controls.Add(choice);
            return choice;
        }

        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public void SaveSettings()
        {
            if (!CaptureSettings()) return;
            SaveSettingsInternal();
        }

        private bool CaptureSettings()
        {
            if (_formatComboBox == null || _formatComboBox.IsDisposed ||
                _translationCheckBox == null || _translationCheckBox.IsDisposed ||
                _pronunciationCheckBox == null || _pronunciationCheckBox.IsDisposed ||
                _useLegacyCheckBox == null || _useLegacyCheckBox.IsDisposed) return false;
            _config.ShowTranslation = _translationCheckBox.Checked;
            _config.ShowPronunciation = _pronunciationCheckBox.Checked;
            _config.SeparateLines = _formatComboBox.SelectedIndex != 1;
            _config.DisplaySettingsVersion = 1;
            _config.InterfaceLanguage = _interfaceLanguageComboBox.SelectedIndex >= 0 ?
                (NeteaseConfig.UiLanguage)_interfaceLanguageComboBox.SelectedIndex : NeteaseConfig.UiLanguage.Automatic;
            _config.CantoneseOverride = SelectedContent(_cantoneseComboBox);
            _config.JapaneseOverride = SelectedContent(_japaneseComboBox);
            _config.KoreanOverride = SelectedContent(_koreanComboBox);
            _config.EnglishOverride = SelectedContent(_englishComboBox);
            _config.Fuzzy = _fuzzyCheckBox.Checked;
            _config.UseLegacyMatch = _useLegacyCheckBox.Checked;
            _config.AllowInsecureHttp = _allowHttpCheckBox.Checked;
            return true;
        }

        private static NeteaseConfig.AccompanyingText? SelectedContent(ComboBox control) =>
            control.SelectedIndex > 0 && control.SelectedIndex < control.Items.Count ?
                (NeteaseConfig.AccompanyingText?)ContentChoices[control.SelectedIndex - 1] : null;

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
        }

        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        [SuppressMessage("ReSharper", "UnusedParameter.Global")]
        public string RetrieveLyrics(string sourceFileUrl, 
            string artist, string trackTitle, string album,
            bool synchronisedPreferred, string provider)
        {
            if (provider != ProviderName) return null;
            try
            {
                // Only the requested file owns this override, even during background library scans.
                var id = TryParseNeteaseUrl(ReadTag(sourceFileUrl, MetaDataType.Custom10));
                var realTitle = FirstNonBlank(ReadTag(sourceFileUrl, MetaDataType.TrackTitle), trackTitle);
                if (id == 0 && string.IsNullOrWhiteSpace(realTitle))
                {
                    Trace("Lookup skipped: no title or song ID");
                    return null;
                }
                using (var session = _sessionFactory())
                {
                    session.AllowInsecureHttp = _config.AllowInsecureHttp;
                    var candidates = id > 0 ? new[] { id } : new long[0];
                    if (id == 0)
                    {
                        var realArtist = FirstNonBlank(ReadTag(sourceFileUrl, MetaDataType.Artist), artist);
                        var realAlbum = FirstNonBlank(ReadTag(sourceFileUrl, MetaDataType.Album), album);
                        string durationStr = null;
                        try { durationStr = _mbApiInterface.Library_GetFileProperty?.Invoke(sourceFileUrl, FilePropertyType.Duration); }
                        catch (Exception ex) { Trace("Failed to read duration: " + ex.GetType().Name); }
                        candidates = SearchMatch.SearchCandidates(realTitle, realArtist, realAlbum,
                            ParseDurationString(durationStr), session, !_config.UseLegacyMatch || _config.Fuzzy);
                    }
                    string result = null;
                    var bestExtras = -1;
                    foreach (var candidate in candidates)
                    {
                        if (candidate != candidates[0]) session.Report("Lyrics: trying compatible song ID " + candidate);
                        var attempt = NeteaseApi.RequestLyric(candidate, session);
                        // An explicit instrumental is authoritative; do not replace it with another listing's words.
                        if (NeteaseApi.IsInstrumental(attempt)) return result;
                        if (!LyricProcessor.HasLyricText(attempt?.lrc?.lyric)) continue;
                        var choice = _config.DefaultContent;
                        if (_config.NeedsLanguageChoice)
                            choice = LanguagePreferences.Choice(_config, _languagePreferences.Resolve(candidate, attempt, session,
                                // Changed content overrides also apply to songs without a pronunciation track.
                                new[] { _config.CantoneseOverride, _config.JapaneseOverride, _config.KoreanOverride, _config.EnglishOverride }
                                    .AnyDifferent(_config.DefaultContent)));
                        var formatted = FormatLyrics(attempt, choice, session.Report, out var usableExtras);
                        if (usableExtras > bestExtras) { result = formatted; bestExtras = usableExtras; }
                        var wanted = choice == NeteaseConfig.AccompanyingText.Original ? 0 :
                            choice == NeteaseConfig.AccompanyingText.PronunciationAndTranslation ? 2 : 1;
                        if (usableExtras >= wanted) break;
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                Trace("Lookup failed: " + ex.GetType().Name);
                return null;
            }
        }

        private string FormatLyrics(LyricResult result, NeteaseConfig.AccompanyingText choice, Action<string> trace, out int usableExtras)
        {
            usableExtras = 0;
            var original = result?.lrc?.lyric;
            if (string.IsNullOrWhiteSpace(original)) return null;
            if (choice == NeteaseConfig.AccompanyingText.Original) return original;
            return LyricProcessor.MergePronunciation(original,
                NeteaseConfig.HasPronunciation(choice) ? result.romalrc?.lyric : null,
                NeteaseConfig.HasTranslation(choice) ? result.tlyric?.lyric : null,
                _config.UseSeparateLines, trace, out usableExtras);
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
                    "CantoneseOverride", "JapaneseOverride", "KoreanOverride", "EnglishOverride" }) storedConfig.Remove(key);
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
                if (storedConfig["Format"]?.ToString() == "3")
                {
                    try
                    {
                        // Migration failures must not discard valid loaded preferences, including HTTP opt-out.
                        var backup = configPath + ".pre9.bak";
                        if (!File.Exists(backup)) File.Copy(configPath, backup);
                        if (SaveSettingsInternal()) Trace("Settings: migrated two-line preference to rollback-safe storage; backup retained");
                    }
                    catch (Exception ex) { Trace("Settings migration deferred: " + ex.GetType().Name); }
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
