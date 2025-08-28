using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace zFramework
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
                Console.WriteLine($"[步骤] 检查路径: {globalgamemanagersPath}");
                if (string.IsNullOrWhiteSpace(globalgamemanagersPath))
                {
                    Console.WriteLine("[错误] 文件路径不能为空");
                    return (false, "文件路径不能为空");
                }

                if (!File.Exists(globalgamemanagersPath))
                {
                    Console.WriteLine($"[错误] 文件不存在: {globalgamemanagersPath}");
                    return (false, $"文件不存在: {globalgamemanagersPath}");
                }

                string fileName = Path.GetFileName(globalgamemanagersPath);
                if (!fileName.Contains("globalgamemanagers"))
                {
                    Console.WriteLine("[错误] 不支持的文件类型，仅支持 globalgamemanagers 文件");
                    return (false, "不支持的文件类型，仅支持 globalgamemanagers 文件");
                }

                string? ussrExec = Path.GetDirectoryName(AppContext.BaseDirectory);
                var tpkFile = Path.Combine(ussrExec ?? string.Empty, ASSET_CLASS_DB);
                Console.WriteLine($"[步骤] 检查类型包: {tpkFile}");
                if (!File.Exists(tpkFile))
                {
                    Console.WriteLine($"[错误] TPK 文件不存在: {tpkFile}");
                    return (false, $"TPK 文件不存在: {tpkFile}");
                }

                string backupFile = $"{globalgamemanagersPath}.bak";
                if (!File.Exists(backupFile))
                {
                    try
                    {
                        Console.WriteLine($"[步骤] 创建备份文件: {backupFile}");
                        File.Copy(globalgamemanagersPath, backupFile, false);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[错误] 创建备份文件失败: {ex.Message}");
                        return (false, $"创建备份文件失败: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"[信息] 备份文件已存在: {backupFile}");
                }

                string tempFile = $"{globalgamemanagersPath}.temp";
                temporaryFiles.Add(tempFile);
                try
                {
                    Console.WriteLine($"[步骤] 创建临时文件: {tempFile}");
                    File.Copy(globalgamemanagersPath, tempFile, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[错误] 创建临时文件失败: {ex.Message}");
                    return (false, $"创建临时文件失败: {ex.Message}");
                }

                AssetsManager assetsManager = new();
                AssetsFileInstance? assetFileInstance = null;
                try
                {
                    Console.WriteLine($"[步骤] 加载类型包: {tpkFile}");
                    assetsManager.LoadClassPackage(path: tpkFile);
                    Console.WriteLine($"[步骤] 加载资产文件: {tempFile}");
                    assetFileInstance = assetsManager.LoadAssetsFile(tempFile, true);
                    if (assetFileInstance == null)
                    {
                        Console.WriteLine("[错误] 加载资源文件失败");
                        return (false, "加载资源文件失败");
                    }
                    Console.WriteLine($"[步骤] 加载类数据库: {assetFileInstance.file.Metadata.UnityVersion}");
                    assetsManager.LoadClassDatabaseFromPackage(assetFileInstance.file.Metadata.UnityVersion);
                    var result = ProcessSplashRemoval(assetsManager, assetFileInstance);
                    if (!result.Success)
                    {
                        Console.WriteLine($"[错误] {result.Reason}");
                        return result;
                    }
                    Console.WriteLine($"[步骤] 写入更改到原文件: {globalgamemanagersPath}");
                    using (AssetsFileWriter writer = new(globalgamemanagersPath))
                    {
                        assetFileInstance.file.Write(writer);
                    }
                    Console.WriteLine($"[成功] {result.Reason}");
                    return (true, result.Reason);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[错误] 处理资源文件时出错: {ex.Message}");
                    return (false, $"处理资源文件时出错: {ex.Message}");
                }
                finally
                {
                    Console.WriteLine("[步骤] 卸载所有资源");
                    assetsManager?.UnloadAll(true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[错误] 未预期的错误: {ex.Message}");
                return (false, $"未预期的错误: {ex.Message}");
            }
            finally
            {
                foreach (string tempFile in temporaryFiles)
                {
                    try
                    {
                        if (File.Exists(tempFile))
                        {
                            Console.WriteLine($"[步骤] 删除临时文件: {tempFile}");
                            File.Delete(tempFile);
                        }
                    }
                    catch
                    {
                        Console.WriteLine($"[警告] 删除临时文件失败: {tempFile}");
                    }
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
                Console.WriteLine("[步骤] 处理 Splash Screen 移除逻辑");
                AssetsFile assetFile = assetFileInstance.file;
                List<AssetFileInfo> buildSettingsInfos = assetFile.GetAssetsOfType(AssetClassID.BuildSettings);
                if (buildSettingsInfos == null || buildSettingsInfos.Count == 0)
                {
                    Console.WriteLine("[错误] 找不到 BuildSettings 数据");
                    return (false, "找不到 BuildSettings 数据");
                }
                AssetTypeValueField buildSettingsBase = assetsManager.GetBaseField(assetFileInstance, buildSettingsInfos[0]);
                List<AssetFileInfo> playerSettingsInfos = assetFile.GetAssetsOfType(AssetClassID.PlayerSettings);
                if (playerSettingsInfos == null || playerSettingsInfos.Count == 0)
                {
                    Console.WriteLine("[错误] 找不到 PlayerSettings 数据");
                    return (false, "找不到 PlayerSettings 数据");
                }
                AssetTypeValueField playerSettingsBase;
                try
                {
                    playerSettingsBase = assetsManager.GetBaseField(assetFileInstance, playerSettingsInfos[0]);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[错误] 无法获取 PlayerSettings 字段: {ex.Message}");
                    return (false, $"无法获取 PlayerSettings 字段: {ex.Message}。可能不支持当前的 Unity 版本");
                }
                bool hasProVersion = buildSettingsBase["hasPROVersion"].AsBool;
                bool showUnityLogo = playerSettingsBase["m_ShowUnitySplashLogo"].AsBool;
                Console.WriteLine($"[信息] hasProVersion: {hasProVersion}, m_ShowUnitySplashLogo: {showUnityLogo}");
                if (hasProVersion && !showUnityLogo)
                {
                    Console.WriteLine("[信息] Unity 启动画面已经被移除过了");
                    return (true, "Unity 启动画面已经被移除过了");
                }
                AssetTypeValueField splashScreenLogos = playerSettingsBase["m_SplashScreenLogos.Array"];
                int totalSplashScreens = splashScreenLogos.Count();
                Console.WriteLine($"[信息] 检测到 {totalSplashScreens} 个 Splash Screen");
                if (totalSplashScreens > 0)
                {
                    Console.WriteLine("[步骤] 移除第一个 Splash Screen");
                    splashScreenLogos.Children.RemoveAt(0);
                }
                buildSettingsBase["hasPROVersion"].AsBool = true;
                playerSettingsBase["m_ShowUnitySplashLogo"].AsBool = false;
                playerSettingsInfos[0].SetNewData(playerSettingsBase);
                buildSettingsInfos[0].SetNewData(buildSettingsBase);
                string message = totalSplashScreens > 0
                    ? $"成功移除首个（共 {totalSplashScreens} 个）Splash Screen 并设置为 Pro 版本"
                    : "已设置为 Pro 版本并隐藏 Unity Logo";
                Console.WriteLine($"[成功] {message}");
                return (true, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[错误] 移除 Splash Screen 时发生错误: {ex.Message}");
                return (false, $"移除 Splash Screen 时发生错误: {ex.Message}");
            }
        }
    }
}
