using IngameCoding.BBCode;
using IngameCoding.Bytecode;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Communicating
{
    public class IPC
    {
        readonly List<IPCMessage<object>> OutgoingMessages = new();
        bool AwaitingAck = false;

        Thread ThreadListener;

        public delegate void OnRecivedEventHandler(IPC sender, IPCMessage<object> message);
        public event OnRecivedEventHandler OnRecived;

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
            if (AwaitingAck) return;
            if (OutgoingMessages.Count == 0) return;
            AwaitingAck = true;

            Console.WriteLine(JsonSerializer.Serialize(OutgoingMessages[0]));

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
