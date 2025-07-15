using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

public class AppStart : MonoBehaviour
{
    private YooAssetsInit yooAssetsInit;
    private GlobalConfig globalConfig;
    
    private void Awake()
    {
        Init();
        DontDestroyOnLoad(gameObject);
        yooAssetsInit = new YooAssetsInit();
    }

    private void Init()
    {
        globalConfig = Resources.Load<GlobalConfig>("GlobalConfig");
    }

    private IEnumerator Start()
    {
        yield return yooAssetsInit.InitializeAndUpdate(globalConfig.operatingMode);
        
        Debug.Log("[AppStart] 启动完成");
        
        Task task = CodeLoader.Instance().DownloadAsync(globalConfig);
        while (!task.IsCompleted)
            yield return null;
        
        CodeLoader.Instance().LoadHotUpdateAssembly();


        Assembly hotUpdateAss = CodeLoader.Instance().GetHotUpdateAssembly();
        Type type = hotUpdateAss.GetType("Hello");
        type.GetMethod("Run").Invoke(null, null);
    }
}
