using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Realis
{
    public class UDSR
    {
        const string ASSET_CLASS_DB = "classdata.tpk";
        static void Main(string[] args)
        {
            int exitCode = 1;
            string reason = string.Empty;
            if (args.Length == 0)
            {
                reason = "用法: UDSR.exe <.exe 文件路径>";
                Console.WriteLine(reason);
                Environment.Exit(exitCode);
                return;
            }

            string filePath = args[0].Trim();
            string exeDir = Path.GetDirectoryName(filePath) ?? string.Empty;
            string dataDir = Path.Combine(exeDir, $"{Path.GetFileNameWithoutExtension(filePath)}_Data");
            string selectedFile = Path.Combine(dataDir, "globalgamemanagers");

            var (Success, Reason) = RemoveUnitySplash(selectedFile);
            Console.WriteLine(Reason);
            exitCode = Success ? 0 : 1;
            Environment.Exit(exitCode);
        }

        /// <summary>
        /// 移除 Unity Splash Screen (仅支持 Windows globalgamemanagers 文件)
        /// </summary>
        /// <param name="globalgamemanagersPath">globalgamemanagers 文件路径</param>
        /// <returns>操作结果和原因</returns>
        public static (bool Success, string Reason) RemoveUnitySplash(string globalgamemanagersPath)
        {
            List<string> temporaryFiles = new();
            try
            {
                if (string.IsNullOrWhiteSpace(globalgamemanagersPath))
                    return (false, "文件路径不能为空");

                if (!File.Exists(globalgamemanagersPath))
                    return (false, $"文件不存在: {globalgamemanagersPath}");

                string fileName = Path.GetFileName(globalgamemanagersPath);
                if (!fileName.Contains("globalgamemanagers"))
                    return (false, "不支持的文件类型，仅支持 globalgamemanagers 文件");

                string? ussrExec = Path.GetDirectoryName(AppContext.BaseDirectory);
                var tpkFile = Path.Combine(ussrExec ?? string.Empty, ASSET_CLASS_DB);
                if (!File.Exists(tpkFile))
                {
                    return (false, $"TPK 文件不存在: {tpkFile}");
                }

                string backupFile = $"{globalgamemanagersPath}.bak";
                if (!File.Exists(backupFile))
                {
                    try
                    {
                        File.Copy(globalgamemanagersPath, backupFile, false);
                    }
                    catch (Exception ex)
                    {
                        return (false, $"创建备份文件失败: {ex.Message}");
                    }
                }

                string tempFile = $"{globalgamemanagersPath}.temp";
                temporaryFiles.Add(tempFile);
                try
                {
                    File.Copy(globalgamemanagersPath, tempFile, true);
                }
                catch (Exception ex)
                {
                    return (false, $"创建临时文件失败: {ex.Message}");
                }

                AssetsManager assetsManager = new();
                AssetsFileInstance? assetFileInstance = null;
                try
                {
                    assetsManager.LoadClassPackage(path: tpkFile);
                    assetFileInstance = assetsManager.LoadAssetsFile(tempFile, true);
                    if (assetFileInstance == null)
                        return (false, "加载资源文件失败");
                    assetsManager.LoadClassDatabaseFromPackage(assetFileInstance.file.Metadata.UnityVersion);
                    var result = ProcessSplashRemoval(assetsManager, assetFileInstance);
                    if (!result.Success)
                        return result;
                    using (AssetsFileWriter writer = new(globalgamemanagersPath))
                    {
                        assetFileInstance.file.Write(writer);
                    }
                    return (true, result.Reason);
                }
                catch (Exception ex)
                {
                    return (false, $"处理资源文件时出错: {ex.Message}");
                }
                finally
                {
                    assetsManager?.UnloadAll(true);
                }
            }
            catch (Exception ex)
            {
                return (false, $"未预期的错误: {ex.Message}");
            }
            finally
            {
                foreach (string tempFile in temporaryFiles)
                {
                    try
                    {
                        if (File.Exists(tempFile))
                            File.Delete(tempFile);
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// 核心的 Splash Screen 移除逻辑
        /// </summary>
        private static (bool Success, string Reason) ProcessSplashRemoval(AssetsManager assetsManager, AssetsFileInstance assetFileInstance)
        {
            try
            {
                AssetsFile assetFile = assetFileInstance.file;
                List<AssetFileInfo> buildSettingsInfos = assetFile.GetAssetsOfType(AssetClassID.BuildSettings);
                if (buildSettingsInfos == null || buildSettingsInfos.Count == 0)
                    return (false, "找不到 BuildSettings 数据");
                AssetTypeValueField buildSettingsBase = assetsManager.GetBaseField(assetFileInstance, buildSettingsInfos[0]);
                List<AssetFileInfo> playerSettingsInfos = assetFile.GetAssetsOfType(AssetClassID.PlayerSettings);
                if (playerSettingsInfos == null || playerSettingsInfos.Count == 0)
                    return (false, "找不到 PlayerSettings 数据");
                AssetTypeValueField playerSettingsBase;
                try
                {
                    playerSettingsBase = assetsManager.GetBaseField(assetFileInstance, playerSettingsInfos[0]);
                }
                catch (Exception ex)
                {
                    return (false, $"无法获取 PlayerSettings 字段: {ex.Message}。可能不支持当前的 Unity 版本");
                }
                bool hasProVersion = buildSettingsBase["hasPROVersion"].AsBool;
                bool showUnityLogo = playerSettingsBase["m_ShowUnitySplashLogo"].AsBool;
                if (hasProVersion && !showUnityLogo)
                    return (true, "Unity 启动画面已经被移除过了");
                AssetTypeValueField splashScreenLogos = playerSettingsBase["m_SplashScreenLogos.Array"];
                int totalSplashScreens = splashScreenLogos.Count();
                if (totalSplashScreens > 0)
                {
                    splashScreenLogos.Children.RemoveAt(0);
                }
                buildSettingsBase["hasPROVersion"].AsBool = true;
                playerSettingsBase["m_ShowUnitySplashLogo"].AsBool = false;
                playerSettingsInfos[0].SetNewData(playerSettingsBase);
                buildSettingsInfos[0].SetNewData(buildSettingsBase);
                string message = totalSplashScreens > 0
                    ? $"成功移除首个（共 {totalSplashScreens} 个）Splash Screen 并设置为 Pro 版本"
                    : "已设置为 Pro 版本并隐藏 Unity Logo";
                return (true, message);
            }
            catch (Exception ex)
            {
                return (false, $"移除 Splash Screen 时发生错误: {ex.Message}");
            }
        }
    }
}
