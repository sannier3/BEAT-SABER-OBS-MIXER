using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace OBSMixer.Core
{
    public sealed class ObsRequestException : Exception
    {
        public int Code { get; }
        public ObsRequestException(string request, int code) : base(request + " rejected (" + code + ")") { Code = code; }
    }

    public static class ObsProtocol
    {
        public const int Subscriptions = 1 | 2 | 4 | 8 | 128 | (1 << 16);
        public const string MonitorOff = "OBS_MONITORING_TYPE_NONE";
        public const string MonitorOnly = "OBS_MONITORING_TYPE_MONITOR_ONLY";
        public const string MonitorOutput = "OBS_MONITORING_TYPE_MONITOR_AND_OUTPUT";
        public static string Authenticate(string password, string salt, string challenge)
        {
            using (var sha = SHA256.Create())
            {
                var secret = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(password + salt)));
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(secret + challenge)));
            }
        }
        public static JObject Envelope(int op, JObject data) => new JObject { ["op"] = op, ["d"] = data };
        public static JObject Identify(JObject hello, string password)
        {
            if (((int?)hello["rpcVersion"] ?? 0) < 1) throw new InvalidOperationException("Unsupported OBS RPC version");
            var data = new JObject { ["rpcVersion"] = 1, ["eventSubscriptions"] = Subscriptions };
            if (hello["authentication"] is JObject auth)
                data["authentication"] = Authenticate(password, (string)auth["salt"], (string)auth["challenge"]);
            return Envelope(1, data);
        }
        public static double ToDb(double amplitude) => amplitude > 0 ? 20 * Math.Log10(amplitude) : -100;
        public static string Key(JObject input) => (string)input["inputUuid"] ?? (string)input["inputName"];
        public static JObject Selector(InputState input) => new JObject { ["inputName"] = input.Name };
    }

    public sealed class InputState
    {
        public string Key { get; set; }
        public string Uuid { get; set; }
        public string Name { get; set; }
        public double VolumeDb { get; set; }
        public bool Muted { get; set; }
        public long OffsetMs { get; set; }
        public JObject Tracks { get; set; } = new JObject();
        public string MonitorType { get; set; } = ObsProtocol.MonitorOff;
        public bool Track(int number) => (bool?)Tracks[number.ToString()] ?? false;
        public InputState Clone() => new InputState { Key = Key, Uuid = Uuid, Name = Name, VolumeDb = VolumeDb,
            Muted = Muted, OffsetMs = OffsetMs, Tracks = (JObject)Tracks.DeepClone(), MonitorType = MonitorType };
    }

    public sealed class MeterState
    {
        public double LeftMagnitude, LeftPeak, RightMagnitude, RightPeak;
        public DateTime ReceivedUtc;
        public int Channels;
        public static MeterState Parse(JArray channels)
        {
            double Read(int channel, int value) => channels.Count > channel && channels[channel] is JArray c && c.Count > value
                ? Math.Max(0, (double?)c[value] ?? 0) : 0;
            int right = channels.Count > 1 ? 1 : 0;
            return new MeterState { Channels = channels.Count, LeftMagnitude = Read(0, 0), LeftPeak = Read(0, 1),
                RightMagnitude = Read(right, 0), RightPeak = Read(right, 1), ReceivedUtc = DateTime.UtcNow };
        }
    }
}
