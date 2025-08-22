import { Link } from "../../../components/Link";
import { useData } from "vike-react/useData";

export default function Page() {
  const { command } = useData<{ command: any }>();

  return (
    <div className="container max-w-2xl mx-auto px-4 py-8">
      <div className="mb-8">
        <nav className="mb-4">
          <Link
            href="/cli-commands"
            className="text-blue-600 hover:text-blue-800"
          >
            ← Back to CLI Commands
          </Link>
        </nav>
        <h1 className="text-4xl font-bold mb-4">{command.name}</h1>
        <p className="text-lg text-gray-600">{command.description}</p>
      </div>

      {command.options && command.options.length > 0 && (
        <div className="bg-white rounded-lg shadow-md p-6 mb-8">
          <h2 className="text-2xl font-semibold mb-4">Options</h2>
          <div className="space-y-4">
            {command.options.map((option: any, index: number) => (
              <div key={index} className="border-l-4 border-blue-200 pl-4">
                <div className="flex flex-wrap gap-2 mb-2">
                  {option.flags.map((flag: string, flagIndex: number) => (
                    <code
                      key={flagIndex}
                      className="bg-gray-100 px-2 py-1 rounded text-sm font-mono"
                    >
                      {String(flag)}
                    </code>
                  ))}
                  {option.required && (
                    <span className="bg-red-100 text-red-800 px-2 py-1 rounded text-xs font-semibold">
                      Required
                    </span>
                  )}
                  {option.defaultValue && (
                    <span className="bg-blue-100 text-blue-800 px-2 py-1 rounded text-xs">
                      Default: {String(option.defaultValue)}
                    </span>
                  )}
                </div>
                <p className="text-gray-600 text-sm mb-2">
                  {String(option.description)}
                </p>
                <p className="text-gray-500 text-xs">Type: {String(option.type)}</p>
              </div>
            ))}
          </div>
        </div>
      )}

      <div className="bg-white rounded-lg shadow-md p-6 mb-8">
        <h2 className="text-2xl font-semibold mb-4">Examples</h2>
        <div className="space-y-4">
          {command.examples.map((example: string, index: number) => (
            <div key={index}>
              <pre className="bg-gray-900 text-gray-100 p-3 rounded overflow-x-auto">
                <code>{example}</code>
              </pre>
            </div>
          ))}
        </div>
      </div>

      <div className="bg-gray-50 rounded-lg p-6">
        <h2 className="text-xl font-semibold mb-3">Usage Tips</h2>
        <ul className="space-y-2 text-gray-700">
          <li>
            • Use <code className="bg-white px-2 py-1 rounded">--help</code>{" "}
            with any command to see detailed usage information
          </li>
          <li>
            • Global options{" "}
            <code className="bg-white px-2 py-1 rounded">--verbose</code> and{" "}
            <code className="bg-white px-2 py-1 rounded">--clean</code> are
            available for all commands
          </li>
          {command.options?.some(
            (opt: any) =>
              opt.flags.includes("-f") || opt.flags.includes("--file")
          ) && (
            <li>
              • If no file is specified, the tool will use your configured
              default save file
            </li>
          )}
          {command.name === "web" && (
            <li>
              • The web interface will automatically open in your default
              browser
            </li>
          )}
          {command.name === "interactive" && (
            <li>
              • Interactive mode provides a menu-driven interface for easier
              navigation
            </li>
          )}
        </ul>
      </div>

      <div className="mt-8 p-4 bg-blue-50 rounded-lg">
        <h3 className="font-semibold mb-2">Source Code</h3>
        <p className="text-sm text-gray-600">
          Implementation:{" "}
          <code className="bg-white px-2 py-1 rounded">
            {command.className}
          </code>
        </p>
      </div>
    </div>
  );
}
