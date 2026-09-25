using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace Common
{
    /// <summary>
    /// XML Validation and Processing Module (covering XSD Schema, DOM, and SAX/Streaming models).
    /// </summary>
    public static class XmlValidation
    {
        private static readonly XmlSchemaSet _schemaSet;
        private static readonly string _xsdContent;

        static XmlValidation()
        {
            _schemaSet = new XmlSchemaSet();

            // Load schema from file or embedded fallback
            string xsdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PayloadSchema.xsd");
            if (!File.Exists(xsdPath))
            {
                // Try parent/common directory
                string altPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Common", "PayloadSchema.xsd");
                if (File.Exists(altPath))
                {
                    xsdPath = Path.GetFullPath(altPath);
                }
            }

            if (File.Exists(xsdPath))
            {
                _xsdContent = File.ReadAllText(xsdPath);
                using var reader = XmlReader.Create(new StringReader(_xsdContent));
                _schemaSet.Add(null, reader);
            }
            else
            {
                // Fallback inline schema definition
                _xsdContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<xs:schema xmlns:xs=""http://www.w3.org/2001/XMLSchema"" elementFormDefault=""qualified"">
  <xs:element name=""payload"">
    <xs:complexType>
      <xs:sequence>
        <xs:element name=""id"" type=""xs:string"" minOccurs=""0"" maxOccurs=""1"" />
        <xs:element name=""topic"" minOccurs=""1"" maxOccurs=""1"">
          <xs:simpleType>
            <xs:restriction base=""xs:string"">
              <xs:minLength value=""1"" />
            </xs:restriction>
          </xs:simpleType>
        </xs:element>
        <xs:element name=""message"" type=""xs:string"" minOccurs=""1"" maxOccurs=""1"" />
        <xs:element name=""timestamp"" type=""xs:string"" minOccurs=""0"" maxOccurs=""1"" />
        <xs:element name=""format"" type=""xs:string"" minOccurs=""0"" maxOccurs=""1"" />
        <xs:element name=""sender"" type=""xs:string"" minOccurs=""0"" maxOccurs=""1"" />
      </xs:sequence>
    </xs:complexType>
  </xs:element>
</xs:schema>";
                using var reader = XmlReader.Create(new StringReader(_xsdContent));
                _schemaSet.Add(null, reader);
            }

            _schemaSet.Compile();
        }

        /// <summary>
        /// Validates XML content strictly against the XSD Schema using SAX streaming reader.
        /// </summary>
        public static bool ValidateXml(string xmlContent, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(xmlContent))
            {
                errorMessage = "XML content is empty or null.";
                return false;
            }

            var errors = new StringBuilder();

            var settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
                Schemas = _schemaSet,
                DtdProcessing = DtdProcessing.Prohibit
            };

            settings.ValidationEventHandler += (sender, args) =>
            {
                errors.AppendLine($"[{args.Severity}] Line {args.Exception.LineNumber}, Pos {args.Exception.LinePosition}: {args.Message}");
            };

            try
            {
                using var stringReader = new StringReader(xmlContent);
                using var xmlReader = XmlReader.Create(stringReader, settings);

                while (xmlReader.Read())
                {
                    // Streaming SAX-style forward consumption validating each token against XSD
                }

                if (errors.Length > 0)
                {
                    errorMessage = errors.ToString().TrimEnd();
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"XML Syntax/Schema Error: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// DOM (Document Object Model) processing demonstration: builds an in-memory node tree.
        /// </summary>
        public static XDocument ParseWithDom(string xmlContent)
        {
            return XDocument.Parse(xmlContent);
        }

        /// <summary>
        /// SAX (Simple API for XML) / Streaming processing demonstration: forward-only, event-driven reader.
        /// </summary>
        public static void InspectWithSax(string xmlContent, Action<string, string> onElementFound)
        {
            using var stringReader = new StringReader(xmlContent);
            using var reader = XmlReader.Create(stringReader);

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    string elemName = reader.Name;
                    string elemValue = reader.ReadElementContentAsString();
                    onElementFound?.Invoke(elemName, elemValue);
                }
            }
        }
    }
}
