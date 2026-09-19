using System;
using Broker;
using Common;

namespace Broker
{
     class Program
     {
          static void Main(string[] args)
          {
               Console.WriteLine("Broker");

               BrokerSocket socket = new BrokerSocket();
               socket.Start(Settings.BROKER_IP, Settings.BROKER_PORT);

               var worker = new Worker();
               Task.Factory.StartNew(worker.DoSendMessageWork, TaskCreationOptions.LongRunning);

               Console.ReadLine();
          }
     }
}