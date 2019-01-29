'use strict';
import { workspace, Disposable, ExtensionContext } from 'vscode';
import { LanguageClient, LanguageClientOptions, SettingMonitor, ServerOptions, TransportKind, InitializeParams } from 'vscode-languageclient';
import { Trace } from 'vscode-jsonrpc';

export function activate(context: ExtensionContext) {

    let serverExe = 'dotnet';

    // If the extension is launched in debug mode then the debug server options are used
    // Otherwise the run options are used
    let serverOptions: ServerOptions = {
        run: { command: serverExe, args: ['C:\\Users\\mb\\src\\gh\\nuspec-lsp\\src\\NuSpec.Server\\bin\\Debug\\netcoreapp2.1\\NuSpec.Server.dll'] },
        debug: { command: serverExe, args: ['C:\\Users\\mb\\src\\gh\\nuspec-lsp\\src\\NuSpec.Server\\bin\\Debug\\netcoreapp2.1\\NuSpec.Server.dll'] }
    }

    // Options to control the language client
    let clientOptions: LanguageClientOptions = {
        // Register the server for plain text documents
        documentSelector: [
            {
                pattern: '**/*.nuspec',
            }
        ],
        synchronize: {
            configurationSection: 'nuspec',
            fileEvents: workspace.createFileSystemWatcher('**/*.nuspec')
        },
    }

    // Create the language client and start the client.
    const client = new LanguageClient('nuspec', 'nuspec', serverOptions, clientOptions);
    client.trace = Trace.Verbose;
    let disposable = client.start();

    // Push the disposable to the context's subscriptions so that the
    // client can be deactivated on extension deactivation
    context.subscriptions.push(disposable);
}