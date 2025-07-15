using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Settings/CreateGlobalConfig", fileName = "GlobalConfig", order = 0)]
public class GlobalConfig : ScriptableObject
{
    public OperatingMode operatingMode = OperatingMode.EditorSimulateMode;
}
