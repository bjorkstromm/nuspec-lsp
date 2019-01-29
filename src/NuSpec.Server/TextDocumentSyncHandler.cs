using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using Microsoft.Language.Xml;
using OmniSharp.Extensions.Embedded.MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using DiagnosticSeverity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity;

namespace NuSpec.Server
{
    class TextDocumentSyncHandler : ITextDocumentSyncHandler
    {
        private readonly ILanguageServer _router;
        private readonly BufferManager _bufferManager;

        private readonly XmlSchemaSet _schemaSet;

        private readonly DocumentSelector _documentSelector = new DocumentSelector(
            new DocumentFilter()
            {
                Pattern = "**/*.nuspec"
            }
        );

        private SynchronizationCapability _capability;

        public TextDocumentSyncHandler(ILanguageServer router, BufferManager bufferManager)
        {
            _router = router;
            _bufferManager = bufferManager;

            _schemaSet = new XmlSchemaSet();
            using (var xsd = typeof(TextDocumentSyncHandler).Assembly.GetManifestResourceStream("NuSpec.Server.nuspec.xsd"))
            {
                var schema = XmlSchema.Read(xsd, (sender, args) => {});
                _schemaSet.Add(schema);
            }
        }

        public TextDocumentSyncKind Change { get; } = TextDocumentSyncKind.Full;

        public TextDocumentChangeRegistrationOptions GetRegistrationOptions()
        {
            return new TextDocumentChangeRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                SyncKind = Change
            };
        }

        public TextDocumentAttributes GetTextDocumentAttributes(Uri uri)
        {
            return new TextDocumentAttributes(uri, "xml");
        }

        public Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
        {
            var documentPath = request.TextDocument.Uri.ToString();
            var text = request.ContentChanges.FirstOrDefault()?.Text;

            _bufferManager.UpdateBuffer(documentPath, new StringBuffer(text));

            CheckDiagnostics(request.TextDocument.Uri, text);
            return Unit.Task;
        }

        public Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
        {
            _bufferManager.UpdateBuffer(request.TextDocument.Uri.ToString(), new StringBuffer(request.TextDocument.Text));

            CheckDiagnostics(request.TextDocument.Uri, request.TextDocument.Text);
            return Unit.Task;
        }

        public Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
        {
            return Unit.Task;
        }

        public Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
        {
            return Unit.Task;
        }

        public void SetCapability(SynchronizationCapability capability)
        {
            _capability = capability;
        }

        TextDocumentRegistrationOptions IRegistration<TextDocumentRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentRegistrationOptions()
            {
                DocumentSelector = _documentSelector
            };
        }

        TextDocumentSaveRegistrationOptions IRegistration<TextDocumentSaveRegistrationOptions>.GetRegistrationOptions()
        {
            return new TextDocumentSaveRegistrationOptions()
            {
                DocumentSelector = _documentSelector,
                IncludeText = default
            };
        }

        private void CheckDiagnostics(Uri uri, string buffer)
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

            using (var sr = new StringReader(buffer))
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