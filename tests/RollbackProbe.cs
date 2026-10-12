// Standalone .NET Framework runner: one published DLL per process, no rebuilt plugin.
// Usage: RollbackProbe.exe plugin.dll repaired-settings.json http://127.0.0.1:PORT
// The supplied proxy must serve ALL responses locally. Do not point it at a real service.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;

internal static class RollbackProbe
{
    private static string _storage;
    private static Dictionary<int, string> _tags;
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    public static string Tag(string url, int field) { string text; return _tags.TryGetValue(field, out text) ? text : null; }
    public static string NowTag(int field) { return null; }
    public static string Property(string url, int field) { return "6:08"; }
    public static string Storage() { return _storage; }
    public static void Trace(string text) { Console.WriteLine(text); }

    private static Delegate Handler(Type type, string method)
    {
        var invoke = type.GetMethod("Invoke");
        var parameters = invoke.GetParameters().Select(p => Expression.Parameter(p.ParameterType)).ToArray();
        var target = typeof(RollbackProbe).GetMethod(method);
        var arguments = parameters.Select((p, i) => (Expression)Expression.Convert(p, target.GetParameters()[i].ParameterType)).ToArray();
        return Expression.Lambda(type, Expression.Call(target, arguments), parameters).Compile();
    }

    private static int Main(string[] args)
    {
        try
        {
            var proxy = new Uri(args[2]);
            if (!proxy.IsLoopback || proxy.Scheme != "http") throw new ArgumentException("Fixture proxy must be local HTTP");
            WebRequest.DefaultWebProxy = new WebProxy(proxy);
            // Only this test process trusts the fixture certificate; system trust is untouched.
            ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, errors) => true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            _storage = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1])), "rollback-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_storage);
            File.Copy(args[1], Path.Combine(_storage, "netease_config"));
            var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            var pluginType = assembly.GetType("MusicBeePlugin.Plugin", true);
            var plugin = Activator.CreateInstance(pluginType);
            var tagType = assembly.GetType("MusicBeePlugin.Plugin+MetaDataType");
            _tags = new Dictionary<int, string>();
            _tags[(int)Enum.Parse(tagType, "TrackTitle")] = "鳥の詩";
            _tags[(int)Enum.Parse(tagType, "Artist")] = "Lia";
            _tags[(int)Enum.Parse(tagType, "Album")] = "AIR ORIGINAL SOUNDTRACK";
            var apiField = pluginType.GetField("_mbApiInterface", All);
            var api = apiField.GetValue(plugin);
            foreach (var pair in new[] { new[] { "Library_GetFileTag", "Tag" }, new[] { "NowPlaying_GetFileTag", "NowTag" },
                new[] { "Library_GetFileProperty", "Property" }, new[] { "Setting_GetPersistentStoragePath", "Storage" }, new[] { "MB_Trace", "Trace" } })
            {
                var field = api.GetType().GetField(pair[0]); field.SetValue(api, Handler(field.FieldType, pair[1]));
            }
            apiField.SetValue(plugin, api);
            var read = pluginType.GetMethod("ReadConfig", All);
            if (read != null) read.Invoke(plugin, null);
            else
            {
                // v1.8.1 loads this exact JSON through Json.NET inside Initialise.
                var configField = pluginType.GetField("_config", All);
                var json = Assembly.Load("Newtonsoft.Json").GetType("Newtonsoft.Json.JsonConvert");
                var deserialize = json.GetMethod("DeserializeObject", new[] { typeof(string), typeof(Type) });
                configField.SetValue(plugin, deserialize.Invoke(null, new object[] { File.ReadAllText(args[1]), configField.FieldType }));
            }
            var config = pluginType.GetField("_config", All).GetValue(plugin);
            foreach (var field in new[] { "Fuzzy", "UseLegacyMatch" })
            {
                var property = config.GetType().GetProperties().FirstOrDefault(p => p.Name.Equals(field, StringComparison.OrdinalIgnoreCase));
                if (property != null && (bool)property.GetValue(config, null)) throw new Exception("Unsafe legacy flag " + field);
            }
            var provider = ((string[])pluginType.GetMethod("GetProviders").Invoke(plugin, null))[0];
            var result = (string)pluginType.GetMethod("RetrieveLyrics").Invoke(plugin,
                new object[] { "fixture.flac", "Lia", "鳥の詩", "AIR ORIGINAL SOUNDTRACK", true, provider });
            Console.WriteLine("RESULT " + assembly.GetName().Version + ": " + result);
            return result != null && result.Contains("CORRECT") && !result.Contains("WRONG") ? 0 : 1;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 2; }
    }
}
