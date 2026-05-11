using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConfigTool
{
    [CreateAssetMenu(fileName = "ConfigToolData", menuName = "ConfigSetting/Configuration Data", order = 1)]
    public class ConfigToolData : ScriptableObject
    {
        [Header("基本信息")]
        public string projectName = "ConfigToolProject";
        public string version = "1.0.0";
        public string description = "";

        [Header("相机点位 - 单个")]
        public List<CameraPointData> singleCameraPoints = new List<CameraPointData>();

        [Header("相机点位 - 列表")]
        public List<CameraPointListData> cameraPointLists = new List<CameraPointListData>();

        [Header("场景物体 - 单个")]
        public List<SceneObjectData> singleSceneObjects = new List<SceneObjectData>();

        [Header("场景物体 - 列表")]
        public List<SceneObjectListData> sceneObjectLists = new List<SceneObjectListData>();

        [Header("风格配置")]
        public List<StyleMaterialData> styleMaterials = new List<StyleMaterialData>();

        public void Reset()
        {
            singleCameraPoints.Clear();
            cameraPointLists.Clear();
            singleSceneObjects.Clear();
            sceneObjectLists.Clear();
            styleMaterials.Clear();
        }
    }

    [Serializable]
    public class StyleMaterialData
    {
        public string materialName;
        public Material material;
        public List<CustomFieldData> customFields = new List<CustomFieldData>();

        public StyleMaterialData()
        {
            materialName = "NewMaterial";
            material = null;
        }
    }
}
