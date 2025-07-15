using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;

public enum OperatingMode
{
    EditorSimulateMode,     // 编辑器模拟模式
    OfflinePlayMode,        // 离线播放模式
    HostPlayMode,           // 联机运行模式
    WebPlayMode             // Web运行模式
}

/// <summary>
/// YooAssets初始化与资源更新
/// 负责完成资源包的初始化、版本更新、资源下载等流程
/// 不继承MonoBehaviour，需由外部MonoBehaviour驱动协程调用
/// </summary>
public class YooAssetsInit
{
    private ResourcePackage package;

    private string packageName = "DefaultPackage";

    /// <summary> 当前操作模式 </summary>
    private OperatingMode operatingMode;

    /// <summary> 主远程服务器地址 </summary>
    private string defaultHostServer = "http://127.0.0.1/CDN/Android/v1.0";

    /// <summary> 备用远程服务器地址 </summary>
    private string fallbackHostServer = "http://127.0.0.1/CDN/Android/v1.0";

    /// <summary> 初始化YooAssets并执行资源更新的协程入口 </summary>
    public IEnumerator InitializeAndUpdate(OperatingMode mode)
    {
        operatingMode = mode;

        Debug.Log("[YooAssetsInit] 初始化开始");

        Debug.Log($"[YooAssetsInit] OperatingMode:{mode}");

        YooAssets.Initialize();

        package = YooAssets.CreatePackage(packageName);

        YooAssets.SetDefaultPackage(package);

        var initOp = CreateInitParameters();

        yield return initOp; // 等待初始化完成

        // 检查初始化状态是否成功
        if (package.InitializeStatus != EOperationStatus.Succeed)
        {
            Debug.LogError("[YooAssetsInit] 初始化失败：" + package.InitializeStatus);
            yield break;
        }

        Debug.Log("[YooAssetsInit] 初始化成功，开始更新资源");

        // 执行资源版本和清单更新
        yield return UpdatePackageVersionAndManifest();

        // 资源更新流程
        yield return UpdatePackage();

        Debug.Log("[YooAssetsInit] 资源更新完成");
    }

    /// <summary> 创建初始化参数，根据平台环境返回不同的初始化配置 </summary>
    private InitializationOperation CreateInitParameters()
    {
        switch (operatingMode)
        {
            case OperatingMode.EditorSimulateMode:
                {
                    var buildResult = EditorSimulateModeHelper.SimulateBuild(packageName);
                    var packageRoot = buildResult.PackageRootDirectory;
                    var editorFileSystemParams = FileSystemParameters.CreateDefaultEditorFileSystemParameters(packageRoot);
                    var initParameters = new EditorSimulateModeParameters();
                    initParameters.EditorFileSystemParameters = editorFileSystemParams;
                    var initOperation = package.InitializeAsync(initParameters);
                    return initOperation;
                }

            case OperatingMode.OfflinePlayMode:
                {
                    var buildinFileSystemParams = FileSystemParameters.CreateDefaultBuildinFileSystemParameters();
                    var initParameters = new OfflinePlayModeParameters();
                    initParameters.BuildinFileSystemParameters = buildinFileSystemParams;
                    var initOperation = package.InitializeAsync(initParameters);
                    return initOperation;
                }

            case OperatingMode.HostPlayMode:
                {
                    IRemoteServices remoteServices = new RemoteServices(defaultHostServer, fallbackHostServer);
                    var cacheFileSystemParams = FileSystemParameters.CreateDefaultCacheFileSystemParameters(remoteServices);
                    var buildinFileSystemParams = FileSystemParameters.CreateDefaultBuildinFileSystemParameters();
                    var initParameters = new HostPlayModeParameters();
                    initParameters.BuildinFileSystemParameters = buildinFileSystemParams;
                    initParameters.CacheFileSystemParameters = cacheFileSystemParams;
                    var initOperation = package.InitializeAsync(initParameters);
                    return initOperation;
                }
            case OperatingMode.WebPlayMode:
                {
                    IRemoteServices remoteServices = new RemoteServices(defaultHostServer, fallbackHostServer);
                    var webServerFileSystemParams = FileSystemParameters.CreateDefaultWebServerFileSystemParameters();
                    var webRemoteFileSystemParams = FileSystemParameters.CreateDefaultWebRemoteFileSystemParameters(remoteServices); //支持跨域下载
                    var initParameters = new WebPlayModeParameters();
                    initParameters.WebServerFileSystemParameters = webServerFileSystemParams;
                    initParameters.WebRemoteFileSystemParameters = webRemoteFileSystemParams;
                    var initOperation = package.InitializeAsync(initParameters);
                    return initOperation;
                }

            default:
                Debug.LogError("[YooAssetsInit] 未知的操作模式，请检查配置");
                return null;
        }
    }

