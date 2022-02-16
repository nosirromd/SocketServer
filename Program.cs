using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocketServer
{
    class Program
    {
        static void Main(string[] args)
        {
            //get a port to listen on from the user
            //and connection management mode
            //and then manage any connections
            int mode, port;
            if  (args.Length == 2) {
                if (!int.TryParse(args[0], out port))
                {
                    ShowUsage();
                    return;
                }                 
                if (!int.TryParse(args[1], out mode))
                {
                    ShowUsage();
                    return;
                } 
            }
            else if  (args.Length == 1) {
                if (!int.TryParse(args[0], out port))
                {
                    ShowUsage();
                    return;
                } 
                mode = 1;                
            }
            else {
                ShowUsage();
                return;
            }

            Listener(port, mode);
            Console.ReadLine();
        }

        private static void ShowUsage() =>
            Console.WriteLine("SocketServer port mode");

        public static void Listener(int port, int mode)
        {
            //listen on TCP streem socket
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.ReceiveTimeout = 50000; // receive timout 5 seconds
            listener.SendTimeout = 50000; // send timeout 5 seconds 

            //listen on the specified port
            //only allow 15 pending connections in the queue
            listener.Bind(new IPEndPoint(IPAddress.Any, port));
            listener.Listen(backlog: 15);

            Console.WriteLine($"listener started on port {port}");

            //this is a class to help us manage task cancellation
            var cts = new CancellationTokenSource();

            //use a factory to create a listener task
            //start the task
            var tf = new TaskFactory(TaskCreationOptions.LongRunning, TaskContinuationOptions.None);
            tf.StartNew(() =>  // listener task
            {
                Console.WriteLine("listener task started");
                //loop until task is cancelled
                while (true)
                {
                    if (cts.Token.IsCancellationRequested)
                    {
                        cts.Token.ThrowIfCancellationRequested();
                        break;
                    }

                    //wait for connection
                    //accept connection
                    //writeout  connecion stats
                    Console.WriteLine("waiting for accept");
                    Socket client = listener.Accept();
                    if (!client.Connected)
                    {
                        Console.WriteLine("not connected");
                        continue;
                    }
                    Console.WriteLine($"client connected local address {((IPEndPoint)client.LocalEndPoint).Address} and port {((IPEndPoint)client.LocalEndPoint).Port}, remote address {((IPEndPoint)client.RemoteEndPoint).Address} and port {((IPEndPoint)client.RemoteEndPoint).Port}");

                    //use another task to manage the connection
                    Task t;
                    switch (mode)
                    {
                        case 1:                       
                        default:
                        t = CommunicateWithClientUsingSocketAsync(client);
                        break;
                        case 2:
                        t = CommunicateWithClientUsingNetworkStreamAsync(client);
                        break;
                        case 3:
                        t = CommunicateWithClientUsingReadersAndWritersAsync(client);
                        break;
                    }
                }
                listener.Dispose();
                Console.WriteLine("Listener task closing");

            }, cts.Token);

            Console.WriteLine("Press return to exit");
            Console.ReadLine();
            cts.Cancel();
        }

        private static Task CommunicateWithClientUsingSocketAsync(Socket socket)
        {
            return Task.Run(() =>
            {
                try
                {
                    using (socket)
                    {
                        bool completed = false;
                        //buffer active connections comms
                        //convert bytes to string
                        //printout received string
                        //echo received string back to client
                        //until client sends a shutdown command
                        do
                        {
                            byte[] readBuffer = new byte[1024];
                            int read = socket.Receive(readBuffer, 0, 1024, SocketFlags.None);
                            string fromClient = Encoding.UTF8.GetString(readBuffer, 0, read);
                            Console.WriteLine($"read {read} bytes: {fromClient}");
                            if (string.Compare(fromClient, "shutdown", ignoreCase: true) == 0)
                            {
                                completed = true;
                            }

                            byte[] writeBuffer = Encoding.UTF8.GetBytes($"echo {fromClient}");

                            int send = socket.Send(writeBuffer);
                            Console.WriteLine($"sent {send} bytes");

                        } while (!completed);
                    }
                    Console.WriteLine("closed stream and client socket");
                }
                catch (SocketException ex)
                {
                    Console.WriteLine(ex.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            });
        }

        private static async Task CommunicateWithClientUsingNetworkStreamAsync(Socket socket)
        {
            try
            {
                //open a network stream on the socket
                using (var stream = new NetworkStream(socket, ownsSocket: true))
                {

                    bool completed = false;
                    do
                    {
                        // while shutdown command not received
                        // read bytes off the stream into buffer
                        // decode to string
                        // write string to console
                        byte[] readBuffer = new byte[1024];
                        int read = await stream.ReadAsync(readBuffer, 0, 1024);
                        string fromClient = Encoding.UTF8.GetString(readBuffer, 0, read);
                        Console.WriteLine($"read {read} bytes: {fromClient}");
                        if (string.Compare(fromClient, "shutdown", ignoreCase: true) == 0)
                        {
                            completed = true;
                        }

                        //echo received string back to client
                        byte[] writeBuffer = Encoding.UTF8.GetBytes($"echo {fromClient}");

                        await stream.WriteAsync(writeBuffer, 0, writeBuffer.Length);

                    } while (!completed);
                }
                Console.WriteLine("closed stream and client socket");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static async Task CommunicateWithClientUsingReadersAndWritersAsync(Socket socket)
        {
            try
            {
                // while shutdown command not received
                // open a network stream on the socket
                // open stream reader and writer on the stream
                // read and write string to console
                using (var stream = new NetworkStream(socket, ownsSocket: true))
                using (var reader = new StreamReader(stream, Encoding.UTF8, false, 8192, leaveOpen: true))
                using (var writer = new StreamWriter(stream, Encoding.UTF8, 8192, leaveOpen: true))
                {
                    writer.AutoFlush = true;

                    bool completed = false;
                    do
                    {
                        string fromClient = await reader.ReadLineAsync();
                        Console.WriteLine($"read {fromClient}");
                        if (string.Compare(fromClient, "shutdown", ignoreCase: true) == 0)
                        {
                            completed = true;
                        }

                        //echo received string back to client
                        await writer.WriteLineAsync($"echo {fromClient}");

                    } while (!completed);
                }
                Console.WriteLine("closed stream and client socket");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}