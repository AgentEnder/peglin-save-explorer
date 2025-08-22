import { Link } from "../../components/Link";
import { useData } from "vike-react/useData";

export default function Page() {
  const { commands } = useData<{ commands: any[] }>();

  return (
    <div className="container max-w-2xl mx-auto px-4 py-8">
      <div className="mb-8">
        <h1 className="text-4xl font-bold mb-4">CLI Commands Reference</h1>
        <p className="text-lg text-gray-600">
          Complete reference for all Peglin Save Explorer command-line interface
          commands
        </p>
      </div>

      <div className="bg-blue-50 rounded-lg p-6 mb-8">
        <h2 className="text-xl font-semibold mb-3">Global Options</h2>
        <p className="text-gray-700 mb-3">
          These options are available for all commands:
        </p>
        <ul className="space-y-2">
          <li>
            <code className="bg-white px-2 py-1 rounded">--verbose</code>
            <span className="ml-2 text-gray-600">
              Enable verbose logging output
            </span>
          </li>
          <li>
            <code className="bg-white px-2 py-1 rounded">--clean</code>
            <span className="ml-2 text-gray-600">
              Clear all caches before executing command
            </span>
          </li>
        </ul>
      </div>

      <div className="space-y-6">
        {commands.map((cmd) => (
          <div key={cmd.name} className="bg-white rounded-lg shadow-md p-6">
            <h2 className="text-2xl font-semibold mb-2" id={cmd.name}>
              <Link
                href={`/cli-commands/${cmd.name}`}
                className="text-blue-600 hover:text-blue-800"
              >
                {cmd.name}
              </Link>
            </h2>
            <p className="text-gray-600 mb-4">{cmd.description}</p>

            {cmd.options.length > 0 && (
              <>
                <h3 className="font-semibold mb-2">
                  Options ({cmd.options.length}):
                </h3>
                <div className="flex flex-wrap gap-2 mb-4">
                  {cmd.options.slice(0, 3).map((opt: any, idx: number) => (
                    <code
                      key={idx}
                      className="bg-gray-100 px-2 py-1 rounded text-sm"
                    >
                      {opt.flags[0]}
                    </code>
                  ))}
                  {cmd.options.length > 3 && (
                    <span className="text-gray-500 text-sm">
                      +{cmd.options.length - 3} more
                    </span>
                  )}
                </div>
              </>
            )}

            <div className="flex justify-between items-center">
              <div>
                <h3 className="font-semibold mb-1 text-sm">Example:</h3>
                <code className="text-sm text-gray-700 bg-gray-50 px-2 py-1 rounded">
                  {cmd.examples[0]}
                </code>
              </div>
              <Link
                href={`/cli-commands/${cmd.name}`}
                className="text-blue-600 hover:text-blue-800 text-sm font-medium"
              >
                View Details →
              </Link>
            </div>
          </div>
        ))}
      </div>

      <div className="mt-8 p-6 bg-gray-50 rounded-lg">
        <h2 className="text-xl font-semibold mb-3">Common Use Cases</h2>
        <div className="space-y-4">
          <div>
            <h3 className="font-semibold">Quick Statistics Overview</h3>
            <pre className="bg-gray-900 text-gray-100 p-2 rounded mt-1 text-sm">
              <code>peglin-save-explorer stats</code>
            </pre>
          </div>
          <div>
            <h3 className="font-semibold">Launch Web Interface</h3>
            <pre className="bg-gray-900 text-gray-100 p-2 rounded mt-1 text-sm">
              <code>peglin-save-explorer web</code>
            </pre>
          </div>
          <div>
            <h3 className="font-semibold">View Recent Winning Runs</h3>
            <pre className="bg-gray-900 text-gray-100 p-2 rounded mt-1 text-sm">
              <code>peglin-save-explorer run-history --wins --limit 5</code>
            </pre>
          </div>
          <div>
            <h3 className="font-semibold">Export All Statistics to JSON</h3>
            <pre className="bg-gray-900 text-gray-100 p-2 rounded mt-1 text-sm">
              <code>
                peglin-save-explorer stats --export my-stats.json --format json
              </code>
            </pre>
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
