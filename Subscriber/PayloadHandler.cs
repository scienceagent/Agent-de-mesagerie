using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common;
using Newtonsoft.Json;
#nullable disable


namespace Subscriber
{
     class PayloadHandler
     {
          public static void Handle(byte[] payloadBytes)
          {
               var payloadString = Encoding.UTF8.GetString(payloadBytes);
               var payload = JsonConvert.DeserializeObject<Payload>(payloadString); 

               Console.WriteLine(payload.Message);

          }
     }
}
