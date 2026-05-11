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
        private ObjectReferenceType referenceType = ObjectReferenceType.ByReference;

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
                case ImportMode.ByComponent:
                    DrawComponentMode();
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

            referenceType = (ObjectReferenceType)EditorGUILayout.EnumPopup("引用方式", referenceType);

            EditorGUILayout.Space();

            GUI.enabled = previewObjects.Count > 0;
            if (GUILayout.Button($"导入 {previewObjects.Count} 个物体", GUILayout.Height(40)))
            {
                ImportObjects();
            }
            GUI.enabled = true;

            EditorGUILayout.EndVertical();
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

        private void DrawComponentMode()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("组件模式说明:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("1. 可以按组件类型筛选对象");
            EditorGUILayout.LabelField("2. 支持任何 Unity 组件类型");
            EditorGUILayout.LabelField("3. 结合名称过滤可以获得精确结果");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            importPrefix = EditorGUILayout.TextField("名称前缀", importPrefix);
            importSuffix = EditorGUILayout.TextField("名称后缀", importSuffix);
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

            foreach (GameObject obj in previewObjects)
            {
                if (obj == null) continue;

                SceneObjectData objData = new SceneObjectData(obj);
                objData.objectId = GenerateObjectId(obj);
                config.singleSceneObjects.Add(objData);
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("导入完成", $"成功导入 {previewObjects.Count} 个物体到配置", "确定");
        }

        private string GenerateObjectId(GameObject obj)
        {
            return $"{obj.name}_{obj.GetInstanceID()}";
        }

        private enum ImportMode
        {
            ByNameFilter,
            ByParentObject,
            ByComponent
        }

        private enum ObjectReferenceType
        {
            ByReference,
            ByName,
            ByID
        }
    }
}
