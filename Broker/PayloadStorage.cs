using System.Collections.Concurrent;
using Common;
#nullable disable

namespace Broker
{
     static class PayloadStorage
     {
          private static ConcurrentQueue<Payload> _payloadQueue;

          static PayloadStorage()
          {
               _payloadQueue = new ConcurrentQueue<Payload>();
          }
          public static void Add(Payload payload)
          {
               _payloadQueue.Enqueue(payload);
          }

          public static Payload GetNext()
          {
               Payload payload = null;

               _payloadQueue.TryDequeue(out payload);

               return payload;
          }

          public static bool IsEmpty()
          {
               return _payloadQueue.IsEmpty;
          }
     }
}
