using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConfigTool
{
    [CreateAssetMenu(fileName = "ConfigToolData", menuName = "ConfigSetting/Configuration Data", order = 1)]
    public class ConfigToolData : ScriptableObject
    {
        [HideInInspector]
        public List<CustomModelData> customModels = new List<CustomModelData>();
        [HideInInspector]
        public List<CustomConfigData> singleCustomConfigs = new List<CustomConfigData>();

        public void Reset()
        {
            customModels.Clear();
            singleCustomConfigs.Clear();
        }
    }
}
