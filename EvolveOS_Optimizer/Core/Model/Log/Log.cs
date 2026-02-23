using System.Globalization;
using System.Runtime.CompilerServices;
using EvolveOS_Optimizer.Core.Interfaces;

namespace EvolveOS_Optimizer.Core.Model.Log
{
    public class Log : IJsonSerializable
    {
        public Log(Enums.Log.Levels level, string message, ILogData? data = null, [CallerMemberName] string? method = null, DateTimeOffset? timestamp = null)
        {
            Data = data;
            Level = level;
            Method = method ?? "Unknown";
            Message = message;
            Timestamp = timestamp ?? DateTimeOffset.Now;
        }

        public ILogData? Data { get; private set; }

        public Enums.Log.Levels Level { get; private set; }

        public string Message { get; private set; }

        public string Method { get; private set; }

        public DateTimeOffset Timestamp { get; private set; }

        public object ToJson()
        {
            var jsonSerializable = Data as IJsonSerializable;
            var data = jsonSerializable?.ToJson();

            var level = Level.ToString();
            var timestamp = Timestamp.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture);

            return data == null
                ? new { timestamp, level, method = Method, message = Message, data = (object?)null }
                : (object)new { timestamp, level, method = Method, message = Message, data };
        }

        public override string ToString()
        {
            return string.Format("[{0}] {1}: {2}", Timestamp.ToString("HH:mm:ss"), Level, Message);
        }
    }
}