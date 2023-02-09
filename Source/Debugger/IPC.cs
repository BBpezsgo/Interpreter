using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Communicating
{
    public class InterProcessCommunication
    {
        readonly List<IPCMessage<object>> OutgoingMessages = new();

        public delegate void OnRecivedEventHandler(InterProcessCommunication sender, IPCMessage<object> message);
        public event OnRecivedEventHandler OnRecived;

        readonly Interface @interface;
        internal Interface.Type CommunicationType => @interface.CommunicationType;

        void Log(string v)
        {
            switch (CommunicationType)
            {
                case Interface.Type.Standard:
                    IngameCoding.Output.File.WriteLine(v);
                    break;
                case Interface.Type.Pipe:
                    Console.WriteLine(v);
                    break;
                case Interface.Type.Socket:
                    Console.WriteLine(v);
                    break;
            }
        }

        public InterProcessCommunication()
        {
            this.@interface = new Interface();
        }

        public void Start()
        {
            this.@interface.OnRecived += OnRecive;
            this.@interface.Start();
        }

        private void OnRecive(string data)
        {
            var message = JsonSerializer.Deserialize<IPCMessage<object>>(data.Trim());

            if (message.type == "base/ping/req")
            {
                Reply("base/ping/res", Math.Round(DateTime.Now.ToUnix()).ToString(), message.id);
                return;
            }

            if (message.type == "base/ping/res")
            {
                return;
            }

            OnRecived?.Invoke(this, message);
        }

        public void Reply<T>(string messageType, T messageData, string reply)
        {
            OutgoingMessages.Add(new IPCMessage<object>(messageType, null, messageData, reply));
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
                @interface.Send(JsonSerializer.Serialize(OutgoingMessages[0]));
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
        internal delegate void OnRecivedEventHandler(string message);
        internal event OnRecivedEventHandler OnRecived;

        static readonly char EOM = Convert.ToChar(4);
        Thread Listener;
        const int BufferSize = 1024;
        string Incoming = "";

        internal enum Type
        {
            Standard,
            Pipe,
            Socket,
        }

        internal Type CommunicationType => Type.Standard; // PipeName != null ? Type.Pipe : Port != -1 ? Type.Socket : Type.Standard;

        void Log(string v)
        {
            switch (CommunicationType)
            {
                case Type.Standard:
                    IngameCoding.Output.File.WriteLine(v);
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
            var outputWriter = new StreamWriter(@out, outEncoding);

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
                        OnDataRecived(data);

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

            outputWriter?.Close();
            outputWriter?.Dispose();

            @out?.Close();
            @out?.Dispose();

            @in?.Close();
            @in?.Dispose();
        }

        void OnDataRecived(string data)
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
                OnRecived?.Invoke(message);
            }
        }
    }

    public class IPCMessage<T>
    {
        public string type { get; set; }
        public string id { get; set; }
        public string reply { get; set; }
        public T data { get; set; }

        public IPCMessage(string type, string id, T data, string reply = null)
        {
            this.id = id;
            this.type = type;
            this.data = data;
            this.reply = reply;
        }

        public string Serialize() => JsonSerializer.Serialize(this);
        public static IPCMessage<T> Deserialize(string data) => JsonSerializer.Deserialize<IPCMessage<T>>(data);

        public override string ToString() => $"IPCMessage<{typeof(T)}>{{ type: {type ?? "<null>"}, id: {id ?? "<null>"}, reply: {reply ?? "<null>"}, data: {{{data.ToString() ?? "<null>"}}} }}";
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
                deleted = "";
                return v;
            }

            deleted = v[..length];
            return v[length..];
        }
    }
}
