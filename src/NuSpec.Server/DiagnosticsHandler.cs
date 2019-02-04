
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Buffer = Microsoft.Language.Xml.Buffer;

namespace NuSpec.Server
{
    class DiagnosticsHandler
    {
        private readonly ILanguageServer _router;
        private readonly BufferManager _bufferManager;
        private readonly XmlSchemaSet _schemaSet;

        public DiagnosticsHandler(ILanguageServer router, BufferManager bufferManager)
        {
            _router = router;
            _bufferManager = bufferManager;
            _schemaSet = new XmlSchemaSet();
            using (var xsd = typeof(DiagnosticsHandler).Assembly.GetManifestResourceStream("NuSpec.Server.nuspec.xsd"))
            using (var sr = new StreamReader(xsd))
            {
                var schemaContent = string.Format(sr.ReadToEnd(), "http://schemas.microsoft.com/packaging/2015/06/nuspec.xsd");

                using (var ms = new MemoryStream())
                using (var sw = new StreamWriter(ms))
                {
                    sw.Write(schemaContent);
                    sw.Flush();
                    ms.Position = 0;

                    var schema = XmlSchema.Read(ms, (sender, args) => {});
                    _schemaSet.Add(schema);
                }
            }
        }

        public void PublishDiagnostics(Uri uri, Buffer buffer)
        {
            var diagnostics = new List<Diagnostic>();

            var settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
                Schemas = _schemaSet,
                ValidationFlags = XmlSchemaValidationFlags.ProcessInlineSchema |
                                  XmlSchemaValidationFlags.ProcessSchemaLocation |
                                  XmlSchemaValidationFlags.ReportValidationWarnings,
                XmlResolver = new XmlUrlResolver(),
            };
            settings.ValidationEventHandler += (sender, args) =>
            {
                diagnostics.Add(new Diagnostic {
                    Message = args.Message,
                    Severity = DiagnosticSeverity.Error,
                    Range = new Range(
                        new Position(args.Exception.LineNumber - 1, args.Exception.LinePosition -1),
                        new Position(args.Exception.LineNumber - 1, args.Exception.LinePosition -1)
                    )
                });
            };

            using (var sr = new StringReader(buffer.GetText(0, buffer.Length)))
            using (var reader = XmlReader.Create(sr, settings))
            {
                try
                {
                    while(reader.Read());
                }
                catch (XmlException e)
                {
                    diagnostics.Add(new Diagnostic {
                        Message = e.Message,
                        Severity = DiagnosticSeverity.Error,
                        Range = new Range(
                            new Position(e.LineNumber - 1, e.LinePosition -1),
                            new Position(e.LineNumber - 1, e.LinePosition -1)
                        )
                    });
                }
            }

            _router.Document.PublishDiagnostics(new PublishDiagnosticsParams
            {
                Uri = uri,
                Diagnostics = diagnostics
            });
        }
    }
}