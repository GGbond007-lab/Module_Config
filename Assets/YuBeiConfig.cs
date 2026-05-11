/// <summary>
/// 数字孪生运行时管理器 - 自动生成
/// 项目: ConfigToolProject
/// 版本: 1.0.0
/// </summary>

using UnityEngine;
using System.Collections.Generic;
using ConfigTool;

namespace ConfigTool.Generated
{
    public class YuBeiConfig : MonoBehaviour
    {
        [System.Serializable]
        public class CameraPoint
        {
            public string pointName;
            public Vector3 position;
            public Vector3 targetPosition;
        }

        public List<CameraPoint> cameraPoints = new List<CameraPoint>();
        [System.Serializable]
        public class SceneObject
        {
            public string objectName;
            public string objectId;
            public GameObject referenceObject;
        }

        public List<SceneObject> sceneObjects = new List<SceneObject>();

        private void Awake()
        {
            InitializeFromConfig();
        }

        public void ApplyGeneratedConfig(GeneratedConfigBuffer buffer)
        {
            if (buffer == null)
            {
                return;
            }

            cameraPoints.Clear();
            foreach (var source in buffer.singleCameraPoints)
            {
                var item = new CameraPoint();
                item.pointName = source.pointName;
                item.position = source.position;
                item.targetPosition = source.targetPosition;
                cameraPoints.Add(item);
            }

            sceneObjects.Clear();
            foreach (var source in buffer.singleSceneObjects)
            {
                var item = new SceneObject();
                item.objectName = source.objectName;
                item.objectId = source.objectId;
                item.referenceObject = source.referenceObject;
                sceneObjects.Add(item);
            }

        }

        private void InitializeFromConfig()
        {
            // TODO: 从配置文件初始化数据
        }
    }
}
