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
    public class NeteaseConfig
    {
        public enum AccompanyingText { Translation = 0, Pronunciation = 1, PronunciationAndTranslation = 2 }
        public bool EnableCantonesePronunciation { get; set; }
        public AccompanyingText JapaneseText { get; set; }
        public AccompanyingText KoreanText { get; set; }
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
        private CheckBox _allowHttpCheckBox;
        private CheckBox _cantoneseCheckBox;
        private ComboBox _japaneseComboBox, _koreanComboBox;
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
            _about.Name = "Netease Lyrics";
            _about.Description = "A plugin to retrieve lyrics from Netease Cloud Music.(从网易云音乐获取歌词的插件。)";
            _about.Description += " Build " + Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            _about.Author = "Charlie Jiang";
            _about.TargetApplication = "";   // current only applies to artwork, lyrics or instant messenger name that appears in the provider drop down selector or target Instant Messenger
            _about.Type = PluginType.LyricsRetrieval;
            _about.VersionMajor = short.Parse(versions[0]);  // your plugin version
            _about.VersionMinor = short.Parse(versions[1]);
            _about.Revision = short.Parse(versions[2]);
            _about.MinInterfaceVersion = MinInterfaceVersion;
            _about.MinApiRevision = MinApiRevision;
            _about.ReceiveNotifications = ReceiveNotificationFlags.DownloadEvents;
            _about.ConfigurationPanelHeight = 275;

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
            configPanel.Controls.Clear();

            // MB_AddPanel doesn't skin the component correctly either
            //_formatComboBox = (ComboBox)_mbApiInterface.MB_AddPanel(null, PluginPanelDock.ComboBox);
            _formatComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                AutoSize = true,
                Location = new Point(0, 0),
                Width = 390
            };
            _formatComboBox.Items.Add("Only original text");
            _formatComboBox.Items.Add("Original and extra text (same line: JP / CN)");
            _formatComboBox.Items.Add("Only translation");
            _formatComboBox.Items.Add("Original and extra text (separate lines)");
            _formatComboBox.SelectedIndex = (int)_config.Format;
            configPanel.Controls.Add(_formatComboBox);

            _cantoneseCheckBox = new CheckBox { Text = "Cantonese: enable pronunciation", Location = new Point(0, 35),
                Checked = _config.EnableCantonesePronunciation, AutoSize = true };
            configPanel.Controls.Add(_cantoneseCheckBox);
            _japaneseComboBox = AddLanguageChoice(configPanel, "Japanese:", 65, _config.JapaneseText);
            _koreanComboBox = AddLanguageChoice(configPanel, "Korean:", 95, _config.KoreanText);
            configPanel.Controls.Add(new Label { Text = "Combined formats only. Re-fetch lyrics after changing.",
                Location = new Point(0, 130), AutoSize = true });
            EventHandler updateLanguageOptions = (sender, e) =>
            {
                var combined = _formatComboBox.SelectedIndex == 1 || _formatComboBox.SelectedIndex == 3;
                _cantoneseCheckBox.Enabled = _japaneseComboBox.Enabled = _koreanComboBox.Enabled = combined;
            };
            _formatComboBox.SelectedIndexChanged += updateLanguageOptions;
            updateLanguageOptions(null, EventArgs.Empty);

            _useLegacyCheckBox = new CheckBox
            {
                Text = "Customize spelling matching",
                Location = new Point(0, 165),
                Checked = _config.UseLegacyMatch,
                AutoSize = true
            };

            _fuzzyCheckBox = new CheckBox
            {
                Text = "Allow small spelling differences",
                Location = new Point(0, 195),
                Checked = _config.Fuzzy,
                Enabled = _config.UseLegacyMatch,
                AutoSize = true
            };

            _useLegacyCheckBox.CheckedChanged += (sender, e) =>
            {
                // Without customization, the default permits small spelling differences.
                _fuzzyCheckBox.Enabled = _useLegacyCheckBox.Checked;
            };

            configPanel.Controls.Add(_useLegacyCheckBox);
            configPanel.Controls.Add(_fuzzyCheckBox);
            _allowHttpCheckBox = new CheckBox
            {
                Text = "Allow HTTP fallback (unencrypted metadata)",
                Location = new Point(0, 225),
                Checked = _config.AllowInsecureHttp,
                AutoSize = true
            };
            configPanel.Controls.Add(_allowHttpCheckBox);

            return false;
        }

        private static ComboBox AddLanguageChoice(Panel panel, string label, int y, NeteaseConfig.AccompanyingText value)
        {
            panel.Controls.Add(new Label { Text = label, Location = new Point(0, y + 3), AutoSize = true });
            var choice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(90, y), Width = 300 };
            choice.Items.AddRange(new object[] { "Translation", "Pronunciation", "Pronunciation & Translation" });
            choice.SelectedIndex = Enum.IsDefined(typeof(NeteaseConfig.AccompanyingText), value) ? (int)value : 0;
            panel.Controls.Add(choice);
            return choice;
        }

        // called by MusicBee when the user clicks Apply or Save in the MusicBee Preferences screen.
        // It's up to you to figure out whether anything has changed and needs updating
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public void SaveSettings()
        {
            if (_formatComboBox == null || _formatComboBox.IsDisposed ||
                _fuzzyCheckBox == null || _fuzzyCheckBox.IsDisposed ||
                _useLegacyCheckBox == null || _useLegacyCheckBox.IsDisposed) return;
            if (_formatComboBox.SelectedIndex < 0 || _formatComboBox.SelectedIndex >= _formatComboBox.Items.Count)
                _config.Format = NeteaseConfig.OutputFormat.BothTwoLines;
            else
                _config.Format = (NeteaseConfig.OutputFormat)_formatComboBox.SelectedIndex;
            _config.Fuzzy = _fuzzyCheckBox.Checked;
            _config.UseLegacyMatch = _useLegacyCheckBox.Checked;
            if (_allowHttpCheckBox != null && !_allowHttpCheckBox.IsDisposed)
                _config.AllowInsecureHttp = _allowHttpCheckBox.Checked;
            if (_cantoneseCheckBox != null && !_cantoneseCheckBox.IsDisposed) _config.EnableCantonesePronunciation = _cantoneseCheckBox.Checked;
            if (_japaneseComboBox != null && !_japaneseComboBox.IsDisposed)
                _config.JapaneseText = ValidChoice(_japaneseComboBox.SelectedIndex);
            if (_koreanComboBox != null && !_koreanComboBox.IsDisposed)
                _config.KoreanText = ValidChoice(_koreanComboBox.SelectedIndex);
            SaveSettingsInternal();
        }

        private static NeteaseConfig.AccompanyingText ValidChoice(int value) =>
            Enum.IsDefined(typeof(NeteaseConfig.AccompanyingText), value) ? (NeteaseConfig.AccompanyingText)value : NeteaseConfig.AccompanyingText.Translation;

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
                var stored = JObject.FromObject(_config);
                stored["TwoLineTranslation"] = _config.Format == NeteaseConfig.OutputFormat.BothTwoLines;
                if (_config.Format == NeteaseConfig.OutputFormat.BothTwoLines) stored["Format"] = 1;
                File.WriteAllText(temporaryPath, stored.ToString(Formatting.None), Encoding.UTF8);
                if (File.Exists(configPath)) File.Replace(temporaryPath, configPath, null);
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
                        var choice = NeteaseConfig.AccompanyingText.Translation;
                        var combined = _config.Format == NeteaseConfig.OutputFormat.Both || _config.Format == NeteaseConfig.OutputFormat.BothTwoLines;
                        if (combined && (_config.EnableCantonesePronunciation || _config.JapaneseText != choice || _config.KoreanText != choice))
                            choice = LanguagePreferences.Choice(_config, _languagePreferences.Resolve(candidate, attempt, session));
                        var formatted = FormatLyrics(attempt, choice, session.Report, out var usableExtras);
                        if (usableExtras > bestExtras) { result = formatted; bestExtras = usableExtras; }
                        var wanted = choice == NeteaseConfig.AccompanyingText.PronunciationAndTranslation ? 2 : 1;
                        if (_config.Format == NeteaseConfig.OutputFormat.Original || usableExtras >= wanted) break;
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
            var translation = result.tlyric?.lyric;
            if (_config.Format == NeteaseConfig.OutputFormat.Original)
                return original;
            if (_config.Format == NeteaseConfig.OutputFormat.Translation)
            {
                usableExtras = LyricProcessor.HasLyricText(translation) ? 1 : 0;
                return usableExtras == 1 ? translation : original;
            }
            if (choice != NeteaseConfig.AccompanyingText.Translation)
                return LyricProcessor.MergePronunciation(original, result.romalrc?.lyric,
                    choice == NeteaseConfig.AccompanyingText.PronunciationAndTranslation ? translation : null,
                    _config.Format == NeteaseConfig.OutputFormat.BothTwoLines, trace, out usableExtras);
            usableExtras = LyricProcessor.HasLyricText(translation) ? 1 : 0;
            if (usableExtras == 0) return original;
            var merged = LyricProcessor.MergeTranslation(original, translation,
                _config.Format == NeteaseConfig.OutputFormat.BothTwoLines, trace);
            // Untimed lyrics cannot be aligned; preserve them rather than returning an empty string.
            return string.IsNullOrWhiteSpace(merged) ? original : merged;
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
