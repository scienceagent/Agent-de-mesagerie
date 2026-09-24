using System;
using System.Threading.Tasks;
using Common;

namespace Broker
{
     class Program
     {
          static void Main(string[] args)
          {
               Console.Title = "Distributed Message Broker";
               Console.WriteLine("==================================================");
               Console.WriteLine("       DISTRIBUTED MESSAGE BROKER (.NET 8)        ");
               Console.WriteLine("==================================================");

               string ip = args.Length > 0 ? args[0] : (Environment.GetEnvironmentVariable("BROKER_IP") ?? "0.0.0.0");
               int port = args.Length > 1 && int.TryParse(args[1], out var parsedPort) ? parsedPort : Settings.BROKER_PORT;

               Console.WriteLine($"[Config] Binding IP: {ip}");
               Console.WriteLine($"[Config] Sockets Port: {port}");
               Console.WriteLine($"[Config] Persistence: storage/messages.journal");
               Console.WriteLine("--------------------------------------------------");

               var socket = new BrokerSocket();
               socket.Start(ip, port);

               var worker = new Worker(workerCount: 4);
               Task.Factory.StartNew(worker.DoSendMessageWork, TaskCreationOptions.LongRunning);

               Console.WriteLine("[Status] Broker is RUNNING. Press Ctrl+C or Enter to shutdown.");
               Console.WriteLine("==================================================");

               Console.ReadLine();
               Console.WriteLine("[Status] Shutting down Broker...");
               worker.Stop();
          }
     }
}