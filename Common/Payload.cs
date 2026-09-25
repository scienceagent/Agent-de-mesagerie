using System;
using System.Xml.Serialization;
using Newtonsoft.Json;

#nullable disable
namespace Common
{
     [XmlRoot("payload")]
     public class Payload
     {
          [XmlElement("id")]
          [JsonProperty("id")]
          public string Id { get; set; } = Guid.NewGuid().ToString();

          [XmlElement("topic")]
          [JsonProperty("topic")]
          public string Topic { get; set; }

          [XmlElement("message")]
          [JsonProperty("message")]
          public string Message { get; set; }

          [XmlElement("timestamp")]
          [JsonProperty("timestamp")]
          public DateTime Timestamp { get; set; } = DateTime.UtcNow;

          [XmlElement("format")]
          [JsonProperty("format")]
          public string Format { get; set; } = "json";

          [XmlElement("sender")]
          [JsonProperty("sender")]
          public string Sender { get; set; } = "anonymous";

          [XmlElement("sequence_number")]
          [JsonProperty("sequence_number")]
          public long SequenceNumber { get; set; } = 0;

          public override string ToString()
          {
               return $"[ID: {Id}] [Topic: {Topic}] [Time: {Timestamp:HH:mm:ss}] [Format: {Format}] [From: {Sender}]: {Message}";
          }
     }
}

