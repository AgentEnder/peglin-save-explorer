#!/usr/bin/env tsx
import { spawn, SpawnOptions } from "child_process";
import * as fs from "node:fs";
import * as path from "path";
import * as os from "os";
import archiver from "archiver";

interface Platform {
  rid: string;
  name: string;
  archiveExt: string;
  executable: string;
  scriptExt: string;
  pathSeparator: string;
}

const platforms: Platform[] = [
  {
    rid: "win-x64",
    name: "Windows x64",
    archiveExt: "zip",
    executable: "peglin-save-explorer.exe",
    scriptExt: "bat",
    pathSeparator: "\\",
  },
  {
    rid: "linux-x64",
    name: "Linux x64",
    archiveExt: "tar.gz",
    executable: "peglin-save-explorer",
    scriptExt: "sh",
    pathSeparator: "/",
  },
  {
    rid: "osx-x64",
    name: "macOS Intel",
    archiveExt: "tar.gz",
    executable: "peglin-save-explorer",
    scriptExt: "sh",
    pathSeparator: "/",
  },
  {
    rid: "osx-arm64",
    name: "macOS Apple Silicon",
    archiveExt: "tar.gz",
    executable: "peglin-save-explorer",
    scriptExt: "sh",
    pathSeparator: "/",
  },
];

class BuildScript {
  private readonly outputDir = "dist";
  private readonly projectDir = "peglin-save-explorer";
  private readonly webDir = "web-frontend";
  private readonly version: string;

  constructor(version: string = "dev") {
    this.version = version;
  }

  private async runCommand(
    command: string,
    args: string[],
    options: SpawnOptions = {}
  ): Promise<void> {
    return new Promise((resolve, reject) => {
      console.log(`📦 Running: ${command} ${args.join(" ")}`);

      const child = spawn(command, args, {
        stdio: "inherit",
        shell: true,
        ...options,
      });

      child.on("close", (code) => {
        if (code === 0) {
          resolve();
        } else {
          reject(new Error(`Command failed with exit code ${code}`));
        }
      });

      child.on("error", (error) => {
        reject(error);
      });
    });
  }

  private cleanOutput() {
    console.log("🧹 Cleaning previous builds...");
    fs.rmSync(this.outputDir, { recursive: true });
    fs.mkdirSync(this.outputDir);
  }

  private async buildFrontend(): Promise<void> {
    console.log("🌐 Building web frontend...");

    // Install dependencies
    await this.runCommand("npm", ["install"], { cwd: this.webDir });

    // Build frontend
    await this.runCommand("npm", ["run", "build"], { cwd: this.webDir });
  }

  private async buildDotNet(platform: Platform): Promise<void> {
    console.log(`🔨 Building .NET application for ${platform.name}...`);

    const outputPath = path.join("..", this.outputDir, platform.rid);

    await this.runCommand(
      "dotnet",
      [
        "publish",
        "--configuration",
        "Release",
        "--runtime",
        platform.rid,
        "--self-contained",
        "true",
        "--output",
        outputPath,
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:PublishTrimmed=false",
        "-p:EnableCompressionInSingleFile=true",
        "-p:PeglinDllPath=",
      ],
      { cwd: this.projectDir }
    );
  }

  private async copyWebAssets(platform: Platform): Promise<void> {
    console.log(`   📁 Copying web assets for ${platform.name}...`);

    const platformDir = path.join(this.outputDir, platform.rid);
    const wwwrootDir = path.join(platformDir, "wwwroot");
    const frontendDistDir = path.join(this.webDir, "dist");

    fs.mkdirSync(wwwrootDir, { recursive: true });
    fs.cpSync(frontendDistDir, wwwrootDir, { recursive: true });
  }

