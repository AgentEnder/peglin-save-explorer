import { Link } from "../../components/Link";

export default function Page() {
  return (
    <div className="container mx-auto px-4 py-8">
      <div className="mb-8">
        <h1 className="text-4xl font-bold mb-4">Getting Started</h1>
        <p className="text-lg text-gray-600">
          Quick guide to get you up and running with Peglin Save Explorer
        </p>
      </div>

      <div className="space-y-8">
        <div className="bg-white rounded-lg shadow-md p-6">
          <h2 className="text-2xl font-semibold mb-4">Installation</h2>
          
          <div className="space-y-4">
            <div>
              <h3 className="text-lg font-semibold mb-2">Download the Latest Release</h3>
              <p className="text-gray-600 mb-3">
                Download the appropriate version for your operating system from the GitHub releases page.
              </p>
              <ul className="space-y-2 text-gray-600">
                <li>• <strong>Windows:</strong> peglin-save-explorer-win-x64.zip</li>
                <li>• <strong>macOS:</strong> peglin-save-explorer-osx-x64.tar.gz (Intel) or peglin-save-explorer-osx-arm64.tar.gz (Apple Silicon)</li>
                <li>• <strong>Linux:</strong> peglin-save-explorer-linux-x64.tar.gz</li>
              </ul>
            </div>

            <div>
              <h3 className="text-lg font-semibold mb-2">Extract the Archive</h3>
              <p className="text-gray-600">
                Extract the downloaded archive to a directory of your choice. The executable will be named <code className="bg-gray-100 px-2 py-1 rounded">peglin-save-explorer</code> (or <code className="bg-gray-100 px-2 py-1 rounded">peglin-save-explorer.exe</code> on Windows).
              </p>
            </div>

            <div>
              <h3 className="text-lg font-semibold mb-2">Add to PATH (Optional)</h3>
              <p className="text-gray-600 mb-2">
                For easier access, add the executable to your system's PATH:
              </p>
              <div className="space-y-2">
                <div>
                  <p className="font-medium">Windows:</p>
                  <pre className="bg-gray-900 text-gray-100 p-2 rounded text-sm overflow-x-auto">
                    <code>setx PATH "%PATH%;C:\path\to\peglin-save-explorer"</code>
                  </pre>
                </div>
                <div>
                  <p className="font-medium">macOS/Linux:</p>
                  <pre className="bg-gray-900 text-gray-100 p-2 rounded text-sm overflow-x-auto">
                    <code>export PATH="$PATH:/path/to/peglin-save-explorer"</code>
                  </pre>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div className="bg-white rounded-lg shadow-md p-6">
          <h2 className="text-2xl font-semibold mb-4">Initial Configuration</h2>
          
          <div className="space-y-4">
            <div>
              <h3 className="text-lg font-semibold mb-2">Automatic Detection</h3>
              <p className="text-gray-600">
                The tool will automatically attempt to detect your Peglin installation and save file location on first run.
              </p>
            </div>

            <div>
              <h3 className="text-lg font-semibold mb-2">Manual Configuration</h3>
              <p className="text-gray-600 mb-2">
                If automatic detection fails, you can manually configure the paths:
              </p>
              <pre className="bg-gray-900 text-gray-100 p-3 rounded overflow-x-auto">
                <code>{`# Set save file path
peglin-save-explorer config --set save_file_path="/path/to/save.data"

# Set Peglin installation path
peglin-save-explorer config --set peglin_path="/path/to/Peglin"

# View current configuration
peglin-save-explorer config --list`}</code>
              </pre>
            </div>

            <div>
              <h3 className="text-lg font-semibold mb-2">Default Save File Locations</h3>
              <ul className="space-y-2 text-gray-600">
                <li>
                  <strong>Windows:</strong>
                  <code className="bg-gray-100 px-2 py-1 rounded text-xs">%APPDATA%/../LocalLow/Red Nexus Games/Peglin/</code>
                </li>
                <li>
                  <strong>macOS:</strong>
                  <code className="bg-gray-100 px-2 py-1 rounded text-xs">~/Library/Application Support/Red Nexus Games/Peglin/</code>
                </li>
                <li>
                  <strong>Linux:</strong>
                  <code className="bg-gray-100 px-2 py-1 rounded text-xs">~/.config/unity3d/Red Nexus Games/Peglin/</code>
                </li>
                <li>
                  <strong>Steam Deck:</strong>
                  <code className="bg-gray-100 px-2 py-1 rounded text-xs">~/.local/share/Steam/steamapps/compatdata/1296610/pfx/</code>
                </li>
              </ul>
            </div>
          </div>
        </div>

        <div className="bg-white rounded-lg shadow-md p-6">
          <h2 className="text-2xl font-semibold mb-4">First Steps</h2>
          
          <div className="space-y-4">
            <div>
              <h3 className="text-lg font-semibold mb-2">1. Check Your Statistics</h3>
              <p className="text-gray-600 mb-2">
                View a summary of your Peglin gameplay statistics:
              </p>
              <pre className="bg-gray-900 text-gray-100 p-2 rounded">
                <code>peglin-save-explorer stats</code>
              </pre>
            </div>

            <div>
              <h3 className="text-lg font-semibold mb-2">2. Launch the Web Interface</h3>
              <p className="text-gray-600 mb-2">
                Start the interactive web dashboard:
              </p>
              <pre className="bg-gray-900 text-gray-100 p-2 rounded">
                <code>peglin-save-explorer web</code>
              </pre>
              <p className="text-sm text-gray-600 mt-2">
                This will open your browser at http://localhost:5000
              </p>
            </div>

            <div>
              <h3 className="text-lg font-semibold mb-2">3. View Your Run History</h3>
              <p className="text-gray-600 mb-2">
                See your recent runs:
              </p>
              <pre className="bg-gray-900 text-gray-100 p-2 rounded">
                <code>peglin-save-explorer run-history --limit 10</code>
              </pre>
            </div>

            <div>
              <h3 className="text-lg font-semibold mb-2">4. Explore Interactive Mode</h3>
              <p className="text-gray-600 mb-2">
                Use the menu-driven interface for easier navigation:
              </p>
              <pre className="bg-gray-900 text-gray-100 p-2 rounded">
                <code>peglin-save-explorer interactive</code>
              </pre>
            </div>
          </div>
        </div>

        <div className="bg-white rounded-lg shadow-md p-6">
          <h2 className="text-2xl font-semibold mb-4">Troubleshooting</h2>
          
          <div className="space-y-4">
            <div>
              <h3 className="text-lg font-semibold mb-2">Save File Not Found</h3>
              <p className="text-gray-600">
                If the tool cannot find your save file:
              </p>
              <ul className="mt-2 space-y-1 text-gray-600">
                <li>• Ensure Peglin has been run at least once</li>
                <li>• Check the save file location manually</li>
                <li>• Use the <code className="bg-gray-100 px-1 rounded">--file</code> flag to specify the path</li>
              </ul>
            </div>

            <div>
              <h3 className="text-lg font-semibold mb-2">Permission Denied</h3>
              <p className="text-gray-600">
                On macOS/Linux, you may need to make the executable runnable:
              </p>
              <pre className="bg-gray-900 text-gray-100 p-2 rounded mt-2">
                <code>chmod +x peglin-save-explorer</code>
              </pre>
            </div>

            <div>
              <h3 className="text-lg font-semibold mb-2">Web Interface Not Opening</h3>
              <p className="text-gray-600">
                If the web interface doesn't open automatically:
              </p>
              <ul className="mt-2 space-y-1 text-gray-600">
                <li>• Manually navigate to http://localhost:5000</li>
                <li>• Try a different port: <code className="bg-gray-100 px-1 rounded">--port 8080</code></li>
                <li>• Check if another process is using the port</li>
              </ul>
            </div>
          </div>
        </div>

        <div className="bg-blue-50 rounded-lg p-6">
          <h2 className="text-xl font-semibold mb-3">Next Steps</h2>
          <p className="text-gray-700 mb-4">
            Now that you're set up, explore these resources:
          </p>
          <div className="grid md:grid-cols-2 gap-4">
            <div>
              <Link href="/cli-commands" className="text-blue-600 hover:text-blue-800 font-medium">
                CLI Commands Reference →
              </Link>
              <p className="text-sm text-gray-600 mt-1">
                Learn about all available commands and options
              </p>
            </div>
            <div>
              <Link href="/web-frontend" className="text-blue-600 hover:text-blue-800 font-medium">
                Web Frontend Guide →
              </Link>
              <p className="text-sm text-gray-600 mt-1">
                Master the interactive web dashboard
              </p>
            </div>
          </div>
        </div>
      </div>

      <div className="mt-8">
        <Link href="/" className="text-blue-600 hover:text-blue-800">
          ← Back to Home
        </Link>
      </div>
    </div>
  );
}