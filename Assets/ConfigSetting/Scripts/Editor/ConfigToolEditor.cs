using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace ConfigTool.Editor
{
    public class ConfigToolEditor : EditorWindow
    {
        private ConfigToolData currentConfig;
        private Vector2 scrollPosition;
        private string[] toolbarStrings = { "相机点位", "场景物体", "风格配置", "批量导入" };
        private int toolbarIndex = 0;
        private bool showSingleCameraPoints = true;
        private bool showCameraPointLists = true;
        private bool showSingleSceneObjects = true;
        private bool showSceneObjectLists = true;
        private bool showStyleConfig = true;

        private string batchImportParentName = "";
        private string batchImportNamePrefix = "";
        private string batchImportNameSuffix = "";
        private bool batchImportUseHierarchy = true;
        private BatchImportTarget batchImportTarget = BatchImportTarget.SingleSceneObjects;

        private string generatedCodePreview = "";

        private const string PendingGenerateSavePathKey = "ConfigTool.PendingGenerate.SavePath";
        private const string PendingGenerateConfigPathKey = "ConfigTool.PendingGenerate.ConfigPath";
        private const string PendingGenerateClassNameKey = "ConfigTool.PendingGenerate.ClassName";
        private const string PendingGenerateObjectNameKey = "ConfigTool.PendingGenerate.ObjectName";
        private const string PendingGenerateRetryCountKey = "ConfigTool.PendingGenerate.RetryCount";
        private const int PendingGenerateMaxRetries = 120;

        [InitializeOnLoadMethod]
        private static void ResumePendingGenerateAfterReload()
        {
            if (!SessionState.GetBool(PendingGenerateSavePathKey + ".Active", false))
            {
                return;
            }

            EditorApplication.delayCall += CompletePendingGenerate;
        }

        [MenuItem("ConfigSetting/配置编辑器")]
        public static void ShowWindow()
        {
            var window = GetWindow<ConfigToolEditor>("数字孪生配置编辑器");
            window.minSize = new Vector2(800, 600);
        }

        [MenuItem("ConfigSetting/创建新配置")]
        public static void CreateNewConfiguration()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "创建数字孪生配置",
                "ConfigToolData",
                "asset",
                "请选择配置文件的保存位置"
            );

            if (!string.IsNullOrEmpty(path))
            {
                var config = CreateInstance<ConfigToolData>();
                AssetDatabase.CreateAsset(config, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = config;
                EditorGUIUtility.PingObject(config);
            }
        }

        [MenuItem("ConfigSetting/生成运行时管理器")]
        public static void GenerateRuntimeManager()
        {
            if (!Selection.activeObject)
            {
                EditorUtility.DisplayDialog("错误", "请先选择一个配置文件", "确定");
                return;
            }

            if (!(Selection.activeObject is ConfigToolData))
            {
                EditorUtility.DisplayDialog("错误", "请选择一个数字孪生配置文件", "确定");
                return;
            }

            var config = (ConfigToolData)Selection.activeObject;
            ConfigToolEditor window = GetWindow<ConfigToolEditor>();
            window.currentConfig = config;
            window.GenerateCodePreview();
            window.GenerateAndAddToScene();
        }

        private void OnEnable()
        {
            if (Selection.activeObject is ConfigToolData)
            {
                currentConfig = (ConfigToolData)Selection.activeObject;
            }
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is ConfigToolData)
            {
                currentConfig = (ConfigToolData)Selection.activeObject;
                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            DrawHeader();
            DrawToolbar();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            if (currentConfig == null)
            {
                DrawNoConfigSelected();
            }
            else
            {
                switch (toolbarIndex)
                {
                    case 0:
                        DrawCameraPointsSection();
                        break;
                    case 1:
                        DrawSceneObjectsSection();
                        break;
                    case 2:
                        DrawStyleSection();
                        break;
                    case 3:
                        DrawBatchImportSection();
                        break;
                }
            }

            EditorGUILayout.EndScrollView();
            DrawFooter();
            EditorGUILayout.EndVertical();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("数字孪生配置编辑器", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("当前配置:", GUILayout.Width(100));
            currentConfig = (ConfigToolData)EditorGUILayout.ObjectField(currentConfig, typeof(ConfigToolData), false);

            if (GUILayout.Button("加载配置", GUILayout.Width(80)))
            {
                string path = EditorUtility.OpenFilePanel("选择配置文件", "Assets", "asset");
                if (!string.IsNullOrEmpty(path))
                {
                    path = "Assets" + path.Substring(Application.dataPath.Length);
                    currentConfig = AssetDatabase.LoadAssetAtPath<ConfigToolData>(path);
                    Selection.activeObject = currentConfig;
                }
            }

            if (GUILayout.Button("保存配置", GUILayout.Width(80)))
            {
                if (currentConfig != null)
                {
                    EditorUtility.SetDirty(currentConfig);
                    AssetDatabase.SaveAssets();
                    EditorUtility.DisplayDialog("成功", "配置已保存", "确定");
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawToolbar()
        {
            toolbarIndex = GUILayout.Toolbar(toolbarIndex, toolbarStrings);
            EditorGUILayout.Space();
        }

        private void DrawNoConfigSelected()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("请选择或创建一个配置文件", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            if (GUILayout.Button("创建新配置文件", GUILayout.Height(30)))
            {
                CreateNewConfiguration();
            }
            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();
        }

        private void DrawCameraPointsSection()
        {
            EditorGUILayout.BeginVertical("box");
            showSingleCameraPoints = EditorGUILayout.Foldout(showSingleCameraPoints, "单个相机点位", true);

            if (showSingleCameraPoints)
            {
                EditorGUI.indentLevel++;
                DrawSingleCameraPointList();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            showCameraPointLists = EditorGUILayout.Foldout(showCameraPointLists, "相机点位列表", true);

            if (showCameraPointLists)
            {
                EditorGUI.indentLevel++;
                DrawCameraPointLists();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSingleCameraPointList()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"相机点位数量: {currentConfig.singleCameraPoints.Count}");
            if (GUILayout.Button("添加相机点位", GUILayout.Width(120)))
            {
                currentConfig.singleCameraPoints.Add(new CameraPointData());
                EditorUtility.SetDirty(currentConfig);
            }
            EditorGUILayout.EndHorizontal();

            for (int i = currentConfig.singleCameraPoints.Count - 1; i >= 0; i--)
            {
                DrawSingleCameraPoint(currentConfig.singleCameraPoints[i], i);
            }
        }

        private void DrawSingleCameraPoint(CameraPointData point, int index)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            point.pointName = EditorGUILayout.TextField("名称", point.pointName);
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                currentConfig.singleCameraPoints.RemoveAt(index);
                EditorUtility.SetDirty(currentConfig);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("观看点位置", GUILayout.Width(100));
            point.position = EditorGUILayout.Vector3Field("", point.position);
            if (GUILayout.Button("从场景选择", GUILayout.Width(100)))
            {
                GameObject obj = Selection.activeGameObject;
                if (obj != null)
                {
                    point.position = obj.transform.position;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("目标点位置", GUILayout.Width(100));
            point.targetPosition = EditorGUILayout.Vector3Field("", point.targetPosition);
            if (GUILayout.Button("从场景选择", GUILayout.Width(100)))
            {
                GameObject obj = Selection.activeGameObject;
                if (obj != null)
                {
                    point.targetPosition = obj.transform.position;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            DrawCustomFieldsList(point.customFields, currentConfig, () => point.AddCustomField("NewField", FieldType.String));

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawCameraPointLists()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"列表数量: {currentConfig.cameraPointLists.Count}");
            if (GUILayout.Button("添加列表", GUILayout.Width(100)))
            {
                currentConfig.cameraPointLists.Add(new CameraPointListData());
                EditorUtility.SetDirty(currentConfig);
            }
            EditorGUILayout.EndHorizontal();

            for (int i = currentConfig.cameraPointLists.Count - 1; i >= 0; i--)
            {
                DrawCameraPointList(currentConfig.cameraPointLists[i], i);
            }
        }

        private void DrawCameraPointList(CameraPointListData list, int index)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            list.listName = EditorGUILayout.TextField("列表名称", list.listName);
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                currentConfig.cameraPointLists.RemoveAt(index);
                EditorUtility.SetDirty(currentConfig);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            list.listDescription = EditorGUILayout.TextField("描述", list.listDescription);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"点位数量: {list.cameraPoints.Count}");
            if (GUILayout.Button("添加点位", GUILayout.Width(100)))
            {
                list.cameraPoints.Add(new CameraPointData());
                EditorUtility.SetDirty(currentConfig);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel++;
            for (int j = 0; j < list.cameraPoints.Count; j++)
            {
                DrawSingleCameraPoint(list.cameraPoints[j], j);
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawSceneObjectsSection()
        {
            EditorGUILayout.BeginVertical("box");
            showSingleSceneObjects = EditorGUILayout.Foldout(showSingleSceneObjects, "单个场景物体", true);

            if (showSingleSceneObjects)
            {
                EditorGUI.indentLevel++;
                DrawSingleSceneObjectList();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            showSceneObjectLists = EditorGUILayout.Foldout(showSceneObjectLists, "场景物体列表", true);

            if (showSceneObjectLists)
            {
                EditorGUI.indentLevel++;
                DrawSceneObjectLists();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSingleSceneObjectList()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"场景物体数量: {currentConfig.singleSceneObjects.Count}");
            if (GUILayout.Button("添加物体", GUILayout.Width(100)))
            {
                currentConfig.singleSceneObjects.Add(new SceneObjectData());
                EditorUtility.SetDirty(currentConfig);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginVertical("box");
            DrawDragDropAreaSingleSceneObject();
            EditorGUILayout.EndVertical();

            for (int i = currentConfig.singleSceneObjects.Count - 1; i >= 0; i--)
            {
                DrawSingleSceneObject(currentConfig.singleSceneObjects[i], i);
            }
        }

        private void DrawDragDropAreaSingleSceneObject()
        {
            Rect dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "拖拽场景中的物体到此处，或从层级面板拖拽", EditorStyles.helpBox);

            Event evt = Event.current;
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                if (dropArea.Contains(evt.mousePosition))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (UnityEngine.Object obj in DragAndDrop.objectReferences)
                        {
                            if (obj is GameObject go)
                            {
                                currentConfig.singleSceneObjects.Add(new SceneObjectData(go));
                                EditorUtility.SetDirty(currentConfig);
                            }
                        }
                    }
                    evt.Use();
                }
            }
        }

        private void DrawSingleSceneObject(SceneObjectData objData, int index)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            objData.objectName = EditorGUILayout.TextField("名称", objData.objectName);
            objData.objectId = EditorGUILayout.TextField("ID", objData.objectId);

            EditorGUILayout.BeginVertical();
            objData.referenceObject = (GameObject)EditorGUILayout.ObjectField("引用", objData.referenceObject, typeof(GameObject), true);
            EditorGUILayout.EndVertical();

            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                currentConfig.singleSceneObjects.RemoveAt(index);
                EditorUtility.SetDirty(currentConfig);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            DrawCustomFieldsList(objData.customFields, currentConfig, () => objData.AddCustomField("NewField", FieldType.String));

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawSceneObjectLists()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"列表数量: {currentConfig.sceneObjectLists.Count}");
            if (GUILayout.Button("添加列表", GUILayout.Width(100)))
            {
                currentConfig.sceneObjectLists.Add(new SceneObjectListData());
                EditorUtility.SetDirty(currentConfig);
            }
            EditorGUILayout.EndHorizontal();

            for (int i = currentConfig.sceneObjectLists.Count - 1; i >= 0; i--)
            {
                DrawSceneObjectList(currentConfig.sceneObjectLists[i], i);
            }
        }

        private void DrawSceneObjectList(SceneObjectListData list, int index)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            list.listName = EditorGUILayout.TextField("列表名称", list.listName);
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                currentConfig.sceneObjectLists.RemoveAt(index);
                EditorUtility.SetDirty(currentConfig);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            list.listDescription = EditorGUILayout.TextField("描述", list.listDescription);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"物体数量: {list.sceneObjects.Count}");
            if (GUILayout.Button("添加物体", GUILayout.Width(100)))
            {
                list.sceneObjects.Add(new SceneObjectData());
                EditorUtility.SetDirty(currentConfig);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginVertical("box");
            DrawDragDropAreaSceneObjectList(list);
            EditorGUILayout.EndVertical();

            EditorGUI.indentLevel++;
            for (int j = 0; j < list.sceneObjects.Count; j++)
            {
                DrawSingleSceneObject(list.sceneObjects[j], j);
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawDragDropAreaSceneObjectList(SceneObjectListData list)
        {
            Rect dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "拖拽场景中的物体到此处添加到此列表", EditorStyles.helpBox);

            Event evt = Event.current;
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                if (dropArea.Contains(evt.mousePosition))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (UnityEngine.Object obj in DragAndDrop.objectReferences)
                        {
                            if (obj is GameObject go)
                            {
                                list.sceneObjects.Add(new SceneObjectData(go));
                                EditorUtility.SetDirty(currentConfig);
                            }
                        }
                    }
                    evt.Use();
                }
            }
        }

        private void DrawCustomFieldsList(List<CustomFieldData> fields, ConfigToolData config, Action addAction)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("自定义字段:");
            if (GUILayout.Button("+ 添加字段", GUILayout.Width(80)))
            {
                addAction();
                EditorUtility.SetDirty(config);
            }
            EditorGUILayout.EndHorizontal();

            for (int i = fields.Count - 1; i >= 0; i--)
            {
                var field = fields[i];
                EditorGUILayout.BeginHorizontal("box");
                field.fieldName = EditorGUILayout.TextField(field.fieldName, GUILayout.Width(120));
                field.fieldType = (FieldType)EditorGUILayout.EnumPopup(field.fieldType, GUILayout.Width(80));

                if (field.fieldType == FieldType.String)
                {
                    field.stringValue = EditorGUILayout.TextField(field.stringValue);
                }
                else if (field.fieldType == FieldType.Int)
                {
                    field.intValue = EditorGUILayout.IntField(field.intValue);
                }
                else if (field.fieldType == FieldType.Bool)
                {
                    field.boolValue = EditorGUILayout.Toggle(field.boolValue);
                }

                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    fields.RemoveAt(i);
                    EditorUtility.SetDirty(config);
                    EditorGUILayout.EndHorizontal();
                    return;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawStyleSection()
        {
            EditorGUILayout.BeginVertical("box");

            showStyleConfig = EditorGUILayout.Foldout(showStyleConfig, "风格配置", true);
            if (showStyleConfig)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"材质数量: {currentConfig.styleMaterials.Count}");
                if (GUILayout.Button("添加材质", GUILayout.Width(100)))
                {
                    currentConfig.styleMaterials.Add(new StyleMaterialData());
                    EditorUtility.SetDirty(currentConfig);
                }
                EditorGUILayout.EndHorizontal();

                for (int i = currentConfig.styleMaterials.Count - 1; i >= 0; i--)
                {
                    DrawStyleMaterial(currentConfig.styleMaterials[i], i);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawStyleMaterial(StyleMaterialData styleData, int index)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            styleData.materialName = EditorGUILayout.TextField("材质名称", styleData.materialName);
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                currentConfig.styleMaterials.RemoveAt(index);
                EditorUtility.SetDirty(currentConfig);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            styleData.material = (Material)EditorGUILayout.ObjectField("材质", styleData.material, typeof(Material), false);

            EditorGUILayout.Space();
            DrawCustomFieldsList(styleData.customFields, currentConfig, () => styleData.customFields.Add(new CustomFieldData("NewField", FieldType.String)));

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawBatchImportSection()
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("批量导入", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            batchImportTarget = (BatchImportTarget)EditorGUILayout.EnumPopup("导入目标", batchImportTarget);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("命名规则配置:", EditorStyles.boldLabel);
            batchImportNamePrefix = EditorGUILayout.TextField("名称前缀", batchImportNamePrefix);
            batchImportNameSuffix = EditorGUILayout.TextField("名称后缀", batchImportNameSuffix);
            EditorGUILayout.Space();

            batchImportUseHierarchy = EditorGUILayout.Toggle("使用层级关系", batchImportUseHierarchy);
            if (batchImportUseHierarchy)
            {
                batchImportParentName = EditorGUILayout.TextField("父级名称", batchImportParentName);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("从场景批量导入", GUILayout.Height(40)))
            {
                BatchImportFromScene();
            }

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("使用说明:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("1. 设置名称前缀/后缀来过滤场景中的物体");
            EditorGUILayout.LabelField("2. 如果使用层级关系，将根据父级名称查找");
            EditorGUILayout.LabelField("3. 点击导入按钮开始批量导入");
            EditorGUILayout.LabelField("4. 导入的物体会根据命名规则自动识别类型");
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("生成代码预览", GUILayout.Height(30)))
            {
                GenerateCodePreview();
            }

            if (GUILayout.Button("生成并添加到场景", GUILayout.Height(30)))
            {
                GenerateAndAddToScene();
            }

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(generatedCodePreview))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("代码预览:", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(generatedCodePreview, GUILayout.Height(200));
            }

            EditorGUILayout.EndVertical();
        }

        private void BatchImportFromScene()
        {
            if (currentConfig == null)
            {
                EditorUtility.DisplayDialog("错误", "请先选择配置文件", "确定");
                return;
            }

            Scene currentScene = SceneManager.GetActiveScene();
            if (!currentScene.isLoaded)
            {
                EditorUtility.DisplayDialog("错误", "请先打开一个场景", "确定");
                return;
            }

            GameObject[] rootObjects = currentScene.GetRootGameObjects();
            List<GameObject> importedObjects = new List<GameObject>();

            string fullPrefix = batchImportParentName + "/" + batchImportNamePrefix;
            if (!batchImportUseHierarchy)
            {
                fullPrefix = batchImportNamePrefix;
            }

            foreach (GameObject root in rootObjects)
            {
                if (batchImportUseHierarchy && !string.IsNullOrEmpty(batchImportParentName))
                {
                    if (root.name == batchImportParentName)
                    {
                        importedObjects.AddRange(GetChildObjects(root, fullPrefix));
                    }
                }
                else
                {
                    if (MatchesNameFilter(root.name))
                    {
                        importedObjects.Add(root);
                    }
                    importedObjects.AddRange(GetChildObjects(root, fullPrefix));
                }
            }

            foreach (GameObject obj in importedObjects)
            {
                if (batchImportTarget == BatchImportTarget.SingleSceneObjects)
                {
                    currentConfig.singleSceneObjects.Add(new SceneObjectData(obj, GenerateObjectId(obj)));
                }
                else if (batchImportTarget == BatchImportTarget.SceneObjectList1)
                {
                    if (currentConfig.sceneObjectLists.Count == 0)
                    {
                        currentConfig.sceneObjectLists.Add(new SceneObjectListData("ImportedObjects", "批量导入的物体"));
                    }
                    currentConfig.sceneObjectLists[0].sceneObjects.Add(new SceneObjectData(obj, GenerateObjectId(obj)));
                }
            }

            EditorUtility.SetDirty(currentConfig);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayDialog("批量导入完成", $"成功导入 {importedObjects.Count} 个物体", "确定");
        }

        private List<GameObject> GetChildObjects(GameObject parent, string prefix)
        {
            List<GameObject> children = new List<GameObject>();
            foreach (Transform child in parent.transform)
            {
                if (MatchesNameFilter(child.name))
                {
                    children.Add(child.gameObject);
                }
                children.AddRange(GetChildObjects(child.gameObject, prefix));
            }
            return children;
        }

        private bool MatchesNameFilter(string objectName)
        {
            bool prefixMatch = string.IsNullOrEmpty(batchImportNamePrefix) || objectName.StartsWith(batchImportNamePrefix);
            bool suffixMatch = string.IsNullOrEmpty(batchImportNameSuffix) || objectName.EndsWith(batchImportNameSuffix);
            return prefixMatch && suffixMatch;
        }

        private string GenerateObjectId(GameObject obj)
        {
            return $"{obj.name}_{obj.GetInstanceID()}";
        }

        private string EscapeStringLiteral(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private void AppendCustomFieldAssignment(StringBuilder sb, string targetExpression, CustomFieldData field)
        {
            string safeFieldName = GetSafeFieldName(field.fieldName);
            string sourceFieldName = EscapeStringLiteral(field.fieldName);
            string valueProperty = field.fieldType == FieldType.String ? "stringValue" : field.fieldType == FieldType.Int ? "intValue" : "boolValue";
            sb.AppendLine($"                var {safeFieldName}Field = source.customFields.Find(item => item.fieldName == \"{sourceFieldName}\");");
            sb.AppendLine($"                if ({safeFieldName}Field != null)");
            sb.AppendLine("                {");
            sb.AppendLine($"                    {targetExpression}.{safeFieldName} = {safeFieldName}Field.{valueProperty};");
            sb.AppendLine("                }");
        }

        private void GenerateCodePreview(string className = null)
        {
            if (currentConfig == null)
            {
                EditorUtility.DisplayDialog("错误", "请先选择配置文件", "确定");
                return;
            }

            // 如果没有提供类名，使用项目名称作为默认
            if (string.IsNullOrEmpty(className))
            {
                className = GetSafeClassName(currentConfig.projectName) + "RuntimeManager";
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("/// <summary>");
            sb.AppendLine($"/// 数字孪生运行时管理器 - 自动生成");
            sb.AppendLine($"/// 项目: {currentConfig.projectName}");
            sb.AppendLine($"/// 版本: {currentConfig.version}");
            sb.AppendLine("/// </summary>");
            sb.AppendLine();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using ConfigTool;");
            sb.AppendLine();
            sb.AppendLine("namespace ConfigTool.Generated");
            sb.AppendLine("{");
            sb.AppendLine($"    public class {className} : MonoBehaviour");
            sb.AppendLine("    {");

            if (true)
            {
                sb.AppendLine("        [System.Serializable]");
                sb.AppendLine("        public class CameraPoint");
                sb.AppendLine("        {");
                sb.AppendLine("            public string pointName;");
                sb.AppendLine("            public Vector3 position;");
                sb.AppendLine("            public Vector3 targetPosition;");
                foreach (var field in currentConfig.singleCameraPoints.Count > 0 ? currentConfig.singleCameraPoints[0].customFields : new List<CustomFieldData>())
                {
                    string typeName = field.fieldType == FieldType.String ? "string" : (field.fieldType == FieldType.Int ? "int" : "bool");
                    sb.AppendLine($"            public {typeName} {GetSafeFieldName(field.fieldName)};");
                }
                sb.AppendLine("        }");
                sb.AppendLine();
                sb.AppendLine($"        public List<CameraPoint> cameraPoints = new List<CameraPoint>();");
            }

            foreach (var list in currentConfig.cameraPointLists)
            {
                string listClassName = GetSafeClassName(list.listName);
                sb.AppendLine("        [System.Serializable]");
                sb.AppendLine($"        public class {listClassName}");
                sb.AppendLine("        {");
                if (true)
                {
                    sb.AppendLine("            [System.Serializable]");
                    sb.AppendLine("            public class Point");
                    sb.AppendLine("            {");
                    sb.AppendLine("                public string pointName;");
                    sb.AppendLine("                public Vector3 position;");
                    sb.AppendLine("                public Vector3 targetPosition;");
                    foreach (var field in list.cameraPoints.Count > 0 ? list.cameraPoints[0].customFields : new List<CustomFieldData>())
                    {
                        string typeName = field.fieldType == FieldType.String ? "string" : (field.fieldType == FieldType.Int ? "int" : "bool");
                        sb.AppendLine($"                public {typeName} {GetSafeFieldName(field.fieldName)};");
                    }
                    sb.AppendLine("            }");
                }
                sb.AppendLine($"            public List<Point> points = new List<Point>();");
                sb.AppendLine("        }");
                sb.AppendLine();
                sb.AppendLine($"        public List<{listClassName}> {GetSafeFieldName(list.listName)} = new List<{listClassName}>();");
            }

            if (true)
            {
                sb.AppendLine("        [System.Serializable]");
                sb.AppendLine("        public class SceneObject");
                sb.AppendLine("        {");
                sb.AppendLine("            public string objectName;");
                sb.AppendLine("            public string objectId;");
                sb.AppendLine("            public GameObject referenceObject;");
                foreach (var field in currentConfig.singleSceneObjects.Count > 0 ? currentConfig.singleSceneObjects[0].customFields : new List<CustomFieldData>())
                {
                    string typeName = field.fieldType == FieldType.String ? "string" : (field.fieldType == FieldType.Int ? "int" : "bool");
                    sb.AppendLine($"            public {typeName} {GetSafeFieldName(field.fieldName)};");
                }
                sb.AppendLine("        }");
                sb.AppendLine();
                sb.AppendLine($"        public List<SceneObject> sceneObjects = new List<SceneObject>();");
            }

            foreach (var list in currentConfig.sceneObjectLists)
            {
                string listClassName = GetSafeClassName(list.listName);
                sb.AppendLine("        [System.Serializable]");
                sb.AppendLine($"        public class {listClassName}");
                sb.AppendLine("        {");
                if (true)
                {
                    sb.AppendLine("            [System.Serializable]");
                    sb.AppendLine("            public class Object");
                    sb.AppendLine("            {");
                    sb.AppendLine("                public string objectName;");
                    sb.AppendLine("                public string objectId;");
                    sb.AppendLine("                public GameObject referenceObject;");
                    foreach (var field in list.sceneObjects.Count > 0 ? list.sceneObjects[0].customFields : new List<CustomFieldData>())
                    {
                        string typeName = field.fieldType == FieldType.String ? "string" : (field.fieldType == FieldType.Int ? "int" : "bool");
                        sb.AppendLine($"                public {typeName} {GetSafeFieldName(field.fieldName)};");
                    }
                    sb.AppendLine("            }");
                }
                sb.AppendLine($"            public List<Object> objects = new List<Object>();");
                sb.AppendLine("        }");
                sb.AppendLine();
                sb.AppendLine($"        public List<{listClassName}> {GetSafeFieldName(list.listName)} = new List<{listClassName}>();");
            }

            sb.AppendLine();
            sb.AppendLine("        private void Awake()");
            sb.AppendLine("        {");
            sb.AppendLine("            InitializeFromConfig();");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public void ApplyGeneratedConfig(GeneratedConfigBuffer buffer)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (buffer == null)");
            sb.AppendLine("            {");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            cameraPoints.Clear();");
            sb.AppendLine("            foreach (var source in buffer.singleCameraPoints)");
            sb.AppendLine("            {");
            sb.AppendLine("                var item = new CameraPoint();");
            sb.AppendLine("                item.pointName = source.pointName;");
            sb.AppendLine("                item.position = source.position;");
            sb.AppendLine("                item.targetPosition = source.targetPosition;");
            foreach (var field in currentConfig.singleCameraPoints.Count > 0 ? currentConfig.singleCameraPoints[0].customFields : new List<CustomFieldData>())
            {
                AppendCustomFieldAssignment(sb, "                item", field);
            }
            sb.AppendLine("                cameraPoints.Add(item);");
            sb.AppendLine("            }");
            sb.AppendLine();
            foreach (var list in currentConfig.cameraPointLists)
            {
                string listClassName = GetSafeClassName(list.listName);
                string fieldName = GetSafeFieldName(list.listName);
                sb.AppendLine($"            {fieldName}.Clear();");
                sb.AppendLine("            {");
                sb.AppendLine($"                var generatedList = new {listClassName}();");
                sb.AppendLine($"                var sourceList = buffer.cameraPointLists.Find(item => item.listName == \"{EscapeStringLiteral(list.listName)}\");");
                sb.AppendLine("                if (sourceList != null)");
                sb.AppendLine("                {");
                sb.AppendLine("                    foreach (var source in sourceList.cameraPoints)");
                sb.AppendLine("                    {");
                sb.AppendLine($"                        var item = new {listClassName}.Point();");
                sb.AppendLine("                        item.pointName = source.pointName;");
                sb.AppendLine("                        item.position = source.position;");
                sb.AppendLine("                        item.targetPosition = source.targetPosition;");
                foreach (var field in list.cameraPoints.Count > 0 ? list.cameraPoints[0].customFields : new List<CustomFieldData>())
                {
                    AppendCustomFieldAssignment(sb, "                        item", field);
                }
                sb.AppendLine("                        generatedList.points.Add(item);");
                sb.AppendLine("                    }");
                sb.AppendLine("                }");
                sb.AppendLine($"                {fieldName}.Add(generatedList);");
                sb.AppendLine("            }");
                sb.AppendLine();
            }
            sb.AppendLine("            sceneObjects.Clear();");
            sb.AppendLine("            foreach (var source in buffer.singleSceneObjects)");
            sb.AppendLine("            {");
            sb.AppendLine("                var item = new SceneObject();");
            sb.AppendLine("                item.objectName = source.objectName;");
            sb.AppendLine("                item.objectId = source.objectId;");
            sb.AppendLine("                item.referenceObject = source.referenceObject;");
            foreach (var field in currentConfig.singleSceneObjects.Count > 0 ? currentConfig.singleSceneObjects[0].customFields : new List<CustomFieldData>())
            {
                AppendCustomFieldAssignment(sb, "                item", field);
            }
            sb.AppendLine("                sceneObjects.Add(item);");
            sb.AppendLine("            }");
            sb.AppendLine();
            foreach (var list in currentConfig.sceneObjectLists)
            {
                string listClassName = GetSafeClassName(list.listName);
                string fieldName = GetSafeFieldName(list.listName);
                sb.AppendLine($"            {fieldName}.Clear();");
                sb.AppendLine("            {");
                sb.AppendLine($"                var generatedList = new {listClassName}();");
                sb.AppendLine($"                var sourceList = buffer.sceneObjectLists.Find(item => item.listName == \"{EscapeStringLiteral(list.listName)}\");");
                sb.AppendLine("                if (sourceList != null)");
                sb.AppendLine("                {");
                sb.AppendLine("                    foreach (var source in sourceList.sceneObjects)");
                sb.AppendLine("                    {");
                sb.AppendLine($"                        var item = new {listClassName}.Object();");
                sb.AppendLine("                        item.objectName = source.objectName;");
                sb.AppendLine("                        item.objectId = source.objectId;");
                sb.AppendLine("                        item.referenceObject = source.referenceObject;");
                foreach (var field in list.sceneObjects.Count > 0 ? list.sceneObjects[0].customFields : new List<CustomFieldData>())
                {
                    AppendCustomFieldAssignment(sb, "                        item", field);
                }
                sb.AppendLine("                        generatedList.objects.Add(item);");
                sb.AppendLine("                    }");
                sb.AppendLine("                }");
                sb.AppendLine($"                {fieldName}.Add(generatedList);");
                sb.AppendLine("            }");
                sb.AppendLine();
            }
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        private void InitializeFromConfig()");
            sb.AppendLine("        {");
            sb.AppendLine("            // TODO: 从配置文件初始化数据");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            generatedCodePreview = sb.ToString();
        }

        private void GenerateAndAddToScene()
        {
            if (currentConfig == null)
            {
                EditorUtility.DisplayDialog("错误", "请先选择配置文件", "确定");
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(currentConfig);
            string assetName = Path.GetFileNameWithoutExtension(assetPath);
            string className = GetSafeClassName(assetName);

            string savePath = EditorUtility.SaveFilePanelInProject(
                "保存生成的脚本",
                className,
                "cs",
                "请选择脚本保存位置"
            );

            if (string.IsNullOrEmpty(savePath))
            {
                return;
            }

            GenerateCodePreview(className);

            string directory = Path.GetDirectoryName(savePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string pendingObjectName = "Temp_" + className;
            GameObject tempObj = new GameObject(pendingObjectName);
            GeneratedConfigBuffer buffer = tempObj.AddComponent<GeneratedConfigBuffer>();
            buffer.Capture(currentConfig);
            Undo.RegisterCreatedObjectUndo(tempObj, "Create generated config manager");
            EditorUtility.SetDirty(buffer);
            EditorUtility.SetDirty(tempObj);
            EditorSceneManager.MarkSceneDirty(tempObj.scene);

            SessionState.SetBool(PendingGenerateSavePathKey + ".Active", true);
            SessionState.SetString(PendingGenerateSavePathKey, savePath);
            SessionState.SetString(PendingGenerateConfigPathKey, assetPath);
            SessionState.SetString(PendingGenerateClassNameKey, className);
            SessionState.SetString(PendingGenerateObjectNameKey, pendingObjectName);
            SessionState.SetInt(PendingGenerateRetryCountKey, 0);

            File.WriteAllText(savePath, generatedCodePreview);
            AssetDatabase.ImportAsset(savePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();

            // 创建临时物体，保存场景对象引用
            string tempObjectName = "Temp_" + className;
            GameObject legacyTempObj = GameObject.Find(tempObjectName);
            if (legacyTempObj != null && legacyTempObj.GetComponent<GeneratedConfigBuffer>() == null)
            {
                legacyTempObj.AddComponent<GeneratedConfigBuffer>().Capture(currentConfig);
            }

            SessionState.SetBool(PendingGenerateSavePathKey + ".Active", true);
            SessionState.SetString(PendingGenerateSavePathKey, savePath);
            SessionState.SetString(PendingGenerateConfigPathKey, assetPath);
            SessionState.SetString(PendingGenerateClassNameKey, className);
            SessionState.SetString(PendingGenerateObjectNameKey, pendingObjectName);
            SessionState.SetInt(PendingGenerateRetryCountKey, 0);

            // 等待编译完成
            EditorApplication.CallbackFunction callback = null;
            callback = () =>
            {
                if (!EditorApplication.isCompiling)
                {
                    EditorApplication.update -= callback;
                    if (SessionState.GetBool(PendingGenerateSavePathKey + ".Active", false))
                    {
                        EditorApplication.delayCall += CompletePendingGenerate;
                        return;
                    }

                    GameObject managerObj = GameObject.Find(tempObjectName);
                    if (managerObj != null)
                    {
                        managerObj.name = className;

                        // 尝试添加组件
                        Component manager = null;

                        // 方法1: 直接通过字符串添加
                        try
                        {
                            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(savePath);
                            Type scriptType = script != null ? script.GetClass() : null;
                            if (scriptType != null && typeof(Component).IsAssignableFrom(scriptType))
                            {
                                manager = managerObj.AddComponent(scriptType);
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"方法1失败: {ex.Message}");
                        }

                        // 方法2: 通过类型加载
                        if (manager == null)
                        {
                            try
                            {
                                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(savePath);
                                if (script != null)
                                {
                                    System.Type scriptType = script.GetClass();
                                    if (scriptType != null)
                                    {
                                        manager = managerObj.AddComponent(scriptType);
                                    }
                                }
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogWarning($"方法2失败: {ex.Message}");
                            }
                        }

                        if (manager == null)
                        {
                            EditorUtility.DisplayDialog("警告", "脚本已生成，但组件未能自动添加到物体上。\n请手动将生成的脚本组件添加到物体上。", "确定");
                        }
                        else
                        {
                            Selection.activeGameObject = managerObj;
                            EditorGUIUtility.PingObject(managerObj);
                            EditorUtility.DisplayDialog("成功", $"运行时管理器已生成并添加到场景中\n脚本保存在: {savePath}", "确定");
                        }
                    }
                }
            };

            EditorApplication.update += callback;
        }

        private static void CompletePendingGenerate()
        {
            if (!SessionState.GetBool(PendingGenerateSavePathKey + ".Active", false))
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += CompletePendingGenerate;
                return;
            }

            string savePath = SessionState.GetString(PendingGenerateSavePathKey, "");
            string className = SessionState.GetString(PendingGenerateClassNameKey, "");
            string objectName = SessionState.GetString(PendingGenerateObjectNameKey, "");

            GameObject managerObj = GameObject.Find(objectName);
            if (managerObj == null)
            {
                ClearPendingGenerate();
                EditorUtility.DisplayDialog("警告", "脚本已生成，但未找到临时场景对象。请手动将生成脚本添加到物体上。", "确定");
                return;
            }

            managerObj.name = className;
            GeneratedConfigBuffer buffer = managerObj.GetComponent<GeneratedConfigBuffer>();
            Component manager = AddGeneratedComponent(managerObj, savePath);

            if (manager == null)
            {
                int retryCount = SessionState.GetInt(PendingGenerateRetryCountKey, 0) + 1;
                SessionState.SetInt(PendingGenerateRetryCountKey, retryCount);
                if (retryCount >= PendingGenerateMaxRetries)
                {
                    ClearPendingGenerate();
                    EditorUtility.DisplayDialog("警告", "脚本已生成，但 Unity 未能解析生成的组件类型。请检查 Console 是否有编译错误，然后手动将生成脚本添加到物体上。", "确定");
                    return;
                }

                EditorApplication.delayCall += CompletePendingGenerate;
                return;
            }

            ApplyGeneratedConfig(manager, buffer);

            if (buffer != null)
            {
                DestroyImmediate(buffer);
            }

            EditorSceneManager.MarkSceneDirty(managerObj.scene);
            Selection.activeGameObject = managerObj;
            EditorGUIUtility.PingObject(managerObj);
            ClearPendingGenerate();
            EditorUtility.DisplayDialog("成功", $"运行时管理器已生成并添加到场景中\n脚本保存位置: {savePath}", "确定");
        }

        private static Component AddGeneratedComponent(GameObject managerObj, string savePath)
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(savePath);
            Type scriptType = script != null ? script.GetClass() : null;
            if (scriptType == null || !typeof(Component).IsAssignableFrom(scriptType))
            {
                return null;
            }

            Component existing = managerObj.GetComponent(scriptType);
            return existing != null ? existing : managerObj.AddComponent(scriptType);
        }

        private static void ApplyGeneratedConfig(Component manager, GeneratedConfigBuffer buffer)
        {
            if (manager == null || buffer == null)
            {
                return;
            }

            Type managerType = manager.GetType();
            System.Reflection.MethodInfo applyMethod = managerType.GetMethod("ApplyGeneratedConfig", new[] { typeof(GeneratedConfigBuffer) });
            if (applyMethod != null)
            {
                applyMethod.Invoke(manager, new object[] { buffer });
                EditorUtility.SetDirty(manager);
            }
        }

        private static void ClearPendingGenerate()
        {
            SessionState.EraseBool(PendingGenerateSavePathKey + ".Active");
            SessionState.EraseString(PendingGenerateSavePathKey);
            SessionState.EraseString(PendingGenerateConfigPathKey);
            SessionState.EraseString(PendingGenerateClassNameKey);
            SessionState.EraseString(PendingGenerateObjectNameKey);
            SessionState.EraseInt(PendingGenerateRetryCountKey);
        }

        private string GetSafeClassName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "ConfigTool";

            StringBuilder sb = new StringBuilder();
            bool nextUpper = true;

            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (nextUpper)
                    {
                        sb.Append(char.ToUpper(c));
                        nextUpper = false;
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                else
                {
                    nextUpper = true;
                }
            }

            string result = sb.ToString();
            if (string.IsNullOrEmpty(result))
            {
                return "ConfigTool";
            }

            if (char.IsDigit(result[0]))
            {
                result = "_" + result;
            }

            return result;
        }

        private string GetSafeFieldName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "field";

            StringBuilder sb = new StringBuilder();
            bool first = true;

            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (first)
                    {
                        sb.Append(char.ToLower(c));
                        first = false;
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }

            string result = sb.ToString();
            if (string.IsNullOrEmpty(result))
            {
                return "field";
            }

            if (char.IsDigit(result[0]))
            {
                result = "_" + result;
            }

            return result;
        }

        private enum BatchImportTarget
        {
            SingleSceneObjects,
            SceneObjectList1,
            SceneObjectList2
        }
    }
}
