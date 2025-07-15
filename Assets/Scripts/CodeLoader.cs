using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using HybridCLR;
using UnityEngine;


public class CodeLoader : BaseManager<CodeLoader>
{
    private Assembly hotUpdateAssembly;
    private Dictionary<string, TextAsset> dlls;
    private Dictionary<string, TextAsset> aotDlls;
    private bool enableDll;

    public async Task DownloadAsync(GlobalConfig globalConfig)
    {
        enableDll = globalConfig.enableDll;
        dlls = await ABManager.Instance().LoadAllAssetsAsyncDic<TextAsset>($"Assets/Bundles/Code/HotUpdate.dll.bytes");
        aotDlls = await ABManager.Instance().LoadAllAssetsAsyncDic<TextAsset>($"Assets/Bundles/AotDlls/mscorlib.dll.bytes");
        Debug.Log("[CodeLoader] 下载dll完成");
    }

    public void LoadHotUpdateAssembly()
    {
        byte[] hotUpdateAssBytes;
        if (enableDll)
            hotUpdateAssBytes = dlls["HotUpdate.dll"].bytes;
        else
            hotUpdateAssBytes = File.ReadAllBytes(Path.Combine("Assets/Bundles/Code", "HotUpdate.dll.bytes"));
        hotUpdateAssembly = Assembly.Load(hotUpdateAssBytes);
        Debug.Log("[CodeLoader] LoadHotUpdateAssembly完成");
    }
    
    public Assembly GetHotUpdateAssembly()
    {
        if (hotUpdateAssembly == null)
        {
            throw new Exception("HotUpdate assembly is not loaded. Please call LoadHotUpdateAssembly first.");
        }
        return hotUpdateAssembly;
    }

}