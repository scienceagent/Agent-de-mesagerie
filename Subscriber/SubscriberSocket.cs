using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Common;
using Newtonsoft.Json;
#nullable disable


namespace Subscriber
{
     class SubscriberSocket
     {
          private Socket _socket;
          private string _topic;

          public SubscriberSocket(string topic)
          {
               _topic = topic;
               _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
          }
          
          public void Connect(string ipAddress, int port)
          {
               _socket.BeginConnect(new IPEndPoint(IPAddress.Parse(ipAddress), port), ConnectedCallback, null);
               Console.WriteLine("Waiting for connection");

          }

          private void ConnectedCallback(IAsyncResult asyncResult)
          {
               if (_socket.Connected)
               {
                    Console.WriteLine("Subscriber connected to broker.");
                    Subscribe();
                    StartRecieve();
               }
               else
               {
                    Console.WriteLine("Error: Subscriber could not connect to broker.");
               }
          }

          private void Subscribe()
          {
               var data = Encoding.UTF8.GetBytes("subscribe#" + _topic);
               Send(data);
          }

          private void StartRecieve()
          {
               ConnectionInfo connection = new ConnectionInfo();
               connection.Socket = _socket;

               _socket.BeginReceive(connection.Data, 0, connection.Data.Length, SocketFlags.None, RecieveCallback, connection);
          }

          private void RecieveCallback(IAsyncResult asyncResult)
          {
               ConnectionInfo connectionInfo = asyncResult.AsyncState as ConnectionInfo;

               try
               {
                    SocketError response;
                    int buffSize = _socket.EndReceive(asyncResult, out response);

                    if (response == SocketError.Success)
                    {
                         byte[] payloadBytes = new byte[buffSize];
                         Array.Copy(connectionInfo.Data, payloadBytes, payloadBytes.Length);

                         string payloadString = Encoding.UTF8.GetString(payloadBytes);

                         Payload payload = JsonConvert.DeserializeObject<Payload>(payloadString);

                         Console.WriteLine(payload.Message);
                    }
               }
               catch(Exception e)
               {
                    Console.WriteLine($"Can't receive data from broker. {e.Message}");
               }
               finally
               {
                    try
                    {
                         connectionInfo.Socket.BeginReceive(connectionInfo.Data, 0, connectionInfo.Data.Length, SocketFlags.None, RecieveCallback, connectionInfo);

                    }
                    catch (Exception e) 
                    {
                         Console.WriteLine($"{e.Message}");
                         connectionInfo.Socket.Close();

                    

                    }
               }
          }
          private void Send(byte[] data)
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
     }
}
