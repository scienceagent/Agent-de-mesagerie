using System;

namespace Common
{
     public class Settings
     {
          public static int BROKER_PORT = int.TryParse(Environment.GetEnvironmentVariable("BROKER_PORT"), out var port) ? port : 9000;
          public static string BROKER_IP = Environment.GetEnvironmentVariable("BROKER_IP") ?? "127.0.0.1";
          public static int GRPC_PORT = int.TryParse(Environment.GetEnvironmentVariable("GRPC_PORT"), out var grpcPort) ? grpcPort : 9001;
     }
}
