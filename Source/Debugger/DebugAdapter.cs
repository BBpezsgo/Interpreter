using System;
using System.IO;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using TheProgram;

namespace DebugAdapter
{
    internal class Server
    {
        internal static void Start(ArgumentParser.Settings settings)
        {
            Listener listener = new(settings.PipeName, settings.Port);
            listener.Start();
        }
    }

    public class Listener
    {
        Thread ThreadListener;

        readonly string PipeName;
        readonly int Port;

        public Listener(string pipeName, int port)
        {
            this.PipeName = pipeName;
            this.Port = port;
        }

        public void Start()
        {
            ThreadListener = new Thread(ListenerThread);
            ThreadListener.Start();
            ThreadListener.Join();
        }

        void ListenerThread()
        {
            if (PipeName != null)
            {
                Console.WriteLine($"Connect to '{PipeName}' ...");
                var client = new NamedPipeClientStream(PipeName);
                client.Connect(1000);
                Console.WriteLine($"Connected to '{PipeName}'");
                while (client.IsConnected)
                {
                    Listen(client);
                }
                Console.WriteLine($"Disconnected from '{PipeName}'");
            }
            else if (Port != -1)
            {
                Console.WriteLine($"Connect to localhost:{Port} ...");
                try
                {
                    using TcpClient client = new("localhost", Port);
                    {
                        NetworkStream stream = client.GetStream();
                        Console.WriteLine($"Connected to localhost:{Port}");
                        Listen(stream);
                    }
                }
                catch (ArgumentNullException e)
                { Console.WriteLine($"ArgumentNullException: {e}"); }
                catch (SocketException e)
                { Console.WriteLine($"SocketException: {e}"); }
                finally
                { Console.WriteLine($"Disconnected from localhost:{Port}"); }
            }
            else
            {
                Listen(Console.OpenStandardInput());
            }
        }

        void Listen(Stream stream)
        {
            byte[] buffer = new byte[1024];
            int length;
            while (stream.CanRead && (length = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                byte[] payload = new byte[length];
                Buffer.BlockCopy(buffer, 0, payload, 0, length);
                string data = Encoding.UTF8.GetString(payload).Trim();
                IngameCoding.Output.File.WriteLine($" << {data}");
                Console.WriteLine($" << {data}");
            }
        }
    }
}