  private createWindowsScripts(platformDir: string): void {
    const scriptsDir = path.join(platformDir, "scripts");
    fs.mkdirSync(scriptsDir, { recursive: true });

    // open-web.bat
    const openWebScript = `@echo off
echo Starting Peglin Save Explorer Web Interface...
cd /d "%~dp0\\.."
start "" "peglin-save-explorer.exe" web --open`;
    fs.writeFileSync(path.join(scriptsDir, "open-web.bat"), openWebScript);

    // install-to-path.bat
    const installScript = `@echo off
echo Installing Peglin Save Explorer to PATH...
set "TARGET_DIR=%USERPROFILE%\\.peglin-save-explorer"
if not exist "%TARGET_DIR%" mkdir "%TARGET_DIR%"
copy "%~dp0\\..\\peglin-save-explorer.exe" "%TARGET_DIR%\\"

:: Add to user PATH
for /f "tokens=2*" %%A in ('reg query HKCU\\Environment /v PATH 2^>nul') do set "USER_PATH=%%B"
if not defined USER_PATH set "USER_PATH="
echo %USER_PATH% | find /i "%TARGET_DIR%" >nul
if errorlevel 1 (
    reg add HKCU\\Environment /v PATH /t REG_EXPAND_SZ /d "%USER_PATH%;%TARGET_DIR%" /f
    echo Added %TARGET_DIR% to PATH
    echo Please restart your command prompt or terminal
) else (
    echo %TARGET_DIR% is already in PATH
)
pause`;
    fs.writeFileSync(
      path.join(scriptsDir, "install-to-path.bat"),
      installScript
    );
  }

  private createUnixScripts(platformDir: string): void {
    const scriptsDir = path.join(platformDir, "scripts");
    fs.mkdirSync(scriptsDir, { recursive: true });

    // open-web.sh
    const openWebScript = `#!/bin/bash
echo "Starting Peglin Save Explorer Web Interface..."
cd "$(dirname "$0")/.."
./peglin-save-explorer web --open`;
    const openWebPath = path.join(scriptsDir, "open-web.sh");
    fs.writeFileSync(openWebPath, openWebScript);
    fs.chmodSync(openWebPath, "755");

    // install-to-path.sh
    const installScript = `#!/bin/bash
echo "Installing Peglin Save Explorer to PATH..."

TARGET_DIR="$HOME/.local/bin"
BINARY_NAME="peglin-save-explorer"
SOURCE_BINARY="$(dirname "$0")/../peglin-save-explorer"

# Create target directory if it doesn't exist
mkdir -p "$TARGET_DIR"

# Copy binary
cp "$SOURCE_BINARY" "$TARGET_DIR/$BINARY_NAME"
chmod +x "$TARGET_DIR/$BINARY_NAME"

echo "Installed $BINARY_NAME to $TARGET_DIR"

# Check if directory is in PATH
if [[ ":$PATH:" != *":$TARGET_DIR:"* ]]; then
    echo "Adding $TARGET_DIR to PATH..."
    
    # Determine shell configuration file
    if [[ "$SHELL" == *"zsh"* ]]; then
        SHELL_RC="$HOME/.zshrc"
    elif [[ "$SHELL" == *"bash"* ]]; then
        SHELL_RC="$HOME/.bashrc"
    else
        SHELL_RC="$HOME/.profile"
    fi
    
    echo "export PATH=\\"\\$PATH:$TARGET_DIR\\"" >> "$SHELL_RC"
    echo "Added $TARGET_DIR to PATH in $SHELL_RC"
    echo "Please restart your terminal or run: source $SHELL_RC"
else
    echo "$TARGET_DIR is already in PATH"
fi`;
    const installPath = path.join(scriptsDir, "install-to-path.sh");
    fs.writeFileSync(installPath, installScript);
    fs.chmodSync(installPath, "755");
  }

  private createReadme(platformDir: string, platform: Platform): void {
    const readme = `Peglin Save Explorer - Standalone Distribution
=============================================

Version: ${this.version}
Platform: ${platform.name} (${platform.rid})
Built: ${new Date().toISOString()}

This package contains the Peglin Save Explorer CLI tool and web interface.

QUICK START:
-----------
1. Extract this archive to a folder of your choice
2. Run the executable directly: ./${platform.executable}
3. For web interface: ./scripts/open-web.${platform.scriptExt}

INSTALLATION TO PATH:
--------------------
To use 'peglin-save-explorer' from anywhere in your terminal:
- Linux/Mac: Run ./scripts/install-to-path.sh
- Windows: Run scripts/install-to-path.bat as Administrator

USAGE:
------
CLI Commands:
  peglin-save-explorer --help                    # Show all commands
  peglin-save-explorer analyze <save-file>       # Analyze save file
  peglin-save-explorer extract <peglin-path>     # Extract game data
  peglin-save-explorer web                       # Start web interface
  peglin-save-explorer web --open                # Start web interface and open browser

Web Interface:
  Access at http://localhost:5000 when running 'web' command
  Use scripts/open-web to automatically start and open in browser

REQUIREMENTS:
------------
- No additional dependencies required (self-contained)
- For game data extraction: Peglin installation required

SUPPORT:
--------
GitHub: https://github.com/AgentEnder/peglin-save-explorer
Issues: https://github.com/AgentEnder/peglin-save-explorer/issues`;

    fs.writeFileSync(path.join(platformDir, "README.txt"), readme);
  }

