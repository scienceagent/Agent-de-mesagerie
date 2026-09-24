using System;
using System.Linq;
using System.Text;
using Common;

#nullable disable
namespace Broker
{
     public static class PayloadHandler
     {
          private const string BROKER_NODE_ID = "BrokerNode-1";

          public static void Handle(string messageFrame, ConnectionInfo connectionInfo)
          {
               if (string.IsNullOrWhiteSpace(messageFrame) || connectionInfo == null)
                    return;

               messageFrame = messageFrame.Trim();

               try
               {
                    // 1. Subscription command + Automatic Historical Replay
                    if (messageFrame.StartsWith("subscribe#", StringComparison.OrdinalIgnoreCase))
                    {
                         string topic = messageFrame.Substring("subscribe#".Length).Trim().ToLowerInvariant();
                         if (!string.IsNullOrEmpty(topic))
                         {
                              ConnectionStorage.Add(connectionInfo);

                              // Enforce single subscription per topic (Idempotency)
                              bool isNewSubscription = ConnectionStorage.Subscribe(connectionInfo.Address, topic);
                              if (!isNewSubscription)
                              {
                                   Console.WriteLine($"[Broker] Client [{connectionInfo.Address}] is ALREADY subscribed to topic: '{topic}'. Ignored duplicate.");
                                   connectionInfo.SendFramed($"INFO#already_subscribed#{topic}");
                                   return;
                              }

                              connectionInfo.Topics.Add(topic);

                              Console.WriteLine($"[Broker] Client [{connectionInfo.Address}] subscribed to topic: '{topic}'");
                              connectionInfo.SendFramed($"ACK#subscribed#{topic}");

                              // Historical replay for newly subscribed client
                              var history = PayloadStorage.GetHistoricalMessages(topic, limit: 50);
                              if (history.Count > 0)
                              {
                                   Console.WriteLine($"[Broker] Replaying {history.Count} historical message(s) on topic '{topic}' to [{connectionInfo.Address}]");
                                   foreach (var oldPayload in history)
                                   {
                                        string targetFormat = connectionInfo.PreferredFormat ?? "json";
                                        string formatted = SerializationHelper.ConvertFormat(oldPayload, targetFormat);
                                        connectionInfo.SendFramed(formatted);
                                   }
                              }
                         }
                         return;
                    }

                    // 2. Unsubscription command
                    if (messageFrame.StartsWith("unsubscribe#", StringComparison.OrdinalIgnoreCase))
                    {
                         string topic = messageFrame.Substring("unsubscribe#".Length).Trim().ToLowerInvariant();
                         if (!string.IsNullOrEmpty(topic))
                         {
                              connectionInfo.Topics.Remove(topic);
                              ConnectionStorage.Unsubscribe(connectionInfo.Address, topic);

                              Console.WriteLine($"[Broker] Client [{connectionInfo.Address}] unsubscribed from topic: '{topic}'");
                              connectionInfo.SendFramed($"ACK#unsubscribed#{topic}");
                         }
                         return;
                    }

                    // 3. Explicit history request command (e.g., history#pisici)
                    if (messageFrame.StartsWith("history#", StringComparison.OrdinalIgnoreCase))
                    {
                         string topic = messageFrame.Substring("history#".Length).Trim().ToLowerInvariant();
                         var history = PayloadStorage.GetHistoricalMessages(topic, limit: 50);
                         Console.WriteLine($"[Broker] Explicit history request: sending {history.Count} message(s) for topic '{topic}' to [{connectionInfo.Address}]");
                         foreach (var oldPayload in history)
                         {
                              string targetFormat = connectionInfo.PreferredFormat ?? "json";
                              string formatted = SerializationHelper.ConvertFormat(oldPayload, targetFormat);
                              connectionInfo.SendFramed(formatted);
                         }
                         return;
                    }

                    // 4. Topics list query
                    if (messageFrame.Equals("topics#list", StringComparison.OrdinalIgnoreCase))
                    {
                         var allTopics = ConnectionStorage.GetAllTopics();
                         string topicList = allTopics.Count > 0 ? string.Join(",", allTopics) : "none";
                         connectionInfo.SendFramed($"TOPICS#{topicList}");
                         return;
                    }

                    // 5. Preferred format selection (Adapter Pattern)
                    if (messageFrame.StartsWith("format#", StringComparison.OrdinalIgnoreCase))
                    {
                         string format = messageFrame.Substring("format#".Length).Trim().ToLowerInvariant();
                         if (format == "xml" || format == "json")
                         {
                              connectionInfo.PreferredFormat = format;
                              connectionInfo.SendFramed($"ACK#format#{format}");
                              Console.WriteLine($"[Broker] Client [{connectionInfo.Address}] set format preference to: {format}");
                         }
                         return;
                    }

                    // 6. Normal Message Payload (JSON or XML)
                    Payload payload = SerializationHelper.DeserializePayload(messageFrame, out string detectedFormat);

                    if (payload == null || string.IsNullOrWhiteSpace(payload.Topic))
                    {
                         Console.WriteLine($"[Broker] Warning: Rejected malformed payload or missing topic from [{connectionInfo.Address}]");
                         connectionInfo.SendFramed("ERROR#invalid_payload_missing_topic");
                         return;
                    }

                    // Content Enricher Pattern: enrich payload with metadata
                    if (string.IsNullOrEmpty(payload.Id) || payload.Id == Guid.Empty.ToString())
                    {
                         payload.Id = Guid.NewGuid().ToString();
                    }

                    if (payload.Timestamp == default)
                    {
                         payload.Timestamp = DateTime.UtcNow;
                    }

                    payload.Topic = payload.Topic.Trim().ToLowerInvariant();
                    payload.Format = detectedFormat;
                    if (string.IsNullOrEmpty(payload.Sender) || payload.Sender == "anonymous")
                    {
                         payload.Sender = $"{connectionInfo.Address} ({BROKER_NODE_ID})";
                    }

                    // Store payload in persistent storage
                    PayloadStorage.Add(payload);

                    Console.WriteLine($"[Broker] Received & Enriched [{payload.Format.ToUpper()}] message on topic '{payload.Topic}' (ID: {payload.Id}). Stored in journal.");
                    connectionInfo.SendFramed($"ACK#published#{payload.Id}");
               }
               catch (Exception ex)
               {
                    Console.WriteLine($"[Broker] Exception handling payload from [{connectionInfo.Address}]: {ex.Message}");
                    connectionInfo.SendFramed($"ERROR#internal_error#{ex.Message}");
               }
          }

          /// <summary>
          /// Overload for backwards compatibility with byte[] interface
          /// </summary>
          public static void Handle(byte[] payloadBytes, ConnectionInfo connectionInfo)
          {
               if (payloadBytes == null || payloadBytes.Length == 0) return;
               string payloadString = Encoding.UTF8.GetString(payloadBytes);
               Handle(payloadString, connectionInfo);
          }
     }
}
