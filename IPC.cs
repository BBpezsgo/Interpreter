using IngameCoding.BBCode;
using IngameCoding.Bytecode;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using TheProgram;

namespace Communicating
{
    public class IPC
    {
        readonly List<IPCMessage<object>> OutgoingMessages = new();
        bool AwaitingAck = false;

        Thread ThreadListener;

        public delegate void OnRecivedEventHandler(IPC sender, IPCMessage<object> message);
        public event OnRecivedEventHandler OnRecived;

        public readonly bool IgnoreACK = false;

        public IPC(bool IgnoreACK = false)
        {
            this.IgnoreACK = IgnoreACK;
        }

        public void Start()
        {
            ThreadListener = new Thread(ListenerThread);
            ThreadListener.Start();
        }

        void ListenerThread()
        {
            var input = Console.OpenStandardInput();
            Send("started");
            var buffer = new byte[1024];
            int length;
            while (input.CanRead && (length = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                var payload = new byte[length];

                Buffer.BlockCopy(buffer, 0, payload, 0, length);

                string payloadText = Encoding.UTF8.GetString(payload).Trim();

                if (payloadText.Contains('\n'))
                {
                    var payloadTexts = payloadText.Split('\n');
                    foreach (var element in payloadTexts)
                    {
                        var payloadMessage = JsonSerializer.Deserialize<IPCMessage<object>>(element.Trim());
                        OnRecive(payloadMessage);
                    }
                }
                else
                {
                    IPCMessage<object> payloadMessage = JsonSerializer.Deserialize<IPCMessage<object>>(payloadText);
                    OnRecive(payloadMessage);
                }
            }
        }

        void OnRecive(IPCMessage<object> message)
        {
            if (message.type == "ack")
            {
                AwaitingAck = false;
                TrySendNext();
                return;
            }

            Send("ack");

            if (message.type == "greeting")
            {
                Send("geeting", "hi");
                return;
            }
            else if (message.type == "ping")
            {
                Send("pong", DateTime.Now.ToUnix().ToString());
                return;
            }

            OnRecived?.Invoke(this, message);
        }

        public void Send(string messageType) => Send(new IPCMessage<string>(messageType, ""));
        public void Send<T>(string messageType, T messageData) => Send(new IPCMessage<T>(messageType, messageData));
        public void Send<T>(IPCMessage<T> message)
        {
            OutgoingMessages.Add(new IPCMessage<object>(message.type, message.data));
            TrySendNext();
        }

        void TrySendNext()
        {
            if (AwaitingAck && !IgnoreACK) return;
            if (OutgoingMessages.Count == 0) return;
            AwaitingAck = true;

            try
            {
                Console.WriteLine(JsonSerializer.Serialize(OutgoingMessages[0]));
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
                        Console.WriteLine(data0.CompiledCode[i].Parameter.GetType().Name);
                    }
                }
                else
                {
                    Console.WriteLine(OutgoingMessages[0].data);
                }
                throw;
            }

            if (OutgoingMessages[0].type == "ack")
            {
                AwaitingAck = false;
                OutgoingMessages.RemoveAt(0);
                if (OutgoingMessages.Count > 0) TrySendNext();
            }
            else
            {
                OutgoingMessages.RemoveAt(0);
            }
        }
    }

    public class IPCMessage<T>
    {
        public string type { get; set; }
        public T data { get; set; }

        public IPCMessage(string type, T data)
        {
            this.type = type;
            this.data = data;
        }
    }

    static class Extensions
    {
        public static double ToUnix(this DateTime v) => v.ToUniversalTime().Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
    }
}
