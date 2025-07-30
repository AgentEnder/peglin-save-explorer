using System;
using System.Globalization;
using peglin_save_explorer.Core;
using peglin_save_explorer.Data;

namespace peglin_save_explorer.Utils
{
    /// <summary>
    /// Utility class for consistent run data formatting across all displays
    /// </summary>
    public static class RunDisplayFormatter
    {
        /// <summary>
        /// Format run status consistently
        /// </summary>
        public static string FormatRunStatus(bool won)
        {
            return won ? "WIN" : "LOSS";
        }

        /// <summary>
        /// Format run status with alternative text
        /// </summary>
        public static string FormatRunStatus(bool won, string winText, string lossText)
        {
            return won ? winText : lossText;
        }

        /// <summary>
        /// Format duration consistently
        /// </summary>
        public static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalMinutes <= 0)
                return "--";
            
            if (duration.TotalHours >= 1)
                return $"{duration.TotalHours:F1}h";
            
            return $"{duration.TotalMinutes:F0}m";
        }

        /// <summary>
        /// Format duration with precision option
        /// </summary>
        public static string FormatDuration(TimeSpan duration, bool showSeconds = false)
        {
            if (duration.TotalMinutes <= 0)
                return "Unknown";

            if (showSeconds && duration.TotalMinutes < 60)
                return $"{duration.TotalMinutes:F1} minutes";
            
            return FormatDuration(duration);
        }

        /// <summary>
        /// Format health display
        /// </summary>
        public static string FormatHealth(int finalHp, int maxHp)
        {
            if (maxHp <= 0)
                return "--";
            
            return $"{finalHp}/{maxHp}";
        }

        /// <summary>
        /// Format character class name with optional truncation
        /// </summary>
        public static string FormatCharacterClass(string className, int maxLength = 0)
        {
            if (string.IsNullOrEmpty(className))
                return "Unknown";

            if (maxLength > 0 && className.Length > maxLength)
                return className.Substring(0, maxLength);
            
            return className;
        }

        /// <summary>
        /// Format damage numbers with thousands separators
        /// </summary>
        public static string FormatDamage(long damage)
        {
            return damage.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Format any number with thousands separators
        /// </summary>
        public static string FormatNumber(long number)
        {
            return number.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Format datetime consistently
        /// </summary>
        public static string FormatDateTime(DateTime timestamp, bool includeSeconds = false)
        {
            if (includeSeconds)
                return timestamp.ToString("yyyy-MM-dd HH:mm:ss");
            
            return timestamp.ToString("MM/dd HH:mm");
        }

        /// <summary>
        /// Format cruciball level
        /// </summary>
        public static string FormatCruciballLevel(int level)
        {
            return level > 0 ? $"C{level}" : "C0";
        }

        /// <summary>
        /// Format percentage
        /// </summary>
        public static string FormatPercentage(double percentage, int decimals = 1)
        {
            return percentage.ToString($"F{decimals}") + "%";
        }

        /// <summary>
        /// Format win rate from wins and total runs
        /// </summary>
        public static string FormatWinRate(int wins, int totalRuns)
        {
            if (totalRuns == 0)
                return "0.0%";
            
            var percentage = (double)wins / totalRuns * 100;
            return FormatPercentage(percentage);
        }

        /// <summary>
        /// Get display format for run summary line
        /// </summary>
        public static string FormatRunSummaryLine(RunRecord run, bool includeSeconds = false)
        {
            var status = FormatRunStatus(run.Won);
            var className = FormatCharacterClass(run.CharacterClass, 10);
            var duration = FormatDuration(run.Duration);
            var damage = FormatDamage(run.DamageDealt);
            var dateTime = FormatDateTime(run.Timestamp, includeSeconds);
            
            return $"{dateTime} | {status.PadRight(4)} | {className.PadRight(10)} | {damage.PadLeft(8)} dmg | {duration.PadLeft(3)}";
        }

        /// <summary>
        /// Get table header for run list displays
        /// </summary>
        public static string GetRunListHeader()
        {
            return "Date/Time        | Result | Class      | Damage     | Duration";
        }

        /// <summary>
        /// Get table separator for run list displays
        /// </summary>
        public static string GetRunListSeparator()
        {
            return "─────────────────┼────────┼────────────┼────────────┼─────────";
        }

        /// <summary>
        /// Format a run for table display with consistent column widths
        /// </summary>
        public static string FormatRunTableRow(RunRecord run)
        {
            var status = FormatRunStatus(run.Won);
            var className = FormatCharacterClass(run.CharacterClass, 10);
            var cruci = FormatCruciballLevel(run.CruciballLevel);
            var duration = FormatDuration(run.Duration);
            var hp = FormatHealth(run.FinalHp, run.MaxHp);
            var damage = FormatDamage(run.DamageDealt);
            
            return $"{run.Timestamp:MM/dd HH:mm:ss} | {status.PadRight(6)} | {className.PadRight(10)} | {cruci.PadRight(5)} | {damage.PadLeft(10)} | {hp.PadLeft(5)} | {duration.PadLeft(7)}";
        }

        /// <summary>
        /// Get enhanced table header for detailed run lists
        /// </summary>
        public static string GetDetailedRunListHeader()
        {
            return "Date/Time        | Result | Class      | Cruci | Damage     | HP    | Duration";
        }

        /// <summary>
        /// Get enhanced table separator for detailed run lists
        /// </summary>
        public static string GetDetailedRunListSeparator()
        {
            return "─────────────────┼────────┼────────────┼───────┼────────────┼───────┼─────────";
        }
    }
}
