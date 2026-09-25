using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Common;

#nullable disable
namespace Subscriber
{
     public class SubscriberSocket
     {
          private Socket _socket;
          private readonly string _initialTopic;
          private readonly HashSet<string> _subscribedTopics = new(StringComparer.OrdinalIgnoreCase);
          private readonly ConnectionInfo _connectionInfo = new();
          public bool IsConnected => _socket != null && _socket.Connected;

          public IReadOnlyCollection<string> SubscribedTopics => _subscribedTopics;

          public SubscriberSocket(string topic = null)
          {
               _initialTopic = topic;
               _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
          }

          public bool Connect(string ipAddress, int port)
          {
               try
               {
                    Console.WriteLine($"[Subscriber] Connecting to Broker at {ipAddress}:{port}...");
                    var ip = (ipAddress == "localhost") ? IPAddress.Loopback : IPAddress.Parse(ipAddress);
                    
                    _socket.Connect(new IPEndPoint(ip, port));
                    Console.WriteLine("[Subscriber] Connected successfully to Broker.");

                    _connectionInfo.Socket = _socket;
                    StartReceive();

                    if (!string.IsNullOrWhiteSpace(_initialTopic))
                    {
                         Subscribe(_initialTopic);
                    }

                    return true;
               }
               catch (Exception ex)
               {
                    Console.WriteLine($"[Subscriber] Connection failed: {ex.Message}");
                    return false;
               }
          }

          public void Subscribe(string topic)
          {
               if (string.IsNullOrWhiteSpace(topic) || !IsConnected)
                    return;

               topic = topic.Trim().ToLowerInvariant();
               if (_subscribedTopics.Contains(topic))
               {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[Subscriber] You are ALREADY subscribed to topic: '{topic}'. Request skipped.");
                    Console.ResetColor();
                    return;
               }

               _subscribedTopics.Add(topic);
               SendFramed("subscribe#" + topic);
               Console.WriteLine($"[Subscriber] Sent subscribe request for topic: '{topic}'");
          }

          public void Unsubscribe(string topic)
          {
               if (string.IsNullOrWhiteSpace(topic) || !IsConnected)
                    return;

               topic = topic.Trim().ToLowerInvariant();
               if (!_subscribedTopics.Contains(topic))
               {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[Subscriber] You are NOT subscribed to topic: '{topic}'.");
                    Console.ResetColor();
                    return;
               }

               _subscribedTopics.Remove(topic);
               SendFramed("unsubscribe#" + topic);
               Console.WriteLine($"[Subscriber] Sent unsubscribe request for topic: '{topic}'");
          }

          public void SetFormat(string format)
          {
               if (!IsConnected) return;
               format = format.Trim().ToLowerInvariant();
               SendFramed("format#" + format);
               Console.WriteLine($"[Subscriber] Requested format preference: {format.ToUpper()}");
          }

          public void RequestTopicsList()
          {
               if (!IsConnected) return;
               SendFramed("topics#list");
          }

          private void StartReceive()
          {
               try
               {
                    _socket.BeginReceive(
                         _connectionInfo.Data,
                         0,
                         _connectionInfo.Data.Length,
                         SocketFlags.None,
                         ReceiveCallback,
                         _connectionInfo);
               }
               catch (Exception ex)
               {
                    Console.WriteLine($"[Subscriber] Cannot start receiving: {ex.Message}");
               }
          }

          private void ReceiveCallback(IAsyncResult asyncResult)
          {
               var connectionInfo = (ConnectionInfo)asyncResult.AsyncState;

               try
               {
                    int buffSize = _socket.EndReceive(asyncResult, out SocketError response);

                    if (response == SocketError.Success && buffSize > 0)
                    {
                         // Extract complete frames from TCP buffer
                         var frames = connectionInfo.AppendAndExtractFrames(connectionInfo.Data, buffSize);

                         foreach (var frame in frames)
                         {
                              PayloadHandler.Handle(frame);

                              // Consumer ACK: confirm message reception & processing to Broker
                              if (!frame.StartsWith("ACK#") && !frame.StartsWith("INFO#") && !frame.StartsWith("TOPICS#") && !frame.StartsWith("ERROR#"))
                              {
                                   try
                                   {
                                        var payload = SerializationHelper.DeserializePayload(frame, out _);
                                        if (payload != null && !string.IsNullOrEmpty(payload.Id))
                                        {
                                             SendFramed($"ACK#consumed#{payload.Id}");
                                        }
                                   }
                                   catch { }
                              }
                         }

                         // Continue receiving
                         _socket.BeginReceive(
                              connectionInfo.Data,
                              0,
                              connectionInfo.Data.Length,
                              SocketFlags.None,
                              ReceiveCallback,
                              connectionInfo);
                    }
                    else
                    {
                         Console.WriteLine("[Subscriber] Connection closed by Broker.");
                         Close();
                    }
               }
               catch (Exception ex)
               {
                    Console.WriteLine($"[Subscriber] Connection dropped: {ex.Message}");
                    Close();
               }
          }

          public void SendFramed(string text)
          {
               try
               {
                    byte[] data = MessageFraming.Encode(text);
                    _socket.Send(data);
               }
               catch (Exception ex)
               {
                    Console.WriteLine($"[Subscriber] Failed to send: {ex.Message}");
               }
          }

          public void Send(byte[] data)
          {
               try
               {
                    _socket.Send(data);
               }
               catch (Exception e)
               {
                    Console.WriteLine($"Could not send data: {e.Message}");
               }
          }

          public void Close()
          {
               try
               {
                    _socket?.Close();
               }
               catch { }
          }
     }
}
