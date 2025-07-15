using UnityEditor;
using UnityEngine;

public class HotKey 
{
    // 添加一个快捷键菜单项，Ctrl + G
    [MenuItem("Tools/HotKey/Toggle Active State %g")] // % 表示 Ctrl（在 Mac 上是 Cmd）
    private static void ToggleActiveState()
    {
        // 获取当前选中的所有对象
        GameObject[] selectedObjects = Selection.gameObjects;

        if (selectedObjects.Length == 0)
        {
            return;
        }

        // 记录撤销操作
        Undo.RecordObjects(selectedObjects, "Toggle Active State");

        foreach (GameObject obj in selectedObjects)
        {
            // 切换激活状态
            obj.SetActive(!obj.activeSelf);
            EditorUtility.SetDirty(obj);
        }
    }
    
    [MenuItem("Tools/HotKey/Toggle Play Mode %q")]  // % 表示 Ctrl，q 是快捷键字母
    private static void TogglePlayMode()
    {
        // 切换 Play 模式
        EditorApplication.isPlaying = !EditorApplication.isPlaying;
    }

}
