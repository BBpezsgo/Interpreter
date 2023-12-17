using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

#pragma warning disable CA1822 // Mark members as static

#nullable disable

namespace Communicating
{
    [RequiresDynamicCode("Uses System.Text.Json.JsonSerializer")]
    [RequiresUnreferencedCode("Uses System.Text.Json.JsonSerializer")]
    public class InterProcessCommunication
    {
        readonly List<IPCMessage<object>> OutgoingMessages = new();

        public delegate void OnReceivedEventHandler(InterProcessCommunication sender, IPCMessage<object> message);
        public event OnReceivedEventHandler OnReceived;

        readonly Interface @interface;
        public Interface.Type CommunicationType => @interface.CommunicationType;

        public static readonly JsonSerializerOptions SerializerOptions = new()
        {

        };

        public void Log(string v) => @interface.Log(v);

        public InterProcessCommunication()
        {
            this.@interface = new Interface();
        }

        public void Start()
        {
            this.@interface.OnReceived += OnReceive;
            this.@interface.Start();
        }

        private void OnReceive(string data)
        {
            IPCMessage<object> message = JsonSerializer.Deserialize<IPCMessage<object>>(data.Trim(), SerializerOptions);

            if (message.Type == "base/ping/req")
            {
                Reply("base/ping/res", Math.Round(DateTime.Now.ToUnix()).ToString(CultureInfo.InvariantCulture), message.Id);
                return;
            }

            if (message.Type == "base/ping/res")
            {
                return;
            }

            OnReceived?.Invoke(this, message);
        }

        public void Reply<T>(string messageType, T messageData, string replyToId)
        {
            OutgoingMessages.Add(new IPCMessage<object>(messageType, null, messageData, replyToId));
            TrySendNext();
        }

        public void Reply<T, T2>(string messageType, T messageData, IPCMessage<T2> replyTo)
        {
            OutgoingMessages.Add(new IPCMessage<object>(messageType, null, messageData, replyTo.Id));
            TrySendNext();
        }

        public void Send<T>(string messageType, T messageData)
        {
            OutgoingMessages.Add(new IPCMessage<object>(messageType, null, messageData));
            TrySendNext();
        }

        void TrySendNext()
        {
            if (OutgoingMessages.Count == 0) return;

            try
            {
                @interface.Send(JsonSerializer.Serialize(OutgoingMessages[0], SerializerOptions));
            }
            catch (System.Exception error)
            {
                Console.Error.WriteLine($"ERROR:\r\n{error}\r\nMESSAGE:\r\n{OutgoingMessages[0]}\r\n\r\n");
                throw;
            }

            OutgoingMessages.RemoveAt(0);
        }
    }

    public class Interface
    {
        public delegate void OnReceivedEventHandler(string message);
        public event OnReceivedEventHandler OnReceived;

        static readonly char EOM = Convert.ToChar(4);
        Thread Listener;
        const int BufferSize = 1024;
        string Incoming = string.Empty;

        public enum Type
        {
            Standard,
            Pipe,
            Socket,
        }

        public Type CommunicationType => Type.Standard; // PipeName != null ? Type.Pipe : Port != -1 ? Type.Socket : Type.Standard;

        public void Log(string v)
        {
            switch (CommunicationType)
            {
                case Type.Standard:
                    // LanguageCore.ProgrammingLanguage.Output.File.WriteLine(v);
                    break;
                case Type.Pipe:
                    Console.WriteLine(v);
                    break;
                case Type.Socket:
                    Console.WriteLine(v);
                    break;
            }
        }

        readonly Queue<string> Outgoing = new();

        public void Start()
        {
            Listener = new Thread(ListenerThread);
            Listener.Start();
            Listener.Join();
        }

        public void Send(string data)
        {
            Outgoing.Enqueue(data + EOM);
        }

        void ListenerThread()
        {
            Log($"Communication[Standard] Opened");
            Listen(Console.OpenStandardInput(BufferSize), Console.OpenStandardOutput(), Console.InputEncoding, Console.OutputEncoding);
            Log($"Communication[Standard] Closed");
        }

        void Listen(Stream @in, Stream @out, Encoding inEncoding, Encoding outEncoding)
        {
            StreamWriter outputWriter = new(@out, outEncoding);

            while (@in.CanRead && @out.CanWrite)
            {
                try
                {
                    byte[] buffer = new byte[BufferSize];
                    int length;
                    while ((length = @in.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        byte[] payload = new byte[length];
                        Buffer.BlockCopy(buffer, 0, payload, 0, length);
                        string data = inEncoding.GetString(payload).Trim();
                        OnDataReceived(data);

                        while (Outgoing.Count > 0)
                        {
                            string data_ = Outgoing.Dequeue();
                            outputWriter.Write(data_);
                            outputWriter.Flush();
                            Log($" >> {data_}");
                        }
                    }
                }
                catch (System.Exception error)
                {
                    Console.Error.WriteLine(error.ToString());
                    break;
                }
            }

            Log($"CanWrite: {@out.CanWrite} CanRead: {@in.CanRead}");

            outputWriter.Close();
            outputWriter.Dispose();

            @out?.Close();
            @out?.Dispose();

            @in?.Close();
            @in?.Dispose();
        }

        void OnDataReceived(string data)
        {
            Incoming += data;

            Incoming = Incoming.TrimStart(EOM);

            int endlessSafe = 8;
            while (data.Contains(EOM))
            {
                if (endlessSafe-- <= 0) { Console.Error.WriteLine($"Endless loop!!!"); break; }

                Incoming = Incoming.Shift(Incoming.IndexOf(EOM), out string message);
                if (string.IsNullOrWhiteSpace(message) || string.IsNullOrEmpty(message)) break;

                if (message.Contains(EOM))
                {
                    Console.Error.WriteLine($" WTF: {message}");
                    continue;
                }
                Log($" << {message}");
                OnReceived?.Invoke(message);
            }
        }
    }

    [RequiresDynamicCode("Uses System.Text.Json.JsonSerializer")]
    [RequiresUnreferencedCode("Uses System.Text.Json.JsonSerializer")]
    public class IPCMessage<T>
    {
        [JsonInclude, JsonPropertyName("type")]
        public string Type;
        [JsonInclude, JsonPropertyName("id")]
        public string Id;
        [JsonInclude, JsonPropertyName("reply")]
        public string Reply;
        [JsonInclude, JsonPropertyName("data")]
        public T Data;

        public IPCMessage(string type, string id, T data, string reply = null)
        {
            this.Id = id;
            this.Type = type;
            this.Data = data;
            this.Reply = reply;
        }

        public string Serialize() => JsonSerializer.Serialize(this, InterProcessCommunication.SerializerOptions);
        public static IPCMessage<T> Deserialize(string data) => JsonSerializer.Deserialize<IPCMessage<T>>(data, InterProcessCommunication.SerializerOptions);

        public override string ToString() => $"IPCMessage<{typeof(T)}>{{ type: {Type ?? "null"}, id: {Id ?? "null"}, reply: {Reply ?? "null"}, data: {{{Data.ToString() ?? "null"}}} }}";
    }

    static class Extensions
    {
        public static double ToUnix(this DateTime v) => v.ToUniversalTime().Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        public static string Shift(this string v, int length, out string deleted)
        {
            if (v is null) throw new ArgumentNullException(nameof(v));
            if (length < 0) throw new ArgumentException($"{nameof(length)} ({length}) can't negative");
            if (length == 0)
            {
                deleted = string.Empty;
                return v;
            }

            deleted = v[..length];
            return v[length..];
        }
    }
}