  private createVersionFiles(platformDir: string): void {
    fs.writeFileSync(path.join(platformDir, "VERSION"), this.version);

    const buildInfo = `Built: ${new Date().toISOString()}
Platform: ${path.basename(platformDir)}
Node Version: ${process.version}
OS: ${os.platform()} ${os.arch()}`;

    fs.writeFileSync(path.join(platformDir, "BUILD_INFO"), buildInfo);
  }

  private async createArchive(platform: Platform): Promise<void> {
    const platformDir = path.join(this.outputDir, platform.rid);
    const archiveName = `peglin-save-explorer-${platform.rid}.${platform.archiveExt}`;
    const archivePath = path.join(this.outputDir, archiveName);

    console.log(`📦 Creating archive: ${archiveName}...`);

    if (platform.archiveExt === "zip") {
      // Create ZIP archive
      const archive = archiver("zip", { zlib: { level: 9 } });
      const output = fs.createWriteStream(archivePath);

      return new Promise((resolve, reject) => {
        output.on("close", () => {
          console.log(
            `   ✅ Created ${archiveName} (${archive.pointer()} bytes)`
          );
          resolve();
        });

        archive.on("error", reject);
        archive.pipe(output);
        archive.directory(platformDir, platform.rid);
        archive.finalize();
      });
    } else {
      // Create tar.gz archive
      const archive = archiver("tar", {
        gzip: true,
        gzipOptions: { level: 9 },
      });
      const output = fs.createWriteStream(archivePath);

      return new Promise((resolve, reject) => {
        output.on("close", () => {
          console.log(
            `   ✅ Created ${archiveName} (${archive.pointer()} bytes)`
          );
          resolve();
        });

        archive.on("error", reject);
        archive.pipe(output);
        archive.directory(platformDir, platform.rid);
        archive.finalize();
      });
    }
  }

  private async buildPlatform(platform: Platform): Promise<void> {
    console.log(`\n🚀 Building ${platform.name}...`);

    // Build .NET application
    await this.buildDotNet(platform);

    const platformDir = path.join(this.outputDir, platform.rid);

    // Copy web assets
    await this.copyWebAssets(platform);

    // Create scripts
    if (platform.rid === "win-x64") {
      this.createWindowsScripts(platformDir);
    } else {
      this.createUnixScripts(platformDir);
    }

    // Create documentation
    this.createReadme(platformDir, platform);
    this.createVersionFiles(platformDir);

    // Create archive
    await this.createArchive(platform);

    console.log(`✅ Completed ${platform.name}`);
  }

  private async printSummary(): Promise<void> {
    console.log("\n🎉 Build completed! Archives created in dist/\n");
    console.log("📦 Available builds:");

    for (const platform of platforms) {
      const platformDir = path.join(this.outputDir, platform.rid);
      const archiveName = `peglin-save-explorer-${platform.rid}.${platform.archiveExt}`;
      const archivePath = path.join(this.outputDir, archiveName);

      if (fs.existsSync(platformDir) && fs.existsSync(archivePath)) {
        const stats = fs.statSync(platformDir);
        const archiveStats = fs.statSync(archivePath);
        const sizeInMB = (archiveStats.size / 1024 / 1024).toFixed(1);
        console.log(`  - ${platform.name}: ${archiveName} (${sizeInMB} MB)`);
      }
    }

    console.log("\n💡 Next steps:");
    console.log("  1. Test the archives on their target platforms");
    console.log("  2. Upload to GitHub releases or distribute as needed");
    console.log("  3. Update documentation with download links");
  }

  async run(): Promise<void> {
    try {
      console.log("🎮 Building Peglin Save Explorer for all platforms...");
      console.log(`📋 Version: ${this.version}\n`);

      // Clean and prepare
      await this.cleanOutput();

      // Build frontend once
      await this.buildFrontend();

      // Build for each platform
      for (const platform of platforms) {
        await this.buildPlatform(platform);
      }

      // Print summary
      await this.printSummary();
    } catch (error) {
      console.error("❌ Build failed:", error);
      process.exit(1);
    }
  }
}

// Main execution
async function main() {
  const version = process.argv[2] || "dev";
  const builder = new BuildScript(version);
  await builder.run();
}

if (import.meta.url === `file://${process.argv[1]}`) {
  main().catch(console.error);
}

export default BuildScript;
