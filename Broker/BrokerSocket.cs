using System;
using System.Net;
using System.Net.Sockets;
using Common;

#nullable disable
namespace Broker
{
     public class BrokerSocket
     {
          private const int CONNECTIONS_LIMIT = 128;
          private Socket _socket;

          public BrokerSocket()
          {
               _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
               // Enable address reuse
               _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
          }

          public void Start(string ip, int port)
          {
               IPAddress bindAddress = (ip == "0.0.0.0" || ip == "*") ? IPAddress.Any : IPAddress.Parse(ip);
               _socket.Bind(new IPEndPoint(bindAddress, port));
               _socket.Listen(CONNECTIONS_LIMIT);
               Console.WriteLine($"[Broker] TCP Socket Server listening on {bindAddress}:{port}");
               Accept();
          }

          private void Accept()
          {
               try
               {
                    _socket.BeginAccept(AcceptedCallback, null);
               }
               catch (Exception e)
               {
                    Console.WriteLine($"[Broker] Accept scheduling error: {e.Message}");
               }
          }

          private void AcceptedCallback(IAsyncResult asyncResult)
          {
               ConnectionInfo connection = new ConnectionInfo();

               try
               {
                    connection.Socket = _socket.EndAccept(asyncResult);
                    connection.Address = connection.Socket.RemoteEndPoint.ToString();
                    ConnectionStorage.Add(connection);

                    Console.WriteLine($"[Broker] New connection accepted from {connection.Address}");

                    connection.Socket.BeginReceive(
                         connection.Data,
                         0,
                         connection.Data.Length,
                         SocketFlags.None,
                         ReceiveCallback,
                         connection);
               }
               catch (Exception e)
               {
                    Console.WriteLine($"[Broker] Can't accept connection: {e.Message}");
               }
               finally
               {
                    Accept();
               }
          }

          private void ReceiveCallback(IAsyncResult asyncResult)
          {
               ConnectionInfo connection = asyncResult.AsyncState as ConnectionInfo;
               if (connection == null || connection.Socket == null)
                    return;

               bool keepReceiving = true;

               try
               {
                    Socket senderSocket = connection.Socket;
                    int buffSize = senderSocket.EndReceive(asyncResult, out SocketError response);

                    if (response == SocketError.Success && buffSize > 0)
                    {
                         // Extract all delimited frames from stream buffer
                         var frames = connection.AppendAndExtractFrames(connection.Data, buffSize);

                         // Process each extracted message frame
                         foreach (var frame in frames)
                         {
                              PayloadHandler.Handle(frame, connection);
                         }
                    }
                    else
                    {
                         keepReceiving = false;
                         Console.WriteLine($"[Broker] Connection closed by remote host: {connection.Address}");
                    }
               }
               catch (Exception e)
               {
                    keepReceiving = false;
                    Console.WriteLine($"[Broker] Client disconnected or read error [{connection.Address}]: {e.Message}");
               }
               finally
               {
                    if (keepReceiving && connection.Socket.Connected)
                    {
                         try
                         {
                              connection.Socket.BeginReceive(
                                   connection.Data,
                                   0,
                                   connection.Data.Length,
                                   SocketFlags.None,
                                   ReceiveCallback,
                                   connection);
                         }
                         catch (Exception e)
                         {
                              Console.WriteLine($"[Broker] Error continuing receive: {e.Message}");
                              CleanupConnection(connection);
                         }
                    }
                    else
                    {
                         CleanupConnection(connection);
                    }
               }
          }

          private void CleanupConnection(ConnectionInfo connection)
          {
               try
               {
                    if (connection?.Address != null)
                    {
                         ConnectionStorage.Remove(connection.Address);
                         Console.WriteLine($"[Broker] Cleaned up resources for client: {connection.Address}");
                    }
                    connection?.Socket?.Close();
               }
               catch
               {
                    // Ignore cleanup exceptions
               }
          }
     }
}