using System;
using System.Collections.Generic;
#nullable disable
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
     public class ConnectionInfo
     {
          public const int BUFF_SIZE = 1024;
          public byte[] Data { get; set; }
          public Socket Socket { get; set; }
          
          public string Address { get; set; }

          public string Topic { get; set; }

          public ConnectionInfo()
          {
               Data = new byte[BUFF_SIZE];
          }

     }
}
