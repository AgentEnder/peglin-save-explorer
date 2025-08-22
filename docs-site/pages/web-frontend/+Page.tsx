import { Link } from "../../components/Link";

export default function Page() {
  return (
    <div className="container mx-auto px-4 py-8">
      <div className="mb-8">
        <h1 className="text-4xl font-bold mb-4">Web Frontend Guide</h1>
        <p className="text-lg text-gray-600">
          Interactive web dashboard for exploring Peglin save files visually
        </p>
      </div>

      <div className="bg-blue-50 rounded-lg p-6 mb-8">
        <h2 className="text-xl font-semibold mb-3">Getting Started</h2>
        <p className="text-gray-700 mb-3">
          The web frontend provides a user-friendly interface to explore your Peglin save data.
        </p>
        <pre className="bg-gray-900 text-gray-100 p-3 rounded">
          <code>peglin-save-explorer web</code>
        </pre>
        <p className="text-sm text-gray-600 mt-2">
          Opens your browser at http://localhost:5000
        </p>
      </div>

      <div className="space-y-6">
        <div className="bg-white rounded-lg shadow-md p-6">
          <h2 className="text-2xl font-semibold mb-4">Dashboard</h2>
          <p className="text-gray-600 mb-4">
            The main dashboard provides an overview of your Peglin statistics and recent activity.
          </p>
          <div className="grid md:grid-cols-2 gap-4">
            <div>
              <h3 className="font-semibold mb-2">Overview Cards</h3>
              <ul className="space-y-1 text-sm text-gray-600">
                <li>• Total runs completed</li>
                <li>• Total wins achieved</li>
                <li>• Overall win rate percentage</li>
                <li>• Best performing character class</li>
              </ul>
            </div>
            <div>
              <h3 className="font-semibold mb-2">Charts & Visualizations</h3>
              <ul className="space-y-1 text-sm text-gray-600">
                <li>• Wins by character class (bar chart)</li>
                <li>• Average damage per run</li>
                <li>• Recent runs timeline</li>
                <li>• Quick access to detailed views</li>
              </ul>
            </div>
          </div>
        </div>

        <div className="bg-white rounded-lg shadow-md p-6">
          <h2 className="text-2xl font-semibold mb-4">Run History</h2>
          <p className="text-gray-600 mb-4">
            Browse and filter through all your past runs with advanced search capabilities.
          </p>
          <h3 className="font-semibold mb-2">Features:</h3>
          <ul className="space-y-2 text-gray-600">
            <li>
              <span className="font-medium">Advanced Filtering:</span> Filter by character class, win/loss status, date range, and damage thresholds
            </li>
            <li>
              <span className="font-medium">Sortable Columns:</span> Sort by any column including date, damage, floor reached, and more
            </li>
            <li>
              <span className="font-medium">Search:</span> Quick search across all run data
            </li>
            <li>
              <span className="font-medium">Pagination:</span> Efficiently browse large datasets with customizable page sizes
            </li>
            <li>
              <span className="font-medium">Run Details:</span> Click any run to view complete details including orbs, relics, and battle history
            </li>
          </ul>
        </div>

        <div className="bg-white rounded-lg shadow-md p-6">
          <h2 className="text-2xl font-semibold mb-4">Run Details View</h2>
          <p className="text-gray-600 mb-4">
            Deep dive into individual runs with comprehensive information.
          </p>
          <div className="grid md:grid-cols-2 gap-4">
            <div>
              <h3 className="font-semibold mb-2">Basic Information</h3>
              <ul className="space-y-1 text-sm text-gray-600">
                <li>• Character class and starting stats</li>
                <li>• Final floor reached</li>
                <li>• Total damage dealt</li>
                <li>• Victory/defeat status</li>
                <li>• Run duration and date</li>
              </ul>
            </div>
            <div>
              <h3 className="font-semibold mb-2">Equipment & Items</h3>
              <ul className="space-y-1 text-sm text-gray-600">
                <li>• Complete orb collection with sprites</li>
                <li>• All relics acquired during run</li>
                <li>• Visual representation of items</li>
                <li>• Rarity indicators</li>
                <li>• Item descriptions and effects</li>
              </ul>
            </div>
          </div>
        </div>

        <div className="bg-white rounded-lg shadow-md p-6">
          <h2 className="text-2xl font-semibold mb-4">Statistics</h2>
          <p className="text-gray-600 mb-4">
            Detailed analytics and insights about your gameplay patterns.
          </p>
          <h3 className="font-semibold mb-2">Available Charts:</h3>
          <div className="grid md:grid-cols-2 gap-4">
            <ul className="space-y-2 text-gray-600">
              <li>• Win rate by character class</li>
              <li>• Most frequently used orbs</li>
              <li>• Orb win rate correlation</li>
              <li>• Damage distribution analysis</li>
            </ul>
            <ul className="space-y-2 text-gray-600">
              <li>• Activity over time</li>
              <li>• Floor progression trends</li>
              <li>• Character class preferences</li>
              <li>• Relic collection statistics</li>
            </ul>
          </div>
        </div>

        <div className="bg-white rounded-lg shadow-md p-6">
          <h2 className="text-2xl font-semibold mb-4">File Upload</h2>
          <p className="text-gray-600 mb-4">
            Upload and analyze save files directly through the web interface.
          </p>
          <h3 className="font-semibold mb-2">How to Use:</h3>
          <ol className="space-y-2 text-gray-600">
            <li>1. Navigate to the File Upload tab</li>
            <li>2. Drag and drop your save file or click to browse</li>
            <li>3. Supported formats: .data and .save files</li>
            <li>4. File is processed locally - no data leaves your machine</li>
            <li>5. View results immediately after processing</li>
          </ol>
          <div className="mt-4 p-4 bg-gray-50 rounded">
            <h4 className="font-semibold mb-2">Save File Locations:</h4>
            <ul className="space-y-1 text-sm text-gray-600">
              <li><strong>Windows:</strong> %APPDATA%/../LocalLow/Red Nexus Games/Peglin/</li>
              <li><strong>macOS:</strong> ~/Library/Application Support/Red Nexus Games/Peglin/</li>
              <li><strong>Linux:</strong> ~/.config/unity3d/Red Nexus Games/Peglin/</li>
              <li><strong>Steam Deck:</strong> ~/.local/share/Steam/steamapps/compatdata/1296610/pfx/</li>
            </ul>
          </div>
        </div>

        <div className="bg-white rounded-lg shadow-md p-6">
          <h2 className="text-2xl font-semibold mb-4">Configuration</h2>
          <p className="text-gray-600 mb-4">
            Customize the web interface to your preferences.
          </p>
          <h3 className="font-semibold mb-2">Settings Available:</h3>
          <ul className="space-y-2 text-gray-600">
            <li>• Save file path configuration</li>
            <li>• Peglin installation directory</li>
            <li>• Auto-refresh intervals</li>
            <li>• Export format preferences</li>
            <li>• Display options and themes</li>
          </ul>
        </div>

        <div className="bg-white rounded-lg shadow-md p-6">
          <h2 className="text-2xl font-semibold mb-4">Data Export</h2>
          <p className="text-gray-600 mb-4">
            Export your data in various formats for external analysis.
          </p>
          <div className="grid md:grid-cols-2 gap-4">
            <div>
              <h3 className="font-semibold mb-2">Export Formats</h3>
              <ul className="space-y-1 text-sm text-gray-600">
                <li>• JSON - Complete data structure</li>
                <li>• CSV - Spreadsheet compatible</li>
                <li>• Individual run exports</li>
                <li>• Batch export options</li>
              </ul>
            </div>
            <div>
              <h3 className="font-semibold mb-2">Export Options</h3>
              <ul className="space-y-1 text-sm text-gray-600">
                <li>• Filter before export</li>
                <li>• Select specific fields</li>
                <li>• Include/exclude metadata</li>
                <li>• Compression options</li>
              </ul>
            </div>
          </div>
        </div>
      </div>

      <div className="mt-8 p-6 bg-gray-50 rounded-lg">
        <h2 className="text-xl font-semibold mb-3">Tips & Tricks</h2>
        <ul className="space-y-2 text-gray-600">
          <li>• Use keyboard shortcuts for navigation (press ? for help)</li>
          <li>• Double-click on charts to zoom in</li>
          <li>• Hold Shift to select multiple runs for comparison</li>
          <li>• The interface auto-saves your filter preferences</li>
          <li>• Export data regularly for backup purposes</li>
        </ul>
      </div>

      <div className="mt-8">
        <Link href="/" className="text-blue-600 hover:text-blue-800">
          ← Back to Home
        </Link>
      </div>
    </div>
  );
}