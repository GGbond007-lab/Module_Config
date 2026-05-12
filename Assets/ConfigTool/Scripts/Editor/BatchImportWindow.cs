using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace ConfigTool.Editor
{
    public class BatchImportWindow : EditorWindow
    {
        private string importPrefix = "";
        private string importSuffix = "";
        private string parentObjectName = "";
        private bool useParentObject = false;
        private ImportMode importMode = ImportMode.ByNameFilter;
        private BatchImportTarget importTarget = BatchImportTarget.SingleCameraPoints;
        private int cameraPointListIndex = 0;
        private int sceneObjectListIndex = 0;

        private List<GameObject> previewObjects = new List<GameObject>();

        [MenuItem("ConfigSetting/批量导入工具")]
        public static void ShowWindow()
        {
            var window = GetWindow<BatchImportWindow>("批量导入工具");
            window.minSize = new Vector2(400, 500);
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("批量导入工具", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            importMode = (ImportMode)EditorGUILayout.EnumPopup("导入模式", importMode);
            EditorGUILayout.Space();

            switch (importMode)
            {
                case ImportMode.ByNameFilter:
                    DrawNameFilterMode();
                    break;
                case ImportMode.ByParentObject:
                    DrawParentObjectMode();
                    break;
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("预览", GUILayout.Height(30)))
            {
                RefreshPreview();
            }

            if (previewObjects.Count > 0)
            {
                EditorGUILayout.Space();
                DrawPreviewList();
            }

            EditorGUILayout.Space();

            ConfigToolData selectedConfig = Selection.activeObject as ConfigToolData;
            importTarget = (BatchImportTarget)EditorGUILayout.EnumPopup("导入目标", importTarget);
            DrawImportTargetOptions(selectedConfig);

            EditorGUILayout.Space();

            GUI.enabled = previewObjects.Count > 0;
            if (GUILayout.Button($"导入 {previewObjects.Count} 个物体", GUILayout.Height(40)))
            {
                ImportObjects();
            }
            GUI.enabled = true;

            EditorGUILayout.EndVertical();
        }

        private void DrawImportTargetOptions(ConfigToolData config)
        {
            if (config == null)
            {
                EditorGUILayout.HelpBox("请选择一个配置文件，或导入时在弹窗中选择。列表目标需要先选中配置文件才能同步显示列表。", MessageType.Info);
                return;
            }

            if (importTarget == BatchImportTarget.CameraPointList)
            {
                DrawCameraPointListSelector(config);
            }
            else if (importTarget == BatchImportTarget.SceneObjectList)
            {
                DrawSceneObjectListSelector(config);
            }
        }

        private void DrawCameraPointListSelector(ConfigToolData config)
        {
            if (config.cameraPointLists.Count == 0)
            {
                EditorGUILayout.HelpBox("当前配置没有相机点位列表，导入时会自动创建 ImportedCameraPoints。", MessageType.Info);
                return;
            }

            cameraPointListIndex = Mathf.Clamp(cameraPointListIndex, 0, config.cameraPointLists.Count - 1);
            string[] listNames = new string[config.cameraPointLists.Count];
            for (int i = 0; i < config.cameraPointLists.Count; i++)
            {
                string listName = config.cameraPointLists[i].listName;
                listNames[i] = string.IsNullOrEmpty(listName) ? $"未命名相机点位列表 {i + 1}" : listName;
            }

            cameraPointListIndex = EditorGUILayout.Popup("相机点位列表", cameraPointListIndex, listNames);
        }

        private void DrawSceneObjectListSelector(ConfigToolData config)
        {
            if (config.sceneObjectLists.Count == 0)
            {
                EditorGUILayout.HelpBox("当前配置没有场景物体列表，导入时会自动创建 ImportedObjects。", MessageType.Info);
                return;
            }

            sceneObjectListIndex = Mathf.Clamp(sceneObjectListIndex, 0, config.sceneObjectLists.Count - 1);
            string[] listNames = new string[config.sceneObjectLists.Count];
            for (int i = 0; i < config.sceneObjectLists.Count; i++)
            {
                string listName = config.sceneObjectLists[i].listName;
                listNames[i] = string.IsNullOrEmpty(listName) ? $"未命名场景物体列表 {i + 1}" : listName;
            }

            sceneObjectListIndex = EditorGUILayout.Popup("场景物体列表", sceneObjectListIndex, listNames);
        }

        private void DrawNameFilterMode()
        {
            EditorGUILayout.LabelField("名称过滤:", EditorStyles.boldLabel);
            importPrefix = EditorGUILayout.TextField("前缀", importPrefix);
            importSuffix = EditorGUILayout.TextField("后缀", importSuffix);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("示例:");
            EditorGUILayout.LabelField("- Camera_* 将匹配所有以 Camera_ 开头的物体");
            EditorGUILayout.LabelField("- *_Sensor 将匹配所有以 _Sensor 结尾的物体");
            EditorGUILayout.LabelField("- Elevator_1F_* 将匹配所有以 Elevator_1F_ 开头的物体");
            EditorGUILayout.EndVertical();
        }

        private void DrawParentObjectMode()
        {
            useParentObject = EditorGUILayout.Toggle("使用父级对象", useParentObject);
            if (useParentObject)
            {
                parentObjectName = EditorGUILayout.TextField("父级名称", parentObjectName);
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("说明:");
            EditorGUILayout.LabelField("1. 如果启用父级对象，将只导入其子物体");
            EditorGUILayout.LabelField("2. 如果禁用，将导入场景中所有匹配的对象");
            EditorGUILayout.LabelField("3. 名称过滤同时生效");
            EditorGUILayout.EndVertical();
        }

        private void DrawPreviewList()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"预览 ({previewObjects.Count} 个对象):", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("scrollview");
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));

            foreach (GameObject obj in previewObjects)
            {
                if (obj != null)
                {
                    EditorGUILayout.ObjectField(obj, typeof(GameObject), true);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private Vector2 scrollPos;

        private void RefreshPreview()
        {
            previewObjects.Clear();

            Scene currentScene = SceneManager.GetActiveScene();
            if (!currentScene.isLoaded)
            {
                Debug.LogWarning("No active scene is loaded.");
                return;
            }

            List<GameObject> allObjects = new List<GameObject>();
            GameObject[] roots = currentScene.GetRootGameObjects();

            foreach (GameObject root in roots)
            {
                if (useParentObject && !string.IsNullOrEmpty(parentObjectName))
                {
                    if (root.name == parentObjectName)
                    {
                        allObjects.AddRange(GetChildObjects(root.transform));
                    }
                }
                else
                {
                    if (MatchesFilter(root.name))
                    {
                        allObjects.Add(root);
                    }
                    allObjects.AddRange(GetChildObjects(root.transform));
                }
            }

            previewObjects = allObjects;
        }

        private List<GameObject> GetChildObjects(Transform parent)
        {
            List<GameObject> children = new List<GameObject>();
            foreach (Transform child in parent)
            {
                if (MatchesFilter(child.name))
                {
                    children.Add(child.gameObject);
                }
                children.AddRange(GetChildObjects(child));
            }
            return children;
        }

        private bool MatchesFilter(string objectName)
        {
            bool prefixMatch = string.IsNullOrEmpty(importPrefix) || objectName.StartsWith(importPrefix);
            bool suffixMatch = string.IsNullOrEmpty(importSuffix) || objectName.EndsWith(importSuffix);
            return prefixMatch && suffixMatch;
        }

        private void ImportObjects()
        {
            if (previewObjects.Count == 0)
            {
                EditorUtility.DisplayDialog("导入失败", "没有对象可导入", "确定");
                return;
            }

            ConfigToolData config = Selection.activeObject as ConfigToolData;
            if (config == null)
            {
                string configPath = EditorUtility.OpenFilePanel("选择配置文件", "Assets", "asset");
                if (string.IsNullOrEmpty(configPath))
                {
                    return;
                }

                configPath = "Assets" + configPath.Substring(Application.dataPath.Length);
                config = AssetDatabase.LoadAssetAtPath<ConfigToolData>(configPath);

                if (config == null)
                {
                    EditorUtility.DisplayDialog("错误", "无法加载配置文件", "确定");
                    return;
                }
            }

            ImportObjectsToTarget(config);

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("导入完成", $"成功导入 {previewObjects.Count} 个物体到配置", "确定");
        }

        private string GenerateObjectId(GameObject obj)
        {
            return $"{obj.name}_{obj.GetInstanceID()}";
        }

        private void ImportObjectsToTarget(ConfigToolData config)
        {
            if (importTarget == BatchImportTarget.CameraPointList)
            {
                EnsureCameraPointListExists(config);
            }
            else if (importTarget == BatchImportTarget.SceneObjectList)
            {
                EnsureSceneObjectListExists(config);
            }

            foreach (GameObject obj in previewObjects)
            {
                if (obj == null)
                {
                    continue;
                }

                switch (importTarget)
                {
                    case BatchImportTarget.SingleCameraPoints:
                        config.singleCameraPoints.Add(CreateCameraPointData(obj));
                        break;
                    case BatchImportTarget.CameraPointList:
                        config.cameraPointLists[cameraPointListIndex].cameraPoints.Add(CreateCameraPointData(obj));
                        break;
                    case BatchImportTarget.SingleSceneObjects:
                        config.singleSceneObjects.Add(CreateSceneObjectData(obj));
                        break;
                    case BatchImportTarget.SceneObjectList:
                        config.sceneObjectLists[sceneObjectListIndex].sceneObjects.Add(CreateSceneObjectData(obj));
                        break;
                }
            }
        }

        private void EnsureCameraPointListExists(ConfigToolData config)
        {
            if (config.cameraPointLists.Count == 0)
            {
                config.cameraPointLists.Add(new CameraPointListData("ImportedCameraPoints", "批量导入的相机点位"));
                cameraPointListIndex = 0;
            }

            cameraPointListIndex = Mathf.Clamp(cameraPointListIndex, 0, config.cameraPointLists.Count - 1);
        }

        private void EnsureSceneObjectListExists(ConfigToolData config)
        {
            if (config.sceneObjectLists.Count == 0)
            {
                config.sceneObjectLists.Add(new SceneObjectListData("ImportedObjects", "批量导入的物体"));
                sceneObjectListIndex = 0;
            }

            sceneObjectListIndex = Mathf.Clamp(sceneObjectListIndex, 0, config.sceneObjectLists.Count - 1);
        }

        private CameraPointData CreateCameraPointData(GameObject obj)
        {
            Vector3 targetPosition = obj.transform.position + obj.transform.forward;
            Camera camera = obj.GetComponent<Camera>();
            if (camera != null)
            {
                targetPosition = obj.transform.position + obj.transform.forward * Mathf.Max(camera.farClipPlane * 0.1f, 1f);
            }

            return new CameraPointData(obj.name, obj.transform.position, targetPosition);
        }

        private SceneObjectData CreateSceneObjectData(GameObject obj)
        {
            return new SceneObjectData(obj, GenerateObjectId(obj));
        }

        private enum ImportMode
        {
            ByNameFilter,
            ByParentObject
        }

        private enum BatchImportTarget
        {
            [InspectorName("相机点位")]
            SingleCameraPoints,
            [InspectorName("相机点位列表")]
            CameraPointList,
            [InspectorName("场景物体")]
            SingleSceneObjects,
            [InspectorName("场景物体列表")]
            SceneObjectList
        }
    }
}
