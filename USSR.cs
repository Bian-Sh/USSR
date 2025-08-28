using AssetsTools.NET;
using AssetsTools.NET.Extra;
using USSR.Enums;
using USSR.Utilities;

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
            BundleFileInstance? bundleFileInstance = null;
            FileStream? bundleStream = null;

            string tempFile = Utility.CloneFile(inspectedFile, $"{inspectedFile}.temp");
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
                        Utility.BackupOnlyOnce(selectedFile);
                        WriteChanges(inspectedFile, AssetTypes.Asset, assetFileInstance, bundleFileInstance);
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
            Utility.CleanUp(temporaryFiles);
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
            AssetTypes assetType,
            AssetsFileInstance? assetFileInstance,
            BundleFileInstance? bundleFileInstance
        )
        {
            string uncompressedBundleFile = $"{modifiedFile}.uncompressed";

            try
            {
                Console.WriteLine($"( INFO ) Writing changes to {modifiedFile}...");

                switch (assetType)
                {
                    case AssetTypes.Asset:
                        {
                            using AssetsFileWriter writer = new(modifiedFile);
                            assetFileInstance?.file.Write(writer);
                            break;
                        }
                    case AssetTypes.Bundle:
                        {
                            bundleFileInstance
                                ?.file.BlockAndDirInfo.DirectoryInfos[0]
                                .SetNewData(assetFileInstance?.file);
                            using (AssetsFileWriter writer = new(uncompressedBundleFile))
                                bundleFileInstance?.file.Write(writer);

                            Console.WriteLine($"( INFO ) Compressing {modifiedFile}...");
                            using FileStream uncompressedBundleStream = File.OpenRead(
                                uncompressedBundleFile
                            );
                            AssetBundleFile uncompressedBundle = new();
                            uncompressedBundle.Read(new AssetsFileReader(uncompressedBundleStream));

                            using AssetsFileWriter uncompressedWriter = new(modifiedFile);
                            uncompressedBundle.Pack(uncompressedWriter, AssetBundleCompressionType.LZ4);
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"( ERR! ) Error when writing changes! {ex.Message}");
            }
            finally
            {
                if (File.Exists(uncompressedBundleFile))
                    File.Delete(uncompressedBundleFile);
            }
        }
    }
}
