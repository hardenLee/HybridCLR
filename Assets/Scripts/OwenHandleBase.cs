using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;

public class OwenHandleBase
{
    public HandleBase YooHandle { get; private set; }
    public int OwenRefCount { get; private set; }

    public OwenHandleBase(HandleBase yooHandle)
    {
        YooHandle = yooHandle;
        OwenRefCount = 1;
    }

    public void AddRef()
    {
        OwenRefCount++;
    }

    public bool Release()
    {
        OwenRefCount--;

        if (OwenRefCount <= 0)
        {
            YooHandle.Release();
            return true; // 表示已完全释放
        }

        return false;
    }
}
