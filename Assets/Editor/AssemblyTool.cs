using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

public static class Define
{
    /// <summary>
    /// 编辑器下加载热更dll的目录
    /// </summary>
    public const string CodeDir = "Assets/Bundles/Code";

    /// <summary>
    /// VS或Rider工程生成dll的所在目录, 使用HybridCLR打包时需要使用
    /// </summary>
    public const string BuildOutputDir = "Temp/Bin/Debug";
}

public class AssemblyTool
{
    /// <summary> Unity线程的同步上下文 </summary>
    static SynchronizationContext unitySynchronizationContext { get; set; }
    
    /// <summary> 热更程序集名字数组 </summary>
    public static readonly string[] DllNames = { "HotUpdate"};

    /// <summary> AOT编译的dll列表 </summary>
    public static readonly string[] AotDllList = { "mscorlib.dll", "System.dll", "System.Core.dll" };
    
    
    [InitializeOnLoadMethod]
    static void Initialize()
    {
        unitySynchronizationContext = SynchronizationContext.Current;
    }
    
    /// <summary> 菜单和快捷键编译按钮 </summary>
    [MenuItem("Tools/Dll/Compile Update_HotDll _F6", false)]
    static void MenuItemOfCompile()
    {
        // 强制刷新一下，防止关闭auto refresh，文件修改时间不准确
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        DoCompile();
    }
    
    /// <summary> 执行编译代码流程 </summary>
    public static void DoCompile()
    {
        // 强制刷新一下，防止关闭auto refresh，编译出老代码
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        

        bool isCompileOk = CompileDlls();
        if (!isCompileOk)
        {
            return;
        }

        CopyHotUpdateDlls();

                        
        Debug.Log($"Compile Finish!");
    }
    
    
    /// <summary> 编译成dll </summary>
    static bool CompileDlls()
    {
        // 运行时编译需要先设置为UnitySynchronizationContext, 编译完再还原为CurrentContext
        SynchronizationContext lastSynchronizationContext = Application.isPlaying ? SynchronizationContext.Current : null;
        SynchronizationContext.SetSynchronizationContext(unitySynchronizationContext);

        bool isCompileOk = false;

        try
        {
            Directory.CreateDirectory(Define.BuildOutputDir);
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
            ScriptCompilationSettings scriptCompilationSettings = new()
            {
                group = group,
                target = target,
                extraScriptingDefines = new[] { "UNITY_COMPILE" },
                options = EditorUserBuildSettings.development ? ScriptCompilationOptions.DevelopmentBuild : ScriptCompilationOptions.None
            };
            ScriptCompilationResult result = PlayerBuildInterface.CompilePlayerScripts(scriptCompilationSettings, Define.BuildOutputDir);
            isCompileOk = result.assemblies.Count > 0;
            EditorUtility.ClearProgressBar();
        }
        finally
        {
            if (lastSynchronizationContext != null)
            {
                SynchronizationContext.SetSynchronizationContext(lastSynchronizationContext);
            }
        }

        return isCompileOk;
    }
    
    /// <summary> 将dll文件复制到加载目录 </summary>
    static void CopyHotUpdateDlls()
    {
        FileHelper.CleanDirectory(Define.CodeDir);
        foreach (string dllName in DllNames)
        {
            string sourceDll = $"{Define.BuildOutputDir}/{dllName}.dll";
            string sourcePdb = $"{Define.BuildOutputDir}/{dllName}.pdb";
            File.Copy(sourceDll, $"{Define.CodeDir}/{dllName}.dll.bytes", true);
            File.Copy(sourcePdb, $"{Define.CodeDir}/{dllName}.pdb.bytes", true);
        }

        AssetDatabase.Refresh();
    }
    
    /// <summary> 菜单和快捷键编译按钮 </summary>
    [MenuItem("Tools/Dll/Compile Update_AotDll _F7", false)]
    static void MenuItemOfCompileF7()
    {
        // 强制刷新一下，防止关闭auto refresh，文件修改时间不准确
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        CopyAotDllsToAssets();
    }
    
    
    public static void CopyAotDllsToAssets()
    {
        string targetPlatform = GetPlatformFolderName(EditorUserBuildSettings.activeBuildTarget);
        
        // 项目根目录（Application.dataPath 返回 Assets 目录路径，需要上一级）
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        // 源目录
        string srcDir = Path.Combine(projectRoot, "HybridCLRData", "AssembliesPostIl2CppStrip", targetPlatform);

        // 目标目录
        string dstDir = Path.Combine(Application.dataPath, "Bundles", "AotDlls");

        // 如果目标目录不存在，则创建
        if (!Directory.Exists(dstDir))
        {
            Directory.CreateDirectory(dstDir);
            Debug.Log($"Created directory: {dstDir}");
        }

        foreach (var dllName in AotDllList)
        {
            string srcFile = Path.Combine(srcDir, dllName);
            if (!File.Exists(srcFile))
            {
                Debug.LogError($"[CopyAotDllsToAssets] DLL not found: {srcFile}");
                continue;
            }

            string dstFile = Path.Combine(dstDir, dllName);
            try
            {
                File.Copy(srcFile, dstFile, true);
                Debug.Log($"Copied {dllName} to {dstFile}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to copy {dllName} from {srcFile} to {dstFile}. Exception: {ex.Message}");
            }
        }

        RenameDllsToBytes(dstDir);
        
        AssetDatabase.Refresh();
        
        Debug.Log($"Compile Finish!");
    }
    
    private static string GetPlatformFolderName(BuildTarget buildTarget)
    {
        switch (buildTarget)
        {
            case BuildTarget.Android: return "Android";
            case BuildTarget.iOS: return "iOS";
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                return "Windows";
            case BuildTarget.StandaloneOSX:
                return "OSX";
            case BuildTarget.WebGL:
                return "WebGL";
            // 根据你的目录结构补充其他平台
            default:
                return null; // 未知平台
        }
    }
    
    /// <summary> 将指定目录下的所有 .dll 文件重命名为 .dll.bytes </summary>
    public static void RenameDllsToBytes(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Debug.LogError($"[AotDllCopier] Directory not found: {directory}");
            return;
        }

        var dllFiles = Directory.GetFiles(directory, "*.dll");

        foreach (var dllPath in dllFiles)
        {
            string newPath = dllPath + ".bytes";

            try
            {
                if (File.Exists(newPath))
                {
                    File.Delete(newPath); // 删除旧的 .dll.bytes 文件以避免冲突
                }

                File.Move(dllPath, newPath);
                Debug.Log($"[AotDllCopier] Renamed: {Path.GetFileName(dllPath)} -> {Path.GetFileName(newPath)}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AotDllCopier] Failed to rename {dllPath}: {ex.Message}");
            }
        }
    }
}
