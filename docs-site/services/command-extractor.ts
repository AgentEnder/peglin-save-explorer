import { execSync } from 'child_process';

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

/**
 * Extract command data using our C# reflection-based tool
 * @returns {Promise<CommandsData>} - Command data from C# extractor
 */
export async function extractCommandData(): Promise<CommandsData> {
  try {
    // Use import.meta.url to get the current file's directory and resolve paths from there
    const currentFileUrl = import.meta.url;
    const currentFilePath = new URL(currentFileUrl).pathname;
    const currentDir = currentFilePath.substring(0, currentFilePath.lastIndexOf('/'));
    
    // Navigate up to the repo root: docs-site/services -> docs-site -> repo root
    const docsDir = currentDir.substring(0, currentDir.lastIndexOf('/'));
    const repoRoot = docsDir.substring(0, docsDir.lastIndexOf('/'));
    
    const output = execSync('dotnet run --project docs-site/scripts/CommandExtractor', {
      encoding: 'utf8',
      stdio: ['pipe', 'pipe', 'ignore'], // Ignore stderr to avoid debug output in JSON
      cwd: repoRoot
    });
    
    const data = JSON.parse(output) as CommandsData;
    console.log(`Extracted ${data.commands.length} commands using C# reflection`);
    return data;
  } catch (error) {
    console.error('Failed to extract command data:', error instanceof Error ? error.message : 'Unknown error');
    // Return empty data as fallback
    return {
      commands: [],
      commandsByName: {},
      commandIds: []
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