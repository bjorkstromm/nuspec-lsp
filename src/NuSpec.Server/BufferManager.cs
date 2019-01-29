using System.Collections.Concurrent;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Buffer = Microsoft.Language.Xml.Buffer;

namespace NuSpec.Server
{
    class BufferManager
    {
        private readonly ILanguageServer _router;

        public BufferManager(ILanguageServer router)
        {
            _router = router;
        }

        private ConcurrentDictionary<string, Buffer> _buffers = new ConcurrentDictionary<string, Buffer>();

        public void UpdateBuffer(string documentPath, Buffer buffer)
        {
            _buffers.AddOrUpdate(documentPath, buffer, (k, v) => buffer);
        }

        public Buffer GetBuffer(string documentPath)
        {
            return _buffers.TryGetValue(documentPath, out var buffer) ? buffer : null;
        }
    }
}