using System;
using Common;

#nullable disable
namespace Subscriber
{
     public static class PayloadHandler
     {
          public static void Handle(string payloadString)
          {
               if (string.IsNullOrWhiteSpace(payloadString)) return;

               try
               {
                    // Check if it is a control ACK / response
                    if (payloadString.StartsWith("ACK#", StringComparison.OrdinalIgnoreCase) ||
                        payloadString.StartsWith("TOPICS#", StringComparison.OrdinalIgnoreCase) ||
                        payloadString.StartsWith("ERROR#", StringComparison.OrdinalIgnoreCase))
                    {
                         Console.ForegroundColor = ConsoleColor.Yellow;
                         Console.WriteLine($"\n[Broker Control] {payloadString}");
                         Console.ResetColor();
                         return;
                    }

                    // Deserialize as Payload (XML or JSON)
                    var payload = SerializationHelper.DeserializePayload(payloadString, out string detectedFormat);
                    if (payload != null)
                    {
                         Console.ForegroundColor = ConsoleColor.Green;
                         Console.WriteLine("\n--------------------------------------------------");
                         Console.WriteLine($"[RECEIVED MESSAGE] Topic: '{payload.Topic}' (Format: {detectedFormat.ToUpper()})");
                         Console.WriteLine($"  ID:        {payload.Id}");
                         Console.WriteLine($"  Time UTC:  {payload.Timestamp:yyyy-MM-dd HH:mm:ss}");
                         Console.WriteLine($"  Sender:    {payload.Sender}");
                         Console.ForegroundColor = ConsoleColor.White;
                         Console.WriteLine($"  Content:   {payload.Message}");
                         Console.ForegroundColor = ConsoleColor.Green;
                         Console.WriteLine("--------------------------------------------------");
                         Console.ResetColor();
                    }
               }
               catch (Exception ex)
               {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[Subscriber] Error parsing incoming payload: {ex.Message}");
                    Console.ResetColor();
               }
          }

          public static void Handle(byte[] payloadBytes)
          {
               if (payloadBytes == null || payloadBytes.Length == 0) return;
               string payloadString = System.Text.Encoding.UTF8.GetString(payloadBytes);
               Handle(payloadString);
          }
     }
}
