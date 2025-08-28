using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Spectre.Console;
using USSR.Enums;
using USSR.Utilities;

namespace USSR.Core
{
    public class USSR
    {
        static readonly string? appVersion = Utility.GetVersion();
        const string ASSET_CLASS_DB = "classdata.tpk";

        static void Main(string[] args)
        {
            Console.Title = $"Unity Splash Screen Remover v{appVersion}";
            AnsiConsole.Background = Color.Grey11;

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
                AnsiConsole.MarkupLine(
                    "[red]( ERR! )[/] Failed to load class types package! Exiting..."
                );
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
                    AnsiConsole.MarkupLine("( INFO ) Loading asset class types database...");
                    assetsManager.LoadClassDatabaseFromPackage(assetFileInstance.file.Metadata.UnityVersion);
                    AnsiConsole.MarkupLineInterpolated($"( INFO ) Unity Version: [bold green]{assetFileInstance.file.Metadata.UnityVersion}[/]");
                    assetFileInstance.file = RemoveSplashScreen(assetsManager, assetFileInstance);
                    if (assetFileInstance.file != null)
                    {
                        Utility.BackupOnlyOnce(selectedFile);
                        WriteChanges(inspectedFile, AssetTypes.Asset, assetFileInstance, bundleFileInstance);
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLineInterpolated(
                        $"[red]( ERR! )[/] Error when loading asset class types database! {ex.Message}"
                    );
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

        /// <summary>
        /// Load Class Types package (.tpk) file.
        /// </summary>
        /// <param name="assetsManager"></param>
        /// <param name="tpkFile"></param>
        /// <returns></returns>
        static bool LoadClassPackage(AssetsManager assetsManager, string tpkFile)
        {
            if (File.Exists(tpkFile))
            {
                try
                {
                    AnsiConsole.MarkupLineInterpolated(
                        $"( INFO ) Loading class types package: [green]{tpkFile}[/]..."
                    );
                    assetsManager.LoadClassPackage(path: tpkFile);
                    return true;
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine(
                        $"[red]( ERR! )[/] Error when loading class types package! {ex.Message}"
                    );
                }
            }
            else
                AnsiConsole.MarkupLineInterpolated(
                    $"[red]( ERR! )[/] TPK file not found: [red]{tpkFile}[/]..."
                );

            return false;
        }

        /// <summary>
        /// Wrapper for LoadAssetsFile.
        /// </summary>
        /// <param name="assetFile"></param>
        /// <param name="assetsManager"></param>
        /// <returns></returns>
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
                    AnsiConsole.MarkupLineInterpolated(
                        $"( INFO ) Loading asset file: [green]{assetFile}[/]..."
                    );
                    assetFileInstance = assetsManager.LoadAssetsFile(assetFile, true);
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLineInterpolated(
                        $"[red]( ERR! )[/] Error when loading asset file! {ex.Message}"
                    );
                }
            }
            else
            {
                AnsiConsole.MarkupLineInterpolated(
                    $"[red]( ERR! )[/] Asset file not found: [red]{assetFile}[/]"
                );
            }

            return assetFileInstance;
        }

