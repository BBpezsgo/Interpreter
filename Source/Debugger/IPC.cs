using IngameCoding.Bytecode;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

using TheProgram;

namespace Communicating
{
    public class InterProcessCommunication
    {
        readonly List<IPCMessage<object>> OutgoingMessages = new();

        static readonly char EOM = Convert.ToChar(4);

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
            this.@interface.OnRecived += Interface_OnRecived;
            this.@interface.Start();
        }

        private void Interface_OnRecived(string data)
        {
            if (data.Contains(EOM))
            {
                var messages = data.Split(EOM);
                foreach (var message in messages)
                {
                    if (string.IsNullOrWhiteSpace(message)) continue;
                    if (string.IsNullOrEmpty(message)) continue;
                    var messageObject = JsonSerializer.Deserialize<IPCMessage<object>>(message.Trim());
                    OnRecive(messageObject);
                }
            }
            else
            {
                IPCMessage<object> messageObject = JsonSerializer.Deserialize<IPCMessage<object>>(data);
                OnRecive(messageObject);
            }
        }

        void OnRecive(IPCMessage<object> message)
        {
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
                var data = JsonSerializer.Serialize(OutgoingMessages[0]);
                this.@interface.Send(data + EOM);
            }
            catch (IngameCoding.Errors.RuntimeException)
            {
                if (OutgoingMessages[0].data is Data_CompilerResult data0)
                {
                    for (int i = 0; i < data0.CompiledCode.Length; i++)
                    {
                        if (data0.CompiledCode[i].Opcode == Opcode.COMMENT.ToString()) continue;
                        if (data0.CompiledCode[i].Parameter is int) continue;
                        if (data0.CompiledCode[i].Parameter is float) continue;
                        if (data0.CompiledCode[i].Parameter is string) continue;
                        if (data0.CompiledCode[i].Parameter is bool) continue;
                        Log(data0.CompiledCode[i].Parameter.GetType().Name);
                    }
                }
                else
                {
                    Log(OutgoingMessages[0].data.ToString());
                }
                throw;
            }

            OutgoingMessages.RemoveAt(0);
        }
    }

    public class Interface
    {
        internal delegate void OnRecivedEventHandler(string message);
        internal event OnRecivedEventHandler OnRecived;

        Thread Listener;
        const int BufferSize = 1024;

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
            Outgoing.Enqueue(data);
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
                        OnRecivedEvent(data);

                        while (Outgoing.Count > 0)
                        {
                            string data_ = Outgoing.Dequeue();
                            outputWriter.Write(data_);
                            outputWriter.Flush();
                            Log($" >> {data_}");
                        }
                    }
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine($"{error}");
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

        void OnRecivedEvent(string data)
        {
            Log($" << {data}");
            OnRecived?.Invoke(data);
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
    }

    static class Extensions
    {
        public static double ToUnix(this DateTime v) => v.ToUniversalTime().Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
    }
}
