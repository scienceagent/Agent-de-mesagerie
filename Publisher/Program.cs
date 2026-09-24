#nullable disable
using System;
using Common;

namespace Publisher
{
     class Program
     {
          static void Main(string[] args)
          {
               Console.Title = "Message Publisher (C#)";
               Console.WriteLine("==================================================");
               Console.WriteLine("             MESSAGE PUBLISHER (C#)               ");
               Console.WriteLine("==================================================");

               string ip = args.Length > 0 ? args[0] : Settings.BROKER_IP;
               int port = args.Length > 1 && int.TryParse(args[1], out var parsedPort) ? parsedPort : Settings.BROKER_PORT;

               Console.Write("Enter your Publisher name (press Enter for default): ");
               string senderName = Console.ReadLine()?.Trim();
               if (string.IsNullOrEmpty(senderName))
               {
                    senderName = "csharp-publisher-" + Environment.MachineName;
               }

               var publisherSocket = new PublisherSocket();
               bool connected = publisherSocket.Connect(ip, port);

               if (!connected)
               {
                    Console.WriteLine("[Error] Could not connect to Broker. Please verify IP and Port.");
                    Console.WriteLine("Press Enter to exit...");
                    Console.ReadLine();
                    return;
               }

               Console.WriteLine("\nChoose default payload serialization format:");
               Console.WriteLine("  1. JSON (Standard)");
               Console.WriteLine("  2. XML  (Adapter Pattern Testing)");
               Console.Write("Select format [1/2, default: 1]: ");
               string formatChoice = Console.ReadLine()?.Trim();
               string selectedFormat = (formatChoice == "2" || formatChoice?.ToLower() == "xml") ? "xml" : "json";

               Console.WriteLine($"\n[Active] Publishing mode: {selectedFormat.ToUpper()}");
               Console.WriteLine("Type 'exit' as topic to quit, or 'switch' to toggle JSON/XML.\n");

               while (true)
               {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write("\nEnter topic: ");
                    Console.ResetColor();
                    string topic = Console.ReadLine()?.Trim();

                    if (string.IsNullOrEmpty(topic))
                         continue;

                    if (topic.Equals("exit", StringComparison.OrdinalIgnoreCase))
                         break;

                    if (topic.Equals("switch", StringComparison.OrdinalIgnoreCase))
                    {
                         selectedFormat = (selectedFormat == "json") ? "xml" : "json";
                         Console.WriteLine($"[Switched] Format is now: {selectedFormat.ToUpper()}");
                         continue;
                    }

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write("Enter message: ");
                    Console.ResetColor();
                    string message = Console.ReadLine();

                    var payload = new Payload
                    {
                         Topic = topic.ToLowerInvariant(),
                         Message = message,
                         Sender = senderName,
                         Format = selectedFormat
                    };

                    bool sent = publisherSocket.SendPayload(payload, selectedFormat);
                    if (sent)
                    {
                         Console.ForegroundColor = ConsoleColor.Green;
                         Console.WriteLine($"-> Sent [{selectedFormat.ToUpper()}] payload to topic '{topic}'");
                         Console.ResetColor();
                    }
               }

               publisherSocket.Close();
               Console.WriteLine("[Publisher] Exited cleanly.");
          }
     }
}
