using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Common;

#nullable disable
namespace Publisher
{
     public class PublisherSocket
     {
          private Socket _socket;
          public bool IsConnected { get; private set; }
          private readonly ManualResetEvent _connectDone = new(false);

          public PublisherSocket()
          {
               _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
          }

          public bool Connect(string ipAddress, int port, int timeoutMs = 5000)
          {
               try
               {
                    Console.WriteLine($"[Publisher] Connecting to Broker at {ipAddress}:{port}...");
                    _connectDone.Reset();

                    var ip = (ipAddress == "localhost") ? IPAddress.Loopback : IPAddress.Parse(ipAddress);
                    var endpoint = new IPEndPoint(ip, port);

                    _socket.BeginConnect(endpoint, ConnectedCallback, _socket);
                    bool connected = _connectDone.WaitOne(timeoutMs);

                    if (connected && _socket.Connected)
                    {
                         IsConnected = true;
                         StartReceiveAcks();
                         return true;
                    }
                    else
                    {
                         Console.WriteLine("[Publisher] Connection timed out.");
                         return false;
                    }
               }
               catch (Exception ex)
               {
                    Console.WriteLine($"[Publisher] Connection error: {ex.Message}");
                    IsConnected = false;
                    return false;
               }
          }

          private void ConnectedCallback(IAsyncResult asyncResult)
          {
               try
               {
                    var sock = (Socket)asyncResult.AsyncState;
                    sock.EndConnect(asyncResult);
                    IsConnected = sock.Connected;
                    Console.WriteLine("[Publisher] Successfully connected to Broker.");
               }
               catch (Exception ex)
               {
                    Console.WriteLine($"[Publisher] Failed to establish connection: {ex.Message}");
                    IsConnected = false;
               }
               finally
               {
                    _connectDone.Set();
               }
          }

          public bool SendPayload(Payload payload, string format = "json")
          {
               if (!IsConnected || _socket == null || !_socket.Connected)
               {
                    Console.WriteLine("[Publisher] Cannot send - not connected to Broker.");
                    return false;
               }

               try
               {
                    string serialized = SerializationHelper.ConvertFormat(payload, format);
                    byte[] data = MessageFraming.Encode(serialized);
                    _socket.Send(data);
                    return true;
               }
               catch (Exception ex)
               {
                    Console.WriteLine($"[Publisher] Failed to send payload: {ex.Message}");
                    IsConnected = false;
                    return false;
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
                    Console.WriteLine($"[Publisher] Could not send data: {e.Message}");
                    IsConnected = false;
               }
          }

          private void StartReceiveAcks()
          {
               var buffer = new byte[1024];
               try
               {
                    _socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, ar =>
                    {
                         try
                         {
                              int bytes = _socket.EndReceive(ar);
                              if (bytes > 0)
                              {
                                   string ack = Encoding.UTF8.GetString(buffer, 0, bytes).Trim();
                                   Console.ForegroundColor = ConsoleColor.DarkGray;
                                   Console.WriteLine($"  <- [Broker Response] {ack}");
                                   Console.ResetColor();

                                   StartReceiveAcks();
                              }
                         }
                         catch
                         {
                              // Connection closed
                         }
                    }, null);
               }
               catch
               {
                    // Ignore background ACK error
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
