using System;
using System.Collections.Generic;
using System.Linq;
using Common;

#nullable disable
namespace Broker
{
     public static class ConnectionStorage
     {
          private static readonly List<ConnectionInfo> _connections;
          private static readonly object _locker;
          private static readonly Dictionary<string, int> _queueIndices;

          static ConnectionStorage()
          {
               _connections = new List<ConnectionInfo>();
               _locker = new object();
               _queueIndices = new Dictionary<string, int>();
          }

          public static void Add(ConnectionInfo connection)
          {
               if (connection == null) return;

               lock (_locker)
               {
                    var existing = _connections.FirstOrDefault(x => x.Address == connection.Address);
                    if (existing == null)
                    {
                         _connections.Add(connection);
                    }
                    else
                    {
                         foreach (var topic in connection.Topics)
                         {
                              existing.Topics.Add(topic);
                         }
                    }
               }
          }

          public static bool Subscribe(string address, string topic)
          {
               if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(topic))
                    return false;

               topic = topic.Trim().ToLowerInvariant();

               lock (_locker)
               {
                    var connection = _connections.FirstOrDefault(x => x.Address == address);
                    if (connection != null)
                    {
                         // Returns true if newly subscribed, false if already subscribed
                         return connection.Topics.Add(topic);
                    }
               }
               return false;
          }

          public static bool Unsubscribe(string address, string topic)
          {
               if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(topic))
                    return false;

               topic = topic.Trim().ToLowerInvariant();

               lock (_locker)
               {
                    var connection = _connections.FirstOrDefault(x => x.Address == address);
                    if (connection != null)
                    {
                         return connection.Topics.Remove(topic);
                    }
               }
               return false;
          }

          public static void Remove(string address)
          {
               lock (_locker)
               {
                    _connections.RemoveAll(x => x.Address == address);
               }
          }

          public static ConnectionInfo GetByAddress(string address)
          {
               lock (_locker)
               {
                    return _connections.FirstOrDefault(x => x.Address == address);
               }
          }

          public static List<ConnectionInfo> GetConnectionByTopic(string topic)
          {
               if (string.IsNullOrWhiteSpace(topic))
                    return new List<ConnectionInfo>();

               topic = topic.Trim().ToLowerInvariant();

               lock (_locker)
               {
                    return _connections
                         .Where(x => x.Socket != null && x.Socket.Connected && x.Topics.Contains(topic))
                         .ToList();
               }
          }

          /// <summary>
          /// Determines if a topic represents a Unicast Point-to-Point Queue (e.g. queue:tasks, queue#jobs, queue/work)
          /// </summary>
          public static bool IsQueueTopic(string topic)
          {
               if (string.IsNullOrWhiteSpace(topic))
                    return false;
               string t = topic.Trim().ToLowerInvariant();
               return t.StartsWith("queue:") || t.StartsWith("queue#") || t.StartsWith("queue/");
          }

          /// <summary>
          /// Retrieves connections for message dispatch.
          /// If the topic is a Unicast Queue, selects exactly ONE consumer in a round-robin fashion (Competing Consumers).
          /// If the topic is a standard topic, selects ALL consumers (Multicast / Pub-Sub).
          /// </summary>
          public static List<ConnectionInfo> GetConnectionsForDispatch(string topic)
          {
               if (string.IsNullOrWhiteSpace(topic))
                    return new List<ConnectionInfo>();

               topic = topic.Trim().ToLowerInvariant();

               lock (_locker)
               {
                    var active = _connections
                         .Where(x => x.Socket != null && x.Socket.Connected && x.Topics.Contains(topic))
                         .ToList();

                    if (active.Count == 0)
                         return active;

                    if (IsQueueTopic(topic))
                    {
                         // Unicast Queue: round-robin selection of ONE consumer
                         int index = 0;
                         if (_queueIndices.TryGetValue(topic, out int lastIndex))
                         {
                              index = (lastIndex + 1) % active.Count;
                         }
                         _queueIndices[topic] = index;
                         if (index >= active.Count)
                              index = 0;

                         return new List<ConnectionInfo> { active[index] };
                    }

                    // Multicast Pub/Sub: fanout to all active subscribers
                    return active;
               }
          }

          public static List<string> GetAllTopics()
          {
               lock (_locker)
               {
                    return _connections
                         .SelectMany(x => x.Topics)
                         .Distinct()
                         .OrderBy(t => t)
                         .ToList();
               }
          }

          public static int GetSubscriberCount(string topic)
          {
               topic = topic?.Trim().ToLowerInvariant();
               lock (_locker)
               {
                    return _connections.Count(x => x.Topics.Contains(topic));
               }
          }
     }
}
