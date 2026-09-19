using System;
using System.Collections.Generic;
using System.Linq;
#nullable disable
using System.Text;
using System.Threading.Tasks;
using Common;
using Newtonsoft.Json;

namespace Publisher
{
     class Program
     {
          static void Main(string[] args)
          {
               Console.WriteLine("Publisher");

               var publisherSocket = new PublisherSocket();
               publisherSocket.Connect(Settings.BROKER_IP, Settings.BROKER_PORT);

               if (publisherSocket.IsConnected)
               {
                    while (true)
                    {
                         var payload = new Payload();

                         Console.Write("Enter the topic: ");
                         payload.Topic = Console.ReadLine().ToLower();

                         Console.Write("Enter the message: ");
                         payload.Message = Console.ReadLine();

                         var payloadString = JsonConvert.SerializeObject(payload);
                         byte[] data = Encoding.UTF8.GetBytes(payloadString);

                         publisherSocket.Send(data);
                    }

               }
               Console.ReadLine();
          }
     }
}
