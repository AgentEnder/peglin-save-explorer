import React from "react";

export function Footer() {
  return (
    <footer className="bg-gray-100 border-t border-gray-200 mt-16">
      <div className="container mx-auto max-w-6xl px-4 py-8">
        <div className="grid md:grid-cols-3 gap-8">
          <div>
            <h3 className="font-semibold text-gray-700 mb-3">Project</h3>
            <ul className="space-y-2">
              <li>
                <a
                  href="https://github.com/AgentEnder/peglin-save-explorer"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-gray-600 hover:text-blue-600 transition-colors"
                >
                  GitHub Repository
                </a>
              </li>
              <li>
                <a
                  href="https://github.com/AgentEnder/peglin-save-explorer/releases"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-gray-600 hover:text-blue-600 transition-colors"
                >
                  Releases
                </a>
              </li>
              <li>
                <a
                  href="https://github.com/AgentEnder/peglin-save-explorer/blob/main/LICENSE"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-gray-600 hover:text-blue-600 transition-colors"
                >
                  License
                </a>
              </li>
            </ul>
          </div>
          <div>
            <h3 className="font-semibold text-gray-700 mb-3">Support</h3>
            <ul className="space-y-2">
              <li>
                <a
                  href="https://github.com/AgentEnder/peglin-save-explorer/issues"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-gray-600 hover:text-blue-600 transition-colors"
                >
                  Report Issues
                </a>
              </li>
              <li>
                <a
                  href="https://github.com/AgentEnder/peglin-save-explorer/discussions"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-gray-600 hover:text-blue-600 transition-colors"
                >
                  Discussions
                </a>
              </li>
              <li>
                <a
                  href="https://github.com/AgentEnder/peglin-save-explorer/wiki"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-gray-600 hover:text-blue-600 transition-colors"
                >
                  Wiki
                </a>
              </li>
            </ul>
          </div>
          <div>
            <h3 className="font-semibold text-gray-700 mb-3">Related</h3>
            <ul className="space-y-2">
              <li>
                <a
                  href="https://store.steampowered.com/app/1296610/Peglin/"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-gray-600 hover:text-blue-600 transition-colors"
                >
                  Peglin on Steam
                </a>
              </li>
              <li>
                <a
                  href="https://www.rednexusgames.com/"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-gray-600 hover:text-blue-600 transition-colors"
                >
                  Red Nexus Games
                </a>
              </li>
              <li>
                <a
                  href="https://discord.gg/peglin"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-gray-600 hover:text-blue-600 transition-colors"
                >
                  Peglin Discord
                </a>
              </li>
            </ul>
          </div>
        </div>
        <div className="mt-8 pt-6 border-t border-gray-300 text-center text-gray-600 text-sm">
          <p>
            Peglin Save Explorer is an unofficial tool and is not affiliated with Red Nexus Games.
          </p>
          <p className="mt-2">
            Made with ❤️ by the community
          </p>
        </div>
      </div>
    </footer>
  );
}