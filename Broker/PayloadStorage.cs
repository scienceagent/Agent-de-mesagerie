using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Common;
using Newtonsoft.Json;

#nullable disable
namespace Broker
{
     public static class PayloadStorage
     {
          private static readonly ConcurrentQueue<Payload> _payloadQueue;
          private static readonly string _storageDir;
          private static readonly string _journalPath;
          private static readonly object _fileLock = new();
          private static readonly AutoResetEvent _hasItemsEvent = new(false);

          static PayloadStorage()
          {
               _payloadQueue = new ConcurrentQueue<Payload>();
               _storageDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "storage");
               _journalPath = Path.Combine(_storageDir, "messages.journal");

               try
               {
                    if (!Directory.Exists(_storageDir))
                    {
                         Directory.CreateDirectory(_storageDir);
                    }

                    RecoverFromJournal();
               }
               catch (Exception ex)
               {
                    Console.WriteLine($"[PayloadStorage] Warning during recovery initialization: {ex.Message}");
               }
          }

          /// <summary>
          /// Recovers persisted messages from disk upon broker startup
          /// </summary>
          private static void RecoverFromJournal()
          {
               lock (_fileLock)
               {
                    if (!File.Exists(_journalPath))
                         return;

                    int recoveredCount = 0;
                    using var reader = new StreamReader(new FileStream(_journalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                         if (string.IsNullOrWhiteSpace(line)) continue;
                         try
                         {
                              var payload = JsonConvert.DeserializeObject<Payload>(line);
                              if (payload != null && !string.IsNullOrEmpty(payload.Topic))
                              {
                                   _payloadQueue.Enqueue(payload);
                                   recoveredCount++;
                              }
                         }
                         catch
                         {
                              // Ignore corrupted line
                         }
                    }

                    if (recoveredCount > 0)
                    {
                         Console.WriteLine($"[PayloadStorage] Recovered {recoveredCount} pending messages from persistent journal.");
                         _hasItemsEvent.Set();
                    }
               }
          }

          /// <summary>
          /// Enqueues payload into memory queue and persists immediately to journal file
          /// </summary>
          public static void Add(Payload payload)
          {
               if (payload == null) return;

               // Persist to disk
               try
               {
                    lock (_fileLock)
                    {
                         string jsonLine = JsonConvert.SerializeObject(payload, Formatting.None);
                         File.AppendAllText(_journalPath, jsonLine + Environment.NewLine);
                    }
               }
               catch (Exception ex)
               {
                    Console.WriteLine($"[PayloadStorage] Persistence error: {ex.Message}");
               }

               _payloadQueue.Enqueue(payload);
               _hasItemsEvent.Set();
          }

          /// <summary>
          /// Retrieves historical messages for a given topic from the persistent journal.
          /// Enables replay for newly subscribed clients.
          /// </summary>
          public static List<Payload> GetHistoricalMessages(string topic, int limit = 50)
          {
               if (string.IsNullOrWhiteSpace(topic))
                    return new List<Payload>();

               topic = topic.Trim().ToLowerInvariant();
               var history = new List<Payload>();

               lock (_fileLock)
               {
                    if (!File.Exists(_journalPath))
                         return history;

                    try
                    {
                         using var reader = new StreamReader(new FileStream(_journalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                         string line;
                         while ((line = reader.ReadLine()) != null)
                         {
                              if (string.IsNullOrWhiteSpace(line)) continue;
                              try
                              {
                                   var payload = JsonConvert.DeserializeObject<Payload>(line);
                                   if (payload != null && string.Equals(payload.Topic, topic, StringComparison.OrdinalIgnoreCase))
                                   {
                                        history.Add(payload);
                                   }
                              }
                              catch
                              {
                                   // Ignore corrupted line
                              }
                         }
                    }
                    catch (Exception ex)
                    {
                         Console.WriteLine($"[PayloadStorage] Error reading historical messages: {ex.Message}");
                    }
               }

               if (history.Count > limit)
               {
                    return history.Skip(history.Count - limit).ToList();
               }

               return history;
          }

          public static Payload GetNext()
          {
               _payloadQueue.TryDequeue(out var payload);
               return payload;
          }

          public static bool IsEmpty()
          {
               return _payloadQueue.IsEmpty;
          }

          public static int Count => _payloadQueue.Count;

          public static WaitHandle AvailableHandle => _hasItemsEvent;
     }
}
