import type { PageContext } from 'vike/types';
import { generateCommandsData } from '../../../services/command-extractor.js';

export async function data(pageContext: PageContext) {
  const { commandsByName, commands } = await generateCommandsData();
  const commandName = pageContext.routeParams.command;
  
  const command = commandsByName[commandName];
  
  if (!command) {
    throw new Error(`Command '${commandName}' not found`);
  }
  
  return {
    command,
    // Also provide commands for the layout
    layoutProps: {
      commands: commands.map(cmd => ({ 
        name: cmd.name, 
        description: cmd.description 
      }))
    }
  };
}