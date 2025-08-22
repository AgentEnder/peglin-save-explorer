import { Link } from "../../components/Link";

export default function Page() {
  return (
    <div className="container mx-auto px-4 py-8">
      <div className="mb-8">
        <h1 className="text-4xl font-bold mb-4">Peglin Save Explorer</h1>
        <p className="text-lg text-gray-600">
          A comprehensive tool for analyzing and exploring Peglin save files
        </p>
      </div>

      <div className="grid md:grid-cols-2 gap-6 mb-8">
        <div className="bg-white rounded-lg shadow-md p-6">
          <h2 className="text-2xl font-semibold mb-3">CLI Commands</h2>
          <p className="text-gray-600 mb-4">
            Powerful command-line interface for save file analysis
          </p>
          <div className="flex flex-wrap gap-2 mb-4">
            <span className="px-2 py-1 bg-blue-100 text-blue-800 rounded text-sm">analyze-save</span>
            <span className="px-2 py-1 bg-blue-100 text-blue-800 rounded text-sm">view-run</span>
            <span className="px-2 py-1 bg-blue-100 text-blue-800 rounded text-sm">stats</span>
            <span className="px-2 py-1 bg-blue-100 text-blue-800 rounded text-sm">web</span>
          </div>
          <Link href="/cli-commands" className="text-blue-600 hover:text-blue-800">
            View CLI Documentation →
          </Link>
        </div>

        <div className="bg-white rounded-lg shadow-md p-6">
          <h2 className="text-2xl font-semibold mb-3">Web Interface</h2>
          <p className="text-gray-600 mb-4">
            Interactive web dashboard for visual exploration
          </p>
          <div className="flex flex-wrap gap-2 mb-4">
            <span className="px-2 py-1 bg-green-100 text-green-800 rounded text-sm">Dashboard</span>
            <span className="px-2 py-1 bg-green-100 text-green-800 rounded text-sm">Run History</span>
            <span className="px-2 py-1 bg-green-100 text-green-800 rounded text-sm">Statistics</span>
            <span className="px-2 py-1 bg-green-100 text-green-800 rounded text-sm">File Upload</span>
          </div>
          <Link href="/web-frontend" className="text-blue-600 hover:text-blue-800">
            View Web Frontend Guide →
          </Link>
        </div>
      </div>

      <div className="bg-gray-50 rounded-lg p-6 mb-8">
        <h2 className="text-2xl font-semibold mb-4">Quick Start</h2>
        <div className="space-y-4">
          <div>
            <h3 className="font-semibold mb-1">1. Install the tool</h3>
            <p className="text-gray-600 text-sm">
              Download the latest release from GitHub for your platform
            </p>
          </div>
          <div>
            <h3 className="font-semibold mb-1">2. Start the web interface</h3>
            <pre className="bg-gray-900 text-gray-100 p-3 rounded overflow-x-auto">
              <code>peglin-save-explorer web</code>
            </pre>
          </div>
          <div>
            <h3 className="font-semibold mb-1">3. Or use CLI commands</h3>
            <pre className="bg-gray-900 text-gray-100 p-3 rounded overflow-x-auto">
              <code>peglin-save-explorer stats</code>
            </pre>
          </div>
        </div>
      </div>

      <div className="grid md:grid-cols-3 gap-6">
        <div className="bg-white rounded-lg shadow-md p-6">
          <h3 className="text-xl font-semibold mb-3">Features</h3>
          <ul className="space-y-2 text-sm text-gray-600">
            <li>• Analyze run history and statistics</li>
            <li>• View detailed orb and relic information</li>
            <li>• Export data in multiple formats</li>
            <li>• Interactive web dashboard</li>
            <li>• Cross-platform support</li>
          </ul>
        </div>

        <div className="bg-white rounded-lg shadow-md p-6">
          <h3 className="text-xl font-semibold mb-3">Supported Platforms</h3>
          <ul className="space-y-2 text-sm text-gray-600">
            <li>• Windows</li>
            <li>• macOS</li>
            <li>• Linux</li>
            <li>• Steam Deck</li>
          </ul>
        </div>

        <div className="bg-white rounded-lg shadow-md p-6">
          <h3 className="text-xl font-semibold mb-3">Resources</h3>
          <ul className="space-y-2 text-sm">
            <li>
              <Link href="/getting-started" className="text-blue-600 hover:text-blue-800">
                Getting Started Guide
              </Link>
            </li>
            <li>
              <Link href="/cli-commands" className="text-blue-600 hover:text-blue-800">
                CLI Reference
              </Link>
            </li>
            <li>
              <Link href="/web-frontend" className="text-blue-600 hover:text-blue-800">
                Web Frontend Guide
              </Link>
            </li>
          </ul>
        </div>
      </div>
    </div>
  );
}
