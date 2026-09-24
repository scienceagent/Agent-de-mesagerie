#nullable disable
using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Newtonsoft.Json;

namespace Common
{
    public static class SerializationHelper
    {
        public static string SerializeJson<T>(T data, bool indented = false)
        {
            return JsonConvert.SerializeObject(data, indented ? Newtonsoft.Json.Formatting.Indented : Newtonsoft.Json.Formatting.None);
        }

        public static T DeserializeJson<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }

        public static string SerializeXml<T>(T data)
        {
            var serializer = new XmlSerializer(typeof(T));
            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = false,
                Encoding = Encoding.UTF8
            };

            using var stringWriter = new StringWriter();
            using var xmlWriter = XmlWriter.Create(stringWriter, settings);
            
            var namespaces = new XmlSerializerNamespaces();
            namespaces.Add(string.Empty, string.Empty);
            
            serializer.Serialize(xmlWriter, data, namespaces);
            return stringWriter.ToString();
        }

        public static T DeserializeXml<T>(string xml)
        {
            var serializer = new XmlSerializer(typeof(T));
            using var stringReader = new StringReader(xml);
            return (T)serializer.Deserialize(stringReader);
        }

        public static string DetectFormat(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return "json";

            var trimmed = content.TrimStart();
            if (trimmed.StartsWith("<"))
                return "xml";

            return "json";
        }

        public static Payload DeserializePayload(string rawData, out string detectedFormat)
        {
            detectedFormat = DetectFormat(rawData);
            if (detectedFormat.Equals("xml", StringComparison.OrdinalIgnoreCase))
            {
                var payload = DeserializeXml<Payload>(rawData);
                payload.Format = "xml";
                return payload;
            }
            else
            {
                var payload = DeserializeJson<Payload>(rawData);
                if (payload != null && string.IsNullOrEmpty(payload.Format))
                {
                    payload.Format = "json";
                }
                return payload;
            }
        }

        /// <summary>
        /// Adapter Pattern: Converts payload representation to the requested format (json or xml)
        /// </summary>
        public static string ConvertFormat(Payload payload, string targetFormat)
        {
            if (targetFormat.Equals("xml", StringComparison.OrdinalIgnoreCase))
            {
                payload.Format = "xml";
                return SerializeXml(payload);
            }

            payload.Format = "json";
            return SerializeJson(payload);
        }
    }
}
