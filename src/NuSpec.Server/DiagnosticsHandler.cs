
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using Microsoft.Language.Xml;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Buffer = Microsoft.Language.Xml.Buffer;
using DiagnosticSeverity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity;

namespace NuSpec.Server
{
    class DiagnosticsHandler
    {
        private readonly ILanguageServer _router;
        private readonly BufferManager _bufferManager;
        private readonly XmlSchemaSet _schemaSet;
        private static readonly IReadOnlyCollection<string> TemplatedValues = new []
        {
            "__replace",
            "space_separated",
            "tag1"
        };

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
            var text = buffer.GetText(0, buffer.Length);
            var syntaxTree = Parser.Parse(buffer);
            var textPositions = new TextPositions(text);
            var diagnostics = new List<Diagnostic>();

            diagnostics.AddRange(ValidateAgainstSchema( text));
            diagnostics.AddRange(ValidateTemplatedValues(syntaxTree, textPositions));

            _router.Document.PublishDiagnostics(new PublishDiagnosticsParams
            {
                Uri = uri,
                Diagnostics = diagnostics
            });
        }

        private IEnumerable<Diagnostic> ValidateTemplatedValues(XmlDocumentSyntax syntaxTree, TextPositions textPositions)
        {
            foreach (var node in syntaxTree.DescendantNodesAndSelf().OfType<XmlTextSyntax>())
            {
                if (!TemplatedValues.Any(x => node.Value.Contains(x, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var range = textPositions.GetRange(node.Start, node.End);

                yield return new Diagnostic {
                    Message = "Templated value which should be removed",
                    Severity = DiagnosticSeverity.Error,
                    Range = range
                };
            }
        }

        private IEnumerable<Diagnostic> ValidateAgainstSchema(string text)
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

            using (var sr = new StringReader(text))
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

            return diagnostics;
        }
    }
}