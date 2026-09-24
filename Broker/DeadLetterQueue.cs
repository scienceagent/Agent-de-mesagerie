using System;
using System.IO;

namespace Broker
{
    /// <summary>
    /// Handles invalid, malformed, or undeliverable messages (DLQ).
    /// Prevents broker crashes and ensures no data is lost for auditing.
    /// </summary>
    public static class DeadLetterQueue
    {
        private static readonly string _dlqFilePath;
        private static readonly object _fileLock = new object();

        static DeadLetterQueue()
        {
            string storageDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "storage");
            if (!Directory.Exists(storageDir))
            {
                Directory.CreateDirectory(storageDir);
            }
            _dlqFilePath = Path.Combine(storageDir, "dead_letter.journal");
        }

        public static void RecordDeadLetter(string rawFrame, string sourceAddress, string reason)
        {
            try
            {
                lock (_fileLock)
                {
                    string timestamp = DateTime.UtcNow.ToString("o");
                    // Format: [TIMESTAMP] [SOURCE] [REASON] | RAW_FRAME
                    string logEntry = $"[{timestamp}] [{sourceAddress}] [{reason}] | {rawFrame}";
                    
                    File.AppendAllLines(_dlqFilePath, new[] { logEntry });
                    Console.WriteLine($"[DLQ] Malformed message from [{sourceAddress}] saved to dead letter queue.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DLQ] CRITICAL ERROR writing to Dead Letter Queue: {ex.Message}");
            }
        }
    }
}
