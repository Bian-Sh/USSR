namespace USSR.Utilities
{
    internal class Utility
    {
        /// <summary>
        /// Clone a file.
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <param name="outputFile"></param>
        /// <returns>Cloned file path.</returns>
        internal static string CloneFile(string sourceFile, string outputFile)
        {
            try
            {
                if (!File.Exists(sourceFile))
                {
                    Console.WriteLine($"( ERROR ) Source file to duplicate doesn't exist: {sourceFile}");
                    return string.Empty;
                }

                File.Copy(sourceFile, outputFile, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                return string.Empty;
            }

            return outputFile;
        }

        /// <summary>
        /// Backup a file as ".bak". If it's already exist, skip.
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <returns></returns>
        internal static string BackupOnlyOnce(string sourceFile)
        {
            string backupFile = $"{sourceFile}.bak";

            if (!File.Exists(backupFile))
            {
                Console.WriteLine($"( INFO ) Backup {Path.GetFileName(sourceFile)} as {backupFile}...");
                CloneFile(sourceFile, backupFile);
            }

            return backupFile;
        }

        /// <summary>
        /// Delete <paramref name="paths"/>.
        /// </summary>
        /// <param name="paths"></param>
        internal static void CleanUp(List<string> paths)
        {
            if (paths != null && paths?.Count > 0)
            {
                Console.WriteLine("( INFO ) Cleaning up temporary files...");
                foreach (string path in paths)
                {
                    if (File.Exists(path))
                        File.Delete(path);

                    if (Directory.Exists(path))
                        Directory.Delete(path, true);
                }
            }
        }
    }
}
