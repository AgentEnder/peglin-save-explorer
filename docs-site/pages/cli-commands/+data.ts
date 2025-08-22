import { generateCommandsData } from '../../services/command-extractor.js';

export async function data() {
  const { commands } = await generateCommandsData();
  
  return {
    commands,
    // Also provide commands for the layout
    layoutProps: {
      commands: commands.map(cmd => ({ 
        name: cmd.name, 
        description: cmd.description 
      }))
    }
  };
}