        /// <summary>
        /// Remove "Made with Unity" splash screen.
        /// </summary>
        /// <param name="assetsManager"></param>
        /// <param name="assetFileInstance"></param>
        /// <returns></returns>
        static AssetsFile? RemoveSplashScreen(
            AssetsManager assetsManager,
            AssetsFileInstance? assetFileInstance
        )
        {
            try
            {
                AnsiConsole.MarkupLine("( INFO ) Start removing Unity splash screen...");

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
                    AnsiConsole.MarkupLineInterpolated(
                        $"[red]( ERR! )[/] Can\'t get Player Settings fields! {ex.Message} It\'s possible that the current Unity version isn\'t supported yet."
                    );
                    AnsiConsole.MarkupLine(
                        "( INFO ) Try updating the [bold green]classdata.tpk[/] manually from there: [link green]https://nightly.link/AssetRipper/Tpk/workflows/type_tree_tpk/master/uncompressed_file.zip[/] and try again. If the issue still persist, try use another Unity version."
                    );
                    return assetFile;
                }

                // Required fields to remove splash screen
                bool hasProVersion = buildSettingsBase["hasPROVersion"].AsBool;
                bool showUnityLogo = playerSettingsBase["m_ShowUnitySplashLogo"].AsBool;

                // Check if the splash screen have been removed
                if (hasProVersion && !showUnityLogo)
                {
                    AnsiConsole.MarkupLine(
                        "[yellow]( WARN ) [bold]Unity splash screen already removed![/][/]"
                    );
                    return assetFile;
                }

                AssetTypeValueField splashScreenLogos = playerSettingsBase[
                    "m_SplashScreenLogos.Array"
                ];
                int totalSplashScreen = splashScreenLogos.Count();

                AnsiConsole.MarkupLineInterpolated(
                    $"( INFO ) There's [green]{totalSplashScreen}[/] splash screen detected:"
                );

                if (totalSplashScreen <= 0)
                {
                    AnsiConsole.MarkupLine("[yellow]( WARN ) Nothing to do.[/]");
                    return assetFile;
                }

                for (int i = 0; i < totalSplashScreen; i++)
                {
                    AssetTypeValueField? logoPptr = splashScreenLogos.Children[i].Get(0);
                    AssetExternal logoExtInfo = assetsManager.GetExtAsset(assetFileInstance, logoPptr);
                    AnsiConsole.MarkupLineInterpolated(
                        $"[green]{i + 1}[/] => [green]{(logoExtInfo.baseField != null ? logoExtInfo.baseField["m_Name"].AsString : "UnitySplash-cube")}[/]"
                    );
                }

                AnsiConsole.Markup("Remove first splash screen !");

                // RemoveSplashScreen:
                AnsiConsole.MarkupLineInterpolated(
                    $"( INFO ) Set [green]hasProVersion[/] = [green]{!hasProVersion}[/] | [green]m_ShowUnitySplashLogo[/] = [green]{!showUnityLogo}[/]"
                );

                buildSettingsBase["hasPROVersion"].AsBool = !hasProVersion; // true
                playerSettingsBase["m_ShowUnitySplashLogo"].AsBool = !showUnityLogo; // false

                splashScreenLogos?.Children.RemoveAt(0);
                playerSettingsInfo?[0].SetNewData(playerSettingsBase);
                buildSettingsInfo?[0].SetNewData(buildSettingsBase);

                return assetFile;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated(
                    $"[red]( ERR! )[/] Error when removing the splash screen! {ex.Message}"
                );
                return null;
            }
        }

        /// <summary>
        /// Write assets changes to disk.
        /// </summary>
        /// <param name="modifiedFile"></param>
        /// <param name="assetType"></param>
        /// <param name="assetFileInstance"></param>
        /// <param name="bundleFileInstance"></param>
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
                AnsiConsole.MarkupLineInterpolated(
                    $"( INFO ) Writing changes to [green]{modifiedFile}[/]..."
                );

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
                            // Write modified assets to uncompressed asset bundle
                            bundleFileInstance
                                ?.file.BlockAndDirInfo.DirectoryInfos[0]
                                .SetNewData(assetFileInstance?.file);
                            using (AssetsFileWriter writer = new(uncompressedBundleFile))
                                bundleFileInstance?.file.Write(writer);

                            AnsiConsole.MarkupLineInterpolated(
                                $"( INFO ) Compressing [green]{modifiedFile}[/]..."
                            );
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
                AnsiConsole.MarkupLineInterpolated(
                    $"[red]( ERR! )[/] Error when writing changes! {ex.Message}"
                );
            }
            finally
            {
                if (File.Exists(uncompressedBundleFile))
                    File.Delete(uncompressedBundleFile);
            }
        }
    }
}
