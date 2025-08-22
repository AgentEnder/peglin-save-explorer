import { generateCommandsData } from "../../../services/command-extractor.js";

export async function onBeforePrerenderStart() {
  const { commandIds } = await generateCommandsData();

  // Generate URLs for all commands
  const urls = commandIds.map((commandName) => `/cli-commands/${commandName}`);

  console.log(`Generating ${urls.length} command pages:`, urls);

  return urls;
}
