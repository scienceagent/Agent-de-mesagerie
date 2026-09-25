using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

#nullable disable
namespace Common
{
     public class ConnectionInfo
     {
          public const int BUFF_SIZE = 8192;
          public byte[] Data { get; set; }
          public Socket Socket { get; set; }
          public string Address { get; set; }

          // Legacy single topic property for backwards compatibility
          public string Topic
          {
               get => Topics.Count > 0 ? string.Join(",", Topics) : string.Empty;
               set
               {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                         Topics.Add(value.Trim().ToLowerInvariant());
                    }
               }
          }

          // Modern multi-topic subscription set
          public HashSet<string> Topics { get; } = new(StringComparer.OrdinalIgnoreCase);

          // Preferred output format for this subscriber (Adapter pattern: "json" or "xml")
          public string PreferredFormat { get; set; } = "json";

          // In-Flight unacknowledged messages sent to this client (Message ID -> Timestamp)
          public System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> InFlightMessages { get; } = new();

          // Stream buffer to assemble TCP chunks into complete delimited frames
          private readonly StringBuilder _buffer = new();
          private readonly object _sendLock = new();

          public ConnectionInfo()
          {
               Data = new byte[BUFF_SIZE];
          }

          public List<string> AppendAndExtractFrames(byte[] chunk, int count)
          {
               string text = Encoding.UTF8.GetString(chunk, 0, count);
               lock (_buffer)
               {
                    _buffer.Append(text);
                    string current = _buffer.ToString();
                    var frames = MessageFraming.Decode(ref current);
                    _buffer.Clear();
                    _buffer.Append(current);
                    return frames;
               }
          }

          public bool SendFramed(string message)
          {
               try
               {
                    if (Socket == null || !Socket.Connected)
                         return false;

                    byte[] data = MessageFraming.Encode(message);
                    lock (_sendLock)
                    {
                         Socket.Send(data);
                    }
                    return true;
               }
               catch
               {
                    return false;
               }
          }
     }
}
