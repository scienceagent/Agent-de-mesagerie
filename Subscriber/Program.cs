#nullable disable
using System;
using System.Linq;
using Common;

namespace Subscriber
{
     class Program
     {
          static void Main(string[] args)
          {
               Console.Title = "Message Subscriber (C#)";
               Console.WriteLine("==================================================");
               Console.WriteLine("             MESSAGE SUBSCRIBER (C#)              ");
               Console.WriteLine("==================================================");

               string ip = args.Length > 0 ? args[0] : Settings.BROKER_IP;
               int port = args.Length > 1 && int.TryParse(args[1], out var parsedPort) ? parsedPort : Settings.BROKER_PORT;

               Console.Write("Enter initial topic to subscribe (or press Enter to skip): ");
               string initialTopic = Console.ReadLine()?.Trim();

               var subscriberSocket = new SubscriberSocket();
               bool connected = subscriberSocket.Connect(ip, port);

               if (!connected)
               {
                    Console.WriteLine("[Error] Could not connect to Broker. Press Enter to exit.");
                    Console.ReadLine();
                    return;
               }

               if (!string.IsNullOrEmpty(initialTopic))
               {
                    subscriberSocket.Subscribe(initialTopic);
               }

               PrintHelp();

               while (true)
               {
                    Console.Write("\n[Command (h for help)] > ");
                    string command = Console.ReadLine()?.Trim();

                    if (string.IsNullOrEmpty(command))
                         continue;

                    if (command.Equals("exit", StringComparison.OrdinalIgnoreCase) || command.Equals("quit", StringComparison.OrdinalIgnoreCase))
                    {
                         break;
                    }

                    if (command.Equals("h", StringComparison.OrdinalIgnoreCase) || command.Equals("help", StringComparison.OrdinalIgnoreCase))
                    {
                         PrintHelp();
                         continue;
                    }

                    if (command.StartsWith("sub ", StringComparison.OrdinalIgnoreCase))
                    {
                         string topic = command.Substring(4).Trim();
                         subscriberSocket.Subscribe(topic);
                    }
                    else if (command.StartsWith("unsub ", StringComparison.OrdinalIgnoreCase))
                    {
                         string topic = command.Substring(6).Trim();
                         subscriberSocket.Unsubscribe(topic);
                    }
                    else if (command.Equals("list", StringComparison.OrdinalIgnoreCase))
                    {
                         subscriberSocket.RequestTopicsList();
                    }
                    else if (command.StartsWith("format ", StringComparison.OrdinalIgnoreCase))
                    {
                         string fmt = command.Substring(7).Trim();
                         subscriberSocket.SetFormat(fmt);
                    }
                    else if (command.Equals("my", StringComparison.OrdinalIgnoreCase))
                    {
                         Console.WriteLine("Active subscriptions: " + 
                              (subscriberSocket.SubscribedTopics.Count > 0 
                                   ? string.Join(", ", subscriberSocket.SubscribedTopics) 
                                   : "None"));
                    }
                    else
                    {
                         Console.WriteLine("Unknown command. Type 'help' for options.");
                    }
               }

               subscriberSocket.Close();
               Console.WriteLine("[Subscriber] Exited cleanly.");
          }

          private static void PrintHelp()
          {
               Console.WriteLine("\nAvailable Subscriber Commands:");
               Console.WriteLine("  sub <topic>     - Subscribe to topic (e.g., sub sport)");
               Console.WriteLine("  unsub <topic>   - Unsubscribe from topic (e.g., unsub sport)");
               Console.WriteLine("  my              - View your currently subscribed topics");
               Console.WriteLine("  list            - Request list of all active topics from Broker");
               Console.WriteLine("  format <json|xml> - Set preferred format (Adapter Pattern)");
               Console.WriteLine("  help            - Display this help message");
               Console.WriteLine("  exit            - Disconnect and exit");
          }
     }
}