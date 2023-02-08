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

        public delegate void OnRecivedEventHandler(InterProcessCommunication sender, IPCMessage<object> message);
        public event OnRecivedEventHandler OnRecived;

        int idCounter = 0;

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
            if (data.Contains('\n'))
            {
                var payloadTexts = data.Split('\n');
                foreach (var element in payloadTexts)
                {
                    var payloadMessage = JsonSerializer.Deserialize<IPCMessage<object>>(element.Trim());
                    OnRecive(payloadMessage);
                }
            }
            else
            {
                IPCMessage<object> payloadMessage = JsonSerializer.Deserialize<IPCMessage<object>>(data);
                OnRecive(payloadMessage);
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
                this.@interface.Send(data);
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
            int BufferSize = 1024;
            Stream input = Console.OpenStandardInput(BufferSize);
            Log($"Communication[Standard] Opened");
            while (true)
            {
                try
                {
                    var buffer = new byte[BufferSize];
                    int length;
                    while (input.CanRead && (length = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        var payload = new byte[length];
                        Buffer.BlockCopy(buffer, 0, payload, 0, length);
                        string data = Encoding.UTF8.GetString(payload).Trim();
                        OnRecivedEvent(data);

                        while (Outgoing.Count > 0)
                        {
                            var data_ = Outgoing.Dequeue();
                            Console.WriteLine(data_);
                            Log($" >> {data_}");
                        }
                    }
                }
                catch (Exception) { break; }
            }
            Log($"Communication[Standard] Closed");
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
