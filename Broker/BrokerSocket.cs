#nullable disable
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Common;

namespace Broker
{
     class BrokerSocket
     {
          private const int CONNECTIONS_LIMIT = 8;

          private Socket _socket;

          public BrokerSocket()
          {
               _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
          }

          public void Start(string ip, int port)
          {
               _socket.Bind(new IPEndPoint(IPAddress.Parse(ip), port));
               _socket.Listen(CONNECTIONS_LIMIT);
               Accept();
          }

          private void Accept()
          {
               _socket.BeginAccept(AcceptedCallback, null);

          }
          private void AcceptedCallback(IAsyncResult asyncResult)
          {
               ConnectionInfo connection = new ConnectionInfo();

               try
               {
                    connection.Socket = _socket.EndAccept(asyncResult);
                    connection.Address = connection.Socket.RemoteEndPoint.ToString();
                    connection.Socket.BeginReceive(connection.Data, 0, connection.Data.Length, SocketFlags.None, RecieveCallback, connection);

               }
               catch (Exception e)
               {
                    Console.WriteLine($"Can't accept. {e.Message}");
               }
               finally
               {
                    Accept();
               }

          }
          private void RecieveCallback(IAsyncResult asyncResult)
          {
               ConnectionInfo connection = asyncResult.AsyncState as ConnectionInfo;
               bool keepReceiving = true;

               try
               {
                    Socket senderSocket = connection.Socket;
                    SocketError response;
                    int buffSize = senderSocket.EndReceive(asyncResult, out response);

                    if (response == SocketError.Success && buffSize > 0)
                    {
                         byte[] payload = new byte[buffSize];
                         Array.Copy(connection.Data, payload, payload.Length);

                         PayloadHandler.Handle(payload, connection);
                    }
                    else
                    {
                         keepReceiving = false;
                         Console.WriteLine("An existing connection was forcibly closed by the remote host.");
                    }
               }
               catch (Exception e)
               {
                    keepReceiving = false;
                    Console.WriteLine($"Can't receive data: {e.Message}");
               }
               finally
               {
                    if (keepReceiving)
                    {
                         try
                         {
                              connection.Socket.BeginReceive(
                                  connection.Data,
                                  0,
                                  connection.Data.Length,
                                  SocketFlags.None,
                                  RecieveCallback,
                                  connection);
                         }
                         catch (Exception e)
                         {
                              Console.WriteLine($"{e.Message}");

                              var address = connection.Socket.RemoteEndPoint.ToString();

                              ConnectionStorage.Remove(address);
                              connection.Socket.Close();
                         }
                    }
                    else
                    {
                         var address = connection.Socket.RemoteEndPoint.ToString();

                         ConnectionStorage.Remove(address);
                         connection.Socket.Close();
                    }
               }
          }
     }
}