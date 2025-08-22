import { execSync } from "child_process";
import { existsSync } from "fs";
import path from "path";

export interface OptionInfo {
  type: string;
  flags: string[];
  description: string;
  required: boolean;
  defaultValue?: string;
}

export interface CommandInfo {
  id: string;
  className: string;
  name: string;
  description: string;
  options: OptionInfo[];
  examples: string[];
}

export interface CommandsData {
  commands: CommandInfo[];
  commandsByName: Record<string, CommandInfo>;
  commandIds: string[];
}

export function findWorkspaceRoot(start: string): string {
  let prev = "";
  let next: string = start.replaceAll(/\\/g, "/");
  while (next != prev) {
    prev = next;
    next = path.dirname(prev);

    if (existsSync(path.join(next, "nx.json"))) {
      return next;
    }
  }
  throw new Error(`Could not find workspace root from ${start}`);
}

/**
 * Extract command data using our C# reflection-based tool
 * @returns {Promise<CommandsData>} - Command data from C# extractor
 */
export async function extractCommandData(): Promise<CommandsData> {
  try {
    // Use import.meta.url to get the current file's directory and resolve paths from there
    const currentFileUrl = import.meta.url;
    const currentFilePath = new URL(currentFileUrl).pathname;
    const repoRoot = findWorkspaceRoot(currentFilePath);

    // Navigate up to the repo root: docs-site/services -> docs-site -> repo root

    // build first, so no build output is in json stream.
    execSync("dotnet build docs-site/scripts/CommandExtractor", {
      stdio: "ignore",
      cwd: repoRoot,
    });

    const output = execSync(
      "dotnet run --no-build --project docs-site/scripts/CommandExtractor",
      {
        encoding: "utf8",
        stdio: ["pipe", "pipe", "ignore"], // Ignore stderr to avoid debug output in JSON
        cwd: repoRoot,
      }
    );

    const data = JSON.parse(output) as CommandsData;
    return data;
  } catch (error) {
    console.error(
      "Failed to extract command data:",
      error instanceof Error ? error.message : "Unknown error"
    );
    console.error((error as any).stdout.toString());
    console.error((error as any).stderr.toString());
    // Return empty data as fallback
    return {
      commands: [],
      commandsByName: {},
      commandIds: [],
    };
  }
}

/**
 * Generate command data for Vike (alias for extractCommandData)
 * @returns {Promise<CommandsData>} - Object with commands array and index by name
 */
export async function generateCommandsData(): Promise<CommandsData> {
  return await extractCommandData();
}
