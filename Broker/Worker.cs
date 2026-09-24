using System;
using System.Threading;
using System.Threading.Tasks;
using Common;

#nullable disable
namespace Broker
{
     public class Worker
     {
          private readonly int _workerCount;
          private readonly CancellationTokenSource _cts = new();

          public Worker(int workerCount = 4)
          {
               _workerCount = workerCount;
          }

          /// <summary>
          /// Main worker loop preserving original method signature for backwards compatibility
          /// </summary>
          public void DoSendMessageWork()
          {
               Console.WriteLine($"[WorkerPool] Starting {_workerCount} concurrent dispatch workers...");

               for (int i = 0; i < _workerCount; i++)
               {
                    int workerId = i + 1;
                    Task.Factory.StartNew(() => RunWorkerLoop(workerId), TaskCreationOptions.LongRunning);
               }

               // Keep alive
               _cts.Token.WaitHandle.WaitOne();
          }

          private void RunWorkerLoop(int workerId)
          {
               while (!_cts.IsCancellationRequested)
               {
                    try
                    {
                         // Wait until payload is available or timeout 250ms
                         PayloadStorage.AvailableHandle.WaitOne(250);

                         while (!PayloadStorage.IsEmpty())
                         {
                              var payload = PayloadStorage.GetNext();
                              if (payload == null)
                                   continue;

                              DispatchMessage(payload, workerId);
                         }
                    }
                    catch (Exception ex)
                    {
                         Console.WriteLine($"[Worker-{workerId}] Dispatch loop error: {ex.Message}");
                         Thread.Sleep(100);
                    }
               }
          }

          private void DispatchMessage(Payload payload, int workerId)
          {
               var connections = ConnectionStorage.GetConnectionByTopic(payload.Topic);
               if (connections.Count == 0)
               {
                    // No active subscribers for this topic at this moment
                    return;
               }

               Console.WriteLine($"[Worker-{workerId}] Dispatching payload '{payload.Id}' (Topic: '{payload.Topic}') to {connections.Count} subscriber(s)...");

               Parallel.ForEach(connections, connection =>
               {
                    try
                    {
                         // Adapter Pattern: Format payload according to subscriber's preference
                         string targetFormat = connection.PreferredFormat ?? "json";
                         string formattedMessage = SerializationHelper.ConvertFormat(payload, targetFormat);

                         // Send with frame delimiter
                         bool sent = connection.SendFramed(formattedMessage);
                         if (!sent)
                         {
                              Console.WriteLine($"[Worker] Failed delivery to [{connection.Address}], removing stale connection.");
                              ConnectionStorage.Remove(connection.Address);
                         }
                    }
                    catch (Exception ex)
                    {
                         Console.WriteLine($"[Worker] Error sending to [{connection.Address}]: {ex.Message}");
                         ConnectionStorage.Remove(connection.Address);
                    }
               });
          }

          public void Stop()
          {
               _cts.Cancel();
          }
     }
}
