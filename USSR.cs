using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace USSR.Core
{
    public class USSR
    {
        const string ASSET_CLASS_DB = "classdata.tpk";
        static void Main(string[] args)
        {
            string? ussrExec = Path.GetDirectoryName(AppContext.BaseDirectory);

            // 检查命令行参数
            if (args.Length == 0)
            {
                Console.WriteLine("用法: UDSR.exe <.exe 文件路径>");
                Environment.Exit(1);
                return;
            }

            // 命令行模式
            string filePath = args[0].Trim();
            string exeDir = Path.GetDirectoryName(filePath) ?? string.Empty;
            string dataDir = Path.Combine(exeDir, $"{Path.GetFileNameWithoutExtension(filePath)}_Data");
            string selectedFile = Path.Combine(dataDir, "globalgamemanagers");

            AssetsManager assetsManager = new();
            string? tpkFile = Path.Combine(ussrExec ?? string.Empty, ASSET_CLASS_DB);
            if (!LoadClassPackage(assetsManager, tpkFile))
            {
                Console.WriteLine("( ERR! ) Failed to load class types package! Exiting...");
                return;
            }

            List<string> temporaryFiles = new();
            string inspectedFile = selectedFile;
            AssetsFileInstance? assetFileInstance = null;
            FileStream? bundleStream = null;

            string tempFile = CloneFile(inspectedFile, $"{inspectedFile}.temp");
            temporaryFiles.Add(tempFile);
            temporaryFiles.Add($"{tempFile}.unpacked");
            assetFileInstance = LoadAssetFileInstance(tempFile, assetsManager);

            if (assetFileInstance != null)
            {
                try
                {
                    Console.WriteLine("( INFO ) Loading asset class types database...");
                    assetsManager.LoadClassDatabaseFromPackage(assetFileInstance.file.Metadata.UnityVersion);
                    Console.WriteLine($"( INFO ) Unity Version: {assetFileInstance.file.Metadata.UnityVersion}");
                    assetFileInstance.file = RemoveSplashScreen(assetsManager, assetFileInstance);
                    if (assetFileInstance.file != null)
                    {
                        BackupOnlyOnce(selectedFile);
                        WriteChanges(inspectedFile, assetFileInstance);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"( ERR! ) Error when loading asset class types database! {ex.Message}");
                }
            }

            Cleanup(
                temporaryFiles,
                bundleStream,
                assetsManager
            );
        }

        private static void Cleanup(List<string> temporaryFiles, FileStream? bundleStream, AssetsManager assetsManager)
        {
            bundleStream?.Close();
            assetsManager?.UnloadAll(true);
            CleanUp(temporaryFiles);
        }

        static bool LoadClassPackage(AssetsManager assetsManager, string tpkFile)
        {
            if (File.Exists(tpkFile))
            {
                try
                {
                    Console.WriteLine($"( INFO ) Loading class types package: {tpkFile}...");
                    assetsManager.LoadClassPackage(path: tpkFile);
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"( ERR! ) Error when loading class types package! {ex.Message}");
                }
            }
            else
                Console.WriteLine($"( ERR! ) TPK file not found: {tpkFile}...");

            return false;
        }

        static AssetsFileInstance? LoadAssetFileInstance(
            string assetFile,
            AssetsManager assetsManager
        )
        {
            AssetsFileInstance? assetFileInstance = null;

            if (File.Exists(assetFile))
            {
                try
                {
                    Console.WriteLine($"( INFO ) Loading asset file: {assetFile}...");
                    assetFileInstance = assetsManager.LoadAssetsFile(assetFile, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"( ERR! ) Error when loading asset file! {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine($"( ERR! ) Asset file not found: {assetFile}");
            }

            return assetFileInstance;
        }

        static AssetsFile? RemoveSplashScreen(
            AssetsManager assetsManager,
            AssetsFileInstance? assetFileInstance
        )
        {
            try
            {
                Console.WriteLine("( INFO ) Start removing Unity splash screen...");

                AssetsFile? assetFile = assetFileInstance?.file;

                List<AssetFileInfo>? buildSettingsInfo = assetFile?.GetAssetsOfType(
                    AssetClassID.BuildSettings
                );
                AssetTypeValueField buildSettingsBase = assetsManager.GetBaseField(
                    assetFileInstance,
                    buildSettingsInfo?[0]
                );

                List<AssetFileInfo>? playerSettingsInfo = assetFile?.GetAssetsOfType(
                    AssetClassID.PlayerSettings
                );
                AssetTypeValueField? playerSettingsBase = null;
                try
                {
                    playerSettingsBase = assetsManager.GetBaseField(
                        assetFileInstance,
                        playerSettingsInfo?[0]
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"( ERR! ) Can't get Player Settings fields! {ex.Message} It's possible that the current Unity version isn't supported yet.");
                    Console.WriteLine("( INFO ) Try updating the classdata.tpk manually from there: https://nightly.link/AssetRipper/Tpk/workflows/type_tree_tpk/master/uncompressed_file.zip and try again. If the issue still persist, try use another Unity version.");
                    return assetFile;
                }

                bool hasProVersion = buildSettingsBase["hasPROVersion"].AsBool;
                bool showUnityLogo = playerSettingsBase["m_ShowUnitySplashLogo"].AsBool;

                if (hasProVersion && !showUnityLogo)
                {
                    Console.WriteLine("( WARN ) Unity splash screen already removed!");
                    return assetFile;
                }

                AssetTypeValueField splashScreenLogos = playerSettingsBase[
                    "m_SplashScreenLogos.Array"
                ];
                int totalSplashScreen = splashScreenLogos.Count();

                Console.WriteLine($"( INFO ) There's {totalSplashScreen} splash screen detected:");

                if (totalSplashScreen <= 0)
                {
                    Console.WriteLine("( WARN ) Nothing to do.");
                    return assetFile;
                }

                for (int i = 0; i < totalSplashScreen; i++)
                {
                    AssetTypeValueField? logoPptr = splashScreenLogos.Children[i].Get(0);
                    AssetExternal logoExtInfo = assetsManager.GetExtAsset(assetFileInstance, logoPptr);
                    Console.WriteLine($"{i + 1} => {(logoExtInfo.baseField != null ? logoExtInfo.baseField["m_Name"].AsString : "UnitySplash-cube")}");
                }

                Console.WriteLine("Remove first splash screen !");

                Console.WriteLine($"( INFO ) Set hasProVersion = {!hasProVersion} | m_ShowUnitySplashLogo = {!showUnityLogo}");

                buildSettingsBase["hasPROVersion"].AsBool = !hasProVersion;
                playerSettingsBase["m_ShowUnitySplashLogo"].AsBool = !showUnityLogo;

                splashScreenLogos?.Children.RemoveAt(0);
                playerSettingsInfo?[0].SetNewData(playerSettingsBase);
                buildSettingsInfo?[0].SetNewData(buildSettingsBase);

                return assetFile;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"( ERR! ) Error when removing the splash screen! {ex.Message}");
                return null;
            }
        }

        static void WriteChanges(
            string modifiedFile,
            AssetsFileInstance? assetFileInstance
        )
        {
            try
            {
                Console.WriteLine($"( INFO ) Writing changes to {modifiedFile}...");
                using AssetsFileWriter writer = new(modifiedFile);
                assetFileInstance?.file.Write(writer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"( ERR! ) Error when writing changes! {ex.Message}");
            }
        }
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
