using System;
using System.IO;

namespace peglin_save_explorer.Utils
{
    public static class PeglinPathHelper
    {
        /// <summary>
        /// Gets the platform-specific path to the Assembly-CSharp.dll file
        /// </summary>
        /// <param name="peglinInstallPath">The base Peglin installation path</param>
        /// <returns>The full path to Assembly-CSharp.dll, or null if not found</returns>
        public static string? GetAssemblyPath(string peglinInstallPath)
        {
            if (string.IsNullOrEmpty(peglinInstallPath) || !Directory.Exists(peglinInstallPath))
                return null;

            // Try Windows/Linux path structure
            var windowsPath = Path.Combine(peglinInstallPath, "Peglin_Data", "Managed", "Assembly-CSharp.dll");
            if (File.Exists(windowsPath))
                return windowsPath;

            // Try macOS path structure (when pointing to Resources folder)
            var macPath = Path.Combine(peglinInstallPath, "Data", "Managed", "Assembly-CSharp.dll");
            if (File.Exists(macPath))
                return macPath;

            return null;
        }

        /// <summary>
        /// Validates if a path contains a valid Peglin installation
        /// </summary>
        public static bool IsValidPeglinPath(string path)
        {
            return !string.IsNullOrEmpty(GetAssemblyPath(path));
        }

        /// <summary>
        /// Gets the platform-specific data folder name
        /// </summary>
        public static string GetDataFolderName()
        {
            // On macOS when pointing to Resources folder, it's just "Data"
            // On Windows/Linux, it's "Peglin_Data"
            return OperatingSystem.IsMacOS() ? "Data" : "Peglin_Data";
        }

        /// <summary>
        /// Normalizes a Peglin installation path to ensure consistency
        /// </summary>
        public static string? NormalizePeglinPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            // If on macOS and path ends with peglin.app, append the resources path
            if (OperatingSystem.IsMacOS() && path.EndsWith("peglin.app", StringComparison.OrdinalIgnoreCase))
            {
                var resourcesPath = Path.Combine(path, "Contents", "Resources");
                if (Directory.Exists(resourcesPath))
                    return resourcesPath;
            }

            return path;
        }

        /// <summary>
        /// Gets the platform-specific streaming assets bundle directory
        /// </summary>
        /// <param name="peglinInstallPath">The base Peglin installation path</param>
        /// <returns>The path to the platform-specific bundle directory</returns>
        public static string? GetStreamingAssetsBundlePath(string peglinInstallPath)
        {
            if (string.IsNullOrEmpty(peglinInstallPath))
                return null;

            var dataFolderName = GetDataFolderName();
            var basePath = Path.Combine(peglinInstallPath, dataFolderName, "StreamingAssets", "aa");

            // Try platform-specific directories
            var platformDirectories = GetPlatformDirectoryNames();
            
            foreach (var platformDir in platformDirectories)
            {
                var fullPath = Path.Combine(basePath, platformDir);
                if (Directory.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the platform-specific directory names for streaming assets, in order of preference
        /// </summary>
        private static string[] GetPlatformDirectoryNames()
        {
            if (OperatingSystem.IsWindows())
            {
                return new[] { "StandaloneWindows64", "StandaloneWindows" };
            }
            else if (OperatingSystem.IsMacOS())
            {
                return new[] { "StandaloneOSX", "StandaloneOSXIntel64", "StandaloneOSXUniversal" };
            }
            else if (OperatingSystem.IsLinux())
            {
                return new[] { "StandaloneLinux64", "StandaloneLinux" };
            }
            else
            {
                // Fallback: try common directory names
                return new[] { "StandaloneWindows64", "StandaloneOSX", "StandaloneLinux64" };
            }
        }
    }
}