using System;
using System.Collections.Generic;
using System.IO;

class Program
{
    private static string _logFilePath;

    static int Main(string[] args)
    {
        // 1. Initialize log directory and daily log file path
        string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        string logDirectory = Path.Combine(appDirectory, "Logs");
        Directory.CreateDirectory(logDirectory);

        _logFilePath = Path.Combine(logDirectory, $"EFM_Log_{DateTime.Now:yyyyMMdd}.txt");

        Log("================================================================================");
        Log($"[PROCESS STARTED] EFM File Mover initialized");
        Log($"Log location: {_logFilePath}");

        // 2. Validate input parameters
        if (args.Length < 2)
        {
            Log("[ERROR] Insufficient arguments provided.");
            Log("Usage: EFM.exe \"<SourcePath>\" \"<DestinationPath>\" [optional_older_than_days]");
            Log("[PROCESS FAILED] Terminating with exit code 1.");
            return 1;
        }

        string sourceDir = Path.GetFullPath(args[0].Trim('"'));
        string targetDir = args[1].Trim('"');

        int olderThanDays = 0;
        if (args.Length >= 3 && int.TryParse(args[2], out int parsedDays))
        {
            olderThanDays = parsedDays;
        }

        Log($"Source Directory:      {sourceDir}");
        Log($"Destination Directory: {targetDir}");
        Log($"Age Filter (Days):     {(olderThanDays > 0 ? olderThanDays.ToString() : "None (All files)")}");

        if (!Directory.Exists(sourceDir))
        {
            Log($"[FATAL] Source directory does not exist: {sourceDir}");
            Log("[PROCESS FAILED] Terminating with exit code 1.");
            return 1;
        }

        // Metrics counters
        int totalMatched = 0;
        int movedCount = 0;
        int skippedAgeCount = 0;
        int errorCount = 0;

        try
        {
            Directory.CreateDirectory(targetDir);

            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".txt", ".pdf", ".xml"
            };

            foreach (string filePath in Directory.EnumerateFiles(sourceDir, "*.*", SearchOption.TopDirectoryOnly))
            {
                string extension = Path.GetExtension(filePath);
                if (!allowedExtensions.Contains(extension))
                {
                    continue; // Ignore non-matching file types without bloating logs
                }

                totalMatched++;
                string fileName = Path.GetFileName(filePath);
                string destinationFilePath = Path.Combine(targetDir, fileName);

                // Check age filter
                if (olderThanDays > 0)
                {
                    DateTime lastModified = File.GetLastWriteTime(filePath);
                    if (lastModified > DateTime.Now.AddDays(-olderThanDays))
                    {
                        skippedAgeCount++;
                        Log($"[SKIPPED] '{fileName}' (Modified: {lastModified:yyyy-MM-dd HH:mm:ss} - newer than {olderThanDays} days)");
                        continue;
                    }
                }

                try
                {
                    long fileSizeBytes = new FileInfo(filePath).Length;

                    // Move file and overwrite destination if it already exists
                    File.Move(filePath, destinationFilePath, overwrite: true);
                    movedCount++;

                    Log($"[MOVED] '{fileName}' ({fileSizeBytes / 1024.0:F2} KB) -> '{destinationFilePath}'");
                }
                catch (IOException ex)
                {
                    errorCount++;
                    Log($"[IO ERROR] Could not move '{fileName}': {ex.Message}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    errorCount++;
                    Log($"[ACCESS DENIED] Permission failure on '{fileName}': {ex.Message}");
                }
                catch (Exception ex)
                {
                    errorCount++;
                    Log($"[ERROR] Unexpected error moving '{fileName}': {ex.Message}");
                }
            }

            // Summary metrics
            Log("--------------------------------------------------------------------------------");
            Log("[EXECUTION SUMMARY]");
            Log($"  Total Eligible Files Scanned : {totalMatched}");
            Log($"  Successfully Moved           : {movedCount}");
            Log($"  Skipped (Age Filter)         : {skippedAgeCount}");
            Log($"  Failed / Errored             : {errorCount}");
            Log($"[PROCESS COMPLETED] Status: {(errorCount > 0 ? "Completed with errors" : "Success")}");
            Log("================================================================================\n");

            return errorCount > 0 ? 2 : 0;
        }
        catch (Exception ex)
        {
            Log($"[FATAL CRASH] {ex.Message}");
            Log($"StackTrace: {ex.StackTrace}");
            Log("[PROCESS TERMINATED] Exit code 1.");
            return 1;
        }
    }

    /// <summary>
    /// Writes output simultaneously to the Console and appends to the log file with timestamps.
    /// </summary>
    private static void Log(string message)
    {
        string formattedEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";

        Console.WriteLine(formattedEntry);

        try
        {
            File.AppendAllText(_logFilePath, formattedEntry + Environment.NewLine);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LOGGER WARNING] Failed writing to log file: {ex.Message}");
        }
    }
}