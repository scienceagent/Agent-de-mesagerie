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

          static ConnectionStorage()
          {
               _connections = new List<ConnectionInfo>();
               _locker = new object();
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