    /// <summary> 执行资源版本和清单更新，并下载缺失资源 </summary>
    private IEnumerator UpdatePackageVersionAndManifest()
    {
        var package = YooAssets.GetPackage(packageName);

        // ① 获取资源版本号（通常是远端服务器上的版本）
        var versionOperation = package.RequestPackageVersionAsync();
        yield return versionOperation;

        if (versionOperation.Status != EOperationStatus.Succeed)
        {
            Debug.LogError($"[YooAsset] 获取版本号失败：{versionOperation.Error}");
            yield break;
        }

        string packageVersion = versionOperation.PackageVersion;
        Debug.Log($"[YooAsset] 获取到版本号：{packageVersion}");

        // ② 更新资源清单（Manifest）
        var manifestOperation = package.UpdatePackageManifestAsync(packageVersion);
        yield return manifestOperation;

        if (manifestOperation.Status != EOperationStatus.Succeed)
        {
            Debug.LogError($"[YooAsset] 更新资源清单失败：{manifestOperation.Error}");
            yield break;
        }

        Debug.Log("[YooAsset] 清单更新成功");
    }

    /// <summary> 下载缺失或待更新的资源包内容 </summary>
    private IEnumerator UpdatePackage()
    {
        var package = YooAssets.GetPackage(packageName);

        // ③ 创建资源下载器（全量或按需）
        int downloadingMaxNum = 10;
        int failedTryAgain = 3;

        var downloader = package.CreateResourceDownloader(downloadingMaxNum, failedTryAgain);

        if (downloader.TotalDownloadCount == 0)
        {
            Debug.Log("[YooAsset] 所有资源均已存在，无需下载");
            yield break;
        }

        downloader.DownloadErrorCallback = (args) =>
        {
            Debug.LogError($"[YooAsset] 下载出错：PackageName:{args.PackageName} FileName:{args.FileName}, Error: {args.ErrorInfo}");
        };

        downloader.DownloadUpdateCallback = (args) =>
        {
            Debug.Log($"[YooAsset] 下载进度：{args.Progress:P1}");
        };

        downloader.DownloadFinishCallback = (args) =>
        {
            Debug.Log("[YooAsset] 下载完成");
        };

        // ④ 开始下载
        downloader.BeginDownload();
        yield return downloader;

        if (downloader.Status == EOperationStatus.Succeed)
        {
            Debug.Log("[YooAsset] 所有资源下载成功");
        }
        else
        {
            Debug.LogError("[YooAsset] 下载失败，请检查网络或重试");
        }
    }


    /// <summary> 远端资源地址查询服务类 </summary>
    public class RemoteServices : IRemoteServices
    {
        private readonly string defaultHostServer;
        private readonly string fallbackHostServer;

        public RemoteServices(string defaultHostServer, string fallbackHostServer)
        {
            this.defaultHostServer = defaultHostServer;
            this.fallbackHostServer = fallbackHostServer;
        }

        string IRemoteServices.GetRemoteMainURL(string fileName)
        {
            return $"{defaultHostServer}/{fileName}";
        }

        string IRemoteServices.GetRemoteFallbackURL(string fileName)
        {
            return $"{fallbackHostServer}/{fileName}";
        }
    }
}