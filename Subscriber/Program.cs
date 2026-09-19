using System;
using Common;
#nullable disable


namespace Subscriber
{
     class Program
     {
          static void Main(string[] args)
          {
               Console.WriteLine("Subscriber");

               string topic;

               Console.Write("Enter the topic: ");
               topic = Console.ReadLine().ToLower();

               var subscriberSocket = new SubscriberSocket(topic);
               subscriberSocket.Connect(Settings.BROKER_IP, Settings.BROKER_PORT);

               Console.WriteLine("Press any key to exit...");
               Console.ReadLine();
              
          }
     }
}