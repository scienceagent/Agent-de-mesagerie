using System;
using System.Collections.Generic;
using System.Text;

namespace Common
{
    /// <summary>
    /// Delimiter-based framing for TCP streams to handle message fragmentation and packet coalescing.
    /// Uses '\n' as the frame boundary.
    /// </summary>
    public static class MessageFraming
    {
        public const char DELIMITER = '\n';

        public static byte[] Encode(string message)
        {
            if (string.IsNullOrEmpty(message))
                return Array.Empty<byte>();

            // Ensure single-line frame ending with delimiter
            string normalized = message.Replace("\r\n", " ").Replace("\n", " ").Trim() + DELIMITER;
            return Encoding.UTF8.GetBytes(normalized);
        }

        public static List<string> Decode(ref string buffer)
        {
            var frames = new List<string>();
            if (string.IsNullOrEmpty(buffer))
                return frames;

            int delimiterIndex;
            while ((delimiterIndex = buffer.IndexOf(DELIMITER)) >= 0)
            {
                string frame = buffer.Substring(0, delimiterIndex).Trim();
                buffer = buffer.Substring(delimiterIndex + 1);

                if (!string.IsNullOrWhiteSpace(frame))
                {
                    frames.Add(frame);
                }
            }

            return frames;
        }
    }
}
