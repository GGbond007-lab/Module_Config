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
        private readonly List<ToolbarPage> toolbarPages = new List<ToolbarPage>();
        private int toolbarIndex = 0;
        private bool showSingleCameraPoints = true;
        private bool showCameraPointLists = true;
        private bool showSingleSceneObjects = true;
        private bool showSceneObjectLists = true;
        private bool showStyleConfig = true;
        private readonly Dictionary<CameraPointListData, bool> cameraPointListFoldouts = new Dictionary<CameraPointListData, bool>();
        private readonly Dictionary<SceneObjectListData, bool> sceneObjectListFoldouts = new Dictionary<SceneObjectListData, bool>();
        private readonly Dictionary<CustomModelData, bool> customModelFoldouts = new Dictionary<CustomModelData, bool>();
        private Vector2 modelTypeScrollPosition;
        private List<Type> serializableModelTypes;
        private string modelTypeSearch = "";
        private string serializableModelFolderPath = "Assets/DataModel";

        private string batchImportParentName = "";
        private string batchImportNamePrefix = "";
        private string batchImportNameSuffix = "";
        private bool batchImportUseHierarchy = true;
        private BatchImportTarget batchImportTarget = BatchImportTarget.SingleCameraPoints;
        private int batchImportCameraPointListIndex = 0;
        private int batchImportSceneObjectListIndex = 0;

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
            var window = GetWindow<ConfigToolEditor>("配置编辑器");
            window.minSize = new Vector2(800, 600);
        }

        [MenuItem("ConfigSetting/创建新配置")]
        public static void CreateNewConfiguration()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "创建新配置",
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

        [MenuItem("ConfigSetting/生成配置")]
        public static void GenerateRuntimeManager()
        {
            if (!Selection.activeObject)
            {
                EditorUtility.DisplayDialog("错误", "请先选择一个配置文件", "确定");
                return;
            }

            if (!(Selection.activeObject is ConfigToolData))
            {
                EditorUtility.DisplayDialog("错误", "请选择一个配置文件", "确定");
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
                BuildToolbarPages();
                toolbarIndex = Mathf.Clamp(toolbarIndex, 0, toolbarPages.Count - 1);
                DrawToolbarPage(toolbarPages[toolbarIndex]);
            }

            EditorGUILayout.EndScrollView();
            DrawFooter();
            EditorGUILayout.EndVertical();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("配置编辑器", EditorStyles.boldLabel);
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
            BuildToolbarPages();
            string[] pageNames = toolbarPages.Select(page => page.title).ToArray();
            toolbarIndex = Mathf.Clamp(toolbarIndex, 0, pageNames.Length - 1);
            toolbarIndex = GUILayout.Toolbar(toolbarIndex, pageNames);
            EditorGUILayout.Space();
        }

        private void BuildToolbarPages()
        {
            toolbarPages.Clear();
            toolbarPages.Add(new ToolbarPage("相机点位", ToolbarPageType.CameraPoints));
            toolbarPages.Add(new ToolbarPage("场景物体", ToolbarPageType.SceneObjects));
            toolbarPages.Add(new ToolbarPage("风格配置", ToolbarPageType.Style));
            toolbarPages.Add(new ToolbarPage("自定义配置", ToolbarPageType.CustomConfigRoot));

            if (currentConfig != null)
            {
                foreach (var config in currentConfig.singleCustomConfigs)
                {
                    toolbarPages.Add(new ToolbarPage(string.IsNullOrEmpty(config.configName) ? "未命名配置" : config.configName, ToolbarPageType.SingleCustomConfig, config, null));
                }

                foreach (var list in currentConfig.customConfigLists)
                {
                    toolbarPages.Add(new ToolbarPage(string.IsNullOrEmpty(list.listName) ? "未命名列表" : list.listName, ToolbarPageType.CustomConfigList, null, list));
                }
            }

            toolbarPages.Add(new ToolbarPage("批量导入", ToolbarPageType.BatchImport));
        }

        private void DrawToolbarPage(ToolbarPage page)
        {
            switch (page.pageType)
            {
                case ToolbarPageType.CameraPoints:
                    DrawCameraPointsSection();
                    break;
                case ToolbarPageType.SceneObjects:
                    DrawSceneObjectsSection();
                    break;
                case ToolbarPageType.Style:
                    DrawStyleSection();
                    break;
                case ToolbarPageType.CustomConfigRoot:
                    DrawCustomConfigSection();
                    break;
                case ToolbarPageType.SingleCustomConfig:
                    DrawSingleCustomConfigPage(page.singleCustomConfig);
                    break;
                case ToolbarPageType.CustomConfigList:
                    DrawCustomConfigListPage(page.customConfigList);
                    break;
                case ToolbarPageType.BatchImport:
                    DrawBatchImportSection();
                    break;
            }
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

        private void DrawSingleCameraPoint(CameraPointData point, int index, Action removeAction = null)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            point.pointName = EditorGUILayout.TextField("名称", point.pointName);
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                if (removeAction != null)
                {
                    removeAction();
                }
                else
                {
                    currentConfig.singleCameraPoints.RemoveAt(index);
                }
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
            bool isExpanded = GetCameraPointListFoldout(list);
            isExpanded = EditorGUILayout.Foldout(isExpanded, "", true);
            SetCameraPointListFoldout(list, isExpanded);
            list.listName = EditorGUILayout.TextField("列表名称", list.listName);
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                currentConfig.cameraPointLists.RemoveAt(index);
                cameraPointListFoldouts.Remove(list);
                EditorUtility.SetDirty(currentConfig);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            if (!isExpanded)
            {
                EditorGUILayout.LabelField($"点位数量: {list.cameraPoints.Count}");
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
                return;
            }

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
                int pointIndex = j;
                DrawSingleCameraPoint(list.cameraPoints[j], pointIndex, () => list.cameraPoints.RemoveAt(pointIndex));
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

        private void DrawSingleSceneObject(SceneObjectData objData, int index, Action removeAction = null)
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
                if (removeAction != null)
                {
                    removeAction();
                }
                else
                {
                    currentConfig.singleSceneObjects.RemoveAt(index);
                }
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
            bool isExpanded = GetSceneObjectListFoldout(list);
            isExpanded = EditorGUILayout.Foldout(isExpanded, "", true);
            SetSceneObjectListFoldout(list, isExpanded);
            list.listName = EditorGUILayout.TextField("列表名称", list.listName);
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                currentConfig.sceneObjectLists.RemoveAt(index);
                sceneObjectListFoldouts.Remove(list);
                EditorUtility.SetDirty(currentConfig);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            if (!isExpanded)
            {
                EditorGUILayout.LabelField($"物体数量: {list.sceneObjects.Count}");
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
                return;
            }

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
                int objectIndex = j;
                DrawSingleSceneObject(list.sceneObjects[j], objectIndex, () => list.sceneObjects.RemoveAt(objectIndex));
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

        private bool GetCameraPointListFoldout(CameraPointListData list)
        {
            if (!cameraPointListFoldouts.TryGetValue(list, out bool isExpanded))
            {
                isExpanded = true;
                cameraPointListFoldouts[list] = isExpanded;
            }

            return isExpanded;
        }

        private void SetCameraPointListFoldout(CameraPointListData list, bool isExpanded)
        {
            cameraPointListFoldouts[list] = isExpanded;
        }

        private bool GetSceneObjectListFoldout(SceneObjectListData list)
        {
            if (!sceneObjectListFoldouts.TryGetValue(list, out bool isExpanded))
            {
                isExpanded = true;
                sceneObjectListFoldouts[list] = isExpanded;
            }

            return isExpanded;
        }

        private void SetSceneObjectListFoldout(SceneObjectListData list, bool isExpanded)
        {
            sceneObjectListFoldouts[list] = isExpanded;
        }

        private void DrawCustomFieldsList(List<CustomFieldData> fields, ConfigToolData config, Action addAction, bool valuesOnly = false)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(valuesOnly ? "字段值:" : "自定义字段:");
            if (!valuesOnly && addAction != null && GUILayout.Button("+ 添加字段", GUILayout.Width(80)))
            {
                addAction();
                EditorUtility.SetDirty(config);
            }
            EditorGUILayout.EndHorizontal();

            for (int i = fields.Count - 1; i >= 0; i--)
            {
                var field = fields[i];
                EditorGUILayout.BeginHorizontal("box");
                using (new EditorGUI.DisabledScope(valuesOnly))
                {
                    field.fieldName = EditorGUILayout.TextField(field.fieldName, GUILayout.Width(120));
                    FieldType newFieldType = (FieldType)EditorGUILayout.EnumPopup(field.fieldType, GUILayout.Width(100));
                    if (newFieldType != field.fieldType)
                    {
                        field.fieldType = newFieldType;
                        if (field.fieldType == FieldType.Model)
                        {
                            field.modelTypeName = DrawDefaultModelName();
                            field.modelValue = CreateModelInstance(field.modelTypeName);
                        }
                    }
                }

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
                else if (field.fieldType == FieldType.Vector3)
                {
                    field.vector3Value = EditorGUILayout.Vector3Field("", field.vector3Value);
                }
                else if (field.fieldType == FieldType.GameObject)
                {
                    field.gameObjectValue = (GameObject)EditorGUILayout.ObjectField(field.gameObjectValue, typeof(GameObject), true);
                }
                else if (field.fieldType == FieldType.Model)
                {
                    string selectedModel = DrawModelSelector("", field.modelTypeName);
                    if (selectedModel != field.modelTypeName)
                    {
                        field.modelTypeName = selectedModel;
                        field.modelValue = CreateModelInstance(field.modelTypeName);
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUI.indentLevel++;
                    DrawModelInstanceFields(field.modelValue, field.modelTypeName);
                    EditorGUI.indentLevel--;
                    if (!valuesOnly && GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        fields.RemoveAt(i);
                        EditorUtility.SetDirty(config);
                        return;
                    }
                    continue;
                }

                if (!valuesOnly && GUILayout.Button("X", GUILayout.Width(25)))
                {
                    fields.RemoveAt(i);
                    EditorUtility.SetDirty(config);
                    EditorGUILayout.EndHorizontal();
                    return;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawCustomConfigSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("自定义配置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("先定义 Model，再基于 Model 添加单个配置或配置列表。Model 也可以作为字段类型嵌套在其他 Model 中。", MessageType.Info);

            DrawCustomModelDefinitionsSection();
            EditorGUILayout.Space();
            DrawSingleCustomConfigsSection();
            EditorGUILayout.Space();
            DrawCustomConfigListsSection();
            EditorGUILayout.Space();
            DrawSerializableModelImportSection();
            EditorGUILayout.EndVertical();
        }

        private void DrawCustomModelDefinitionsSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Model 定义: {currentConfig.customModels.Count}", EditorStyles.boldLabel);
            if (GUILayout.Button("添加 Model", GUILayout.Width(100)))
            {
                currentConfig.customModels.Add(new CustomModelData());
                EditorUtility.SetDirty(currentConfig);
            }
            EditorGUILayout.EndHorizontal();

            for (int i = currentConfig.customModels.Count - 1; i >= 0; i--)
            {
                DrawCustomModel(currentConfig.customModels[i], i);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSingleCustomConfigsSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"单个配置: {currentConfig.singleCustomConfigs.Count}", EditorStyles.boldLabel);
            if (GUILayout.Button("添加配置", GUILayout.Width(100)))
            {
                var config = new CustomConfigData();
                ApplyModelTemplate(config);
                currentConfig.singleCustomConfigs.Add(config);
                EditorUtility.SetDirty(currentConfig);
            }
            EditorGUILayout.EndHorizontal();

            for (int i = currentConfig.singleCustomConfigs.Count - 1; i >= 0; i--)
            {
                DrawCustomConfig(currentConfig.singleCustomConfigs[i], () => currentConfig.singleCustomConfigs.RemoveAt(i));
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawCustomConfigListsSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"配置列表: {currentConfig.customConfigLists.Count}", EditorStyles.boldLabel);
            if (GUILayout.Button("添加列表", GUILayout.Width(100)))
            {
                currentConfig.customConfigLists.Add(new CustomConfigListData());
                EditorUtility.SetDirty(currentConfig);
            }
            EditorGUILayout.EndHorizontal();

            for (int i = currentConfig.customConfigLists.Count - 1; i >= 0; i--)
            {
                DrawCustomConfigList(currentConfig.customConfigLists[i], i);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawCustomModel(CustomModelData model, int index)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            bool isExpanded = GetCustomModelFoldout(model);
            isExpanded = EditorGUILayout.Foldout(isExpanded, "", true);
            SetCustomModelFoldout(model, isExpanded);
            model.modelName = EditorGUILayout.TextField("Model 名称", model.modelName);
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                currentConfig.customModels.RemoveAt(index);
                customModelFoldouts.Remove(model);
                EditorUtility.SetDirty(currentConfig);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            if (!isExpanded)
            {
                EditorGUILayout.LabelField($"字段数量: {model.fields.Count}");
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("来源类型", model.sourceTypeName);
            }

            DrawCustomFieldsList(model.fields, currentConfig, () => model.fields.Add(new CustomFieldData("NewField", FieldType.String)));
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawSingleCustomConfigPage(CustomConfigData config)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(config == null ? "自定义配置" : config.configName, EditorStyles.boldLabel);
            if (config == null)
            {
                EditorGUILayout.HelpBox("配置不存在，可能已被删除。", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            DrawCustomConfig(config, null);
            EditorGUILayout.EndVertical();
        }

        private void DrawCustomConfigListPage(CustomConfigListData list)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(list == null ? "自定义配置列表" : list.listName, EditorStyles.boldLabel);
            if (list == null)
            {
                EditorGUILayout.HelpBox("配置列表不存在，可能已被删除。", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            DrawCustomConfigList(list, currentConfig.customConfigLists.IndexOf(list));
            EditorGUILayout.EndVertical();
        }

        private void DrawCustomConfig(CustomConfigData config, Action removeAction)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            config.configName = EditorGUILayout.TextField("配置名称", config.configName);
            string selectedModel = DrawModelSelector("Model", config.modelTypeName);
            if (selectedModel != config.modelTypeName)
            {
                config.modelTypeName = selectedModel;
                ApplyModelTemplate(config);
                EditorUtility.SetDirty(currentConfig);
            }
            if (removeAction != null && GUILayout.Button("X", GUILayout.Width(25)))
            {
                removeAction();
                EditorUtility.SetDirty(currentConfig);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            DrawModelInstanceFields(config.value, config.modelTypeName);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawCustomConfigList(CustomConfigListData list, int index)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            list.listName = EditorGUILayout.TextField("列表名称", list.listName);
            string selectedModel = DrawModelSelector("Model", list.modelTypeName);
            if (selectedModel != list.modelTypeName)
            {
                list.modelTypeName = selectedModel;
                foreach (var config in list.configs)
                {
                    config.modelTypeName = selectedModel;
                    ApplyModelTemplate(config);
                }
                EditorUtility.SetDirty(currentConfig);
            }
            if (index >= 0 && GUILayout.Button("X", GUILayout.Width(25)))
            {
                currentConfig.customConfigLists.RemoveAt(index);
                EditorUtility.SetDirty(currentConfig);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            list.listDescription = EditorGUILayout.TextField("描述", list.listDescription);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"配置数量: {list.configs.Count}");
            if (GUILayout.Button("添加配置", GUILayout.Width(100)))
            {
                var config = new CustomConfigData
                {
                    modelTypeName = list.modelTypeName
                };
                ApplyModelTemplate(config);
                list.configs.Add(config);
                EditorUtility.SetDirty(currentConfig);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel++;
            for (int i = 0; i < list.configs.Count; i++)
            {
                int configIndex = i;
                DrawCustomConfig(list.configs[i], () => list.configs.RemoveAt(configIndex));
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private string DrawDefaultModelName()
        {
            return currentConfig.customModels.Select(model => model.modelName).FirstOrDefault(name => !string.IsNullOrEmpty(name)) ?? "";
        }

        private string DrawModelSelector(string label, string selectedModel)
        {
            string[] modelNames = currentConfig.customModels.Select(model => model.modelName).Where(name => !string.IsNullOrEmpty(name)).ToArray();
            if (modelNames.Length == 0)
            {
                EditorGUILayout.LabelField(label, "请先添加 Model");
                return "";
            }

            int selectedIndex = Array.IndexOf(modelNames, selectedModel);
            selectedIndex = Mathf.Max(0, selectedIndex);
            selectedIndex = EditorGUILayout.Popup(label, selectedIndex, modelNames, GUILayout.Width(240));
            return modelNames[selectedIndex];
        }

        private void ApplyModelTemplate(CustomConfigData config)
        {
            CustomModelData model = currentConfig.customModels.FirstOrDefault(item => item.modelName == config.modelTypeName);
            config.value = new CustomModelInstanceData
            {
                fields = model == null ? new List<CustomFieldData>() : CloneFieldTemplates(model.fields)
            };
        }

        private List<CustomFieldData> CloneFieldTemplates(List<CustomFieldData> fields)
        {
            return fields.Select(CloneFieldTemplate).ToList();
        }

        private CustomFieldData CloneFieldTemplate(CustomFieldData field)
        {
            return new CustomFieldData(field.fieldName, field.fieldType)
            {
                modelTypeName = field.modelTypeName,
                modelValue = field.fieldType == FieldType.Model ? CreateModelInstance(field.modelTypeName) : new CustomModelInstanceData()
            };
        }

        private CustomModelInstanceData CreateModelInstance(string modelTypeName)
        {
            CustomModelData model = currentConfig.customModels.FirstOrDefault(item => item.modelName == modelTypeName);
            return new CustomModelInstanceData
            {
                fields = model == null ? new List<CustomFieldData>() : CloneFieldTemplates(model.fields)
            };
        }

        private void DrawModelInstanceFields(CustomModelInstanceData instance, string modelTypeName)
        {
            if (string.IsNullOrEmpty(modelTypeName))
            {
                EditorGUILayout.HelpBox("请选择 Model。", MessageType.Info);
                return;
            }

            if (instance == null)
            {
                return;
            }

            DrawCustomFieldsList(instance.fields, currentConfig, null, true);
        }

        private bool GetCustomModelFoldout(CustomModelData model)
        {
            if (!customModelFoldouts.TryGetValue(model, out bool isExpanded))
            {
                isExpanded = true;
                customModelFoldouts[model] = isExpanded;
            }

            return isExpanded;
        }

        private void SetCustomModelFoldout(CustomModelData model, bool isExpanded)
        {
            customModelFoldouts[model] = isExpanded;
        }

        private void DrawSerializableModelImportSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("从已有序列化 Model 导入", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            serializableModelFolderPath = EditorGUILayout.TextField("扫描路径", serializableModelFolderPath);
            if (GUILayout.Button("选择", GUILayout.Width(60)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("选择 Model 文件夹", "Assets", "");
                if (!string.IsNullOrEmpty(selectedPath) && selectedPath.StartsWith(Application.dataPath))
                {
                    serializableModelFolderPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                    RefreshSerializableModelTypes();
                }
            }
            EditorGUILayout.EndHorizontal();
            modelTypeSearch = EditorGUILayout.TextField("搜索类型", modelTypeSearch);

            if (GUILayout.Button("刷新类型列表", GUILayout.Width(120)) || serializableModelTypes == null)
            {
                RefreshSerializableModelTypes();
            }

            if (serializableModelTypes == null || serializableModelTypes.Count == 0)
            {
                EditorGUILayout.HelpBox($"未在 {serializableModelFolderPath} 下找到包含支持字段的可序列化类型。", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            modelTypeScrollPosition = EditorGUILayout.BeginScrollView(modelTypeScrollPosition, GUILayout.Height(160));
            foreach (Type type in serializableModelTypes)
            {
                if (!string.IsNullOrEmpty(modelTypeSearch) && type.FullName.IndexOf(modelTypeSearch, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(type.FullName);
                if (GUILayout.Button("导入", GUILayout.Width(60)))
                {
                    ImportSerializableModel(type);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void RefreshSerializableModelTypes()
        {
            string normalizedPath = NormalizeAssetFolderPath(serializableModelFolderPath);
            serializableModelFolderPath = normalizedPath;

            if (!AssetDatabase.IsValidFolder(normalizedPath))
            {
                serializableModelTypes = new List<Type>();
                return;
            }

            HashSet<string> scriptTypeNames = AssetDatabase.FindAssets("t:MonoScript", new[] { normalizedPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath<MonoScript>(path))
                .Where(script => script != null && script.GetClass() != null)
                .Select(script => script.GetClass().FullName)
                .ToHashSet();

            serializableModelTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly =>
                {
                    try
                    {
                        return assembly.GetTypes();
                    }
                    catch
                    {
                        return Array.Empty<Type>();
                    }
                })
                .Where(type => scriptTypeNames.Contains(type.FullName))
                .Where(type => type.IsClass && type.IsSerializable && !type.IsAbstract && GetSupportedSerializableFields(type).Count > 0)
                .OrderBy(type => type.FullName)
                .ToList();
        }

        private string NormalizeAssetFolderPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "Assets/DataModel";
            }

            string normalized = path.Replace("\\", "/").TrimEnd('/');
            if (normalized.StartsWith(Application.dataPath))
            {
                normalized = "Assets" + normalized.Substring(Application.dataPath.Length);
            }

            if (!normalized.StartsWith("Assets"))
            {
                normalized = "Assets/" + normalized.TrimStart('/');
            }

            return normalized;
        }

        private void ImportSerializableModel(Type type)
        {
            var model = new CustomModelData
            {
                modelName = type.Name,
                sourceTypeName = type.FullName,
                fields = GetSupportedSerializableFields(type)
            };
            currentConfig.customModels.Add(model);
            customModelFoldouts[model] = true;
            EditorUtility.SetDirty(currentConfig);
        }

        private static List<CustomFieldData> GetSupportedSerializableFields(Type type)
        {
            return type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                .Select(field => CreateFieldFromType(field.Name, field.FieldType))
                .Where(field => field != null)
                .ToList();
        }

        private static CustomFieldData CreateFieldFromType(string fieldName, Type type)
        {
            if (type == typeof(string))
            {
                return new CustomFieldData(fieldName, FieldType.String);
            }
            if (type == typeof(int))
            {
                return new CustomFieldData(fieldName, FieldType.Int);
            }
            if (type == typeof(bool))
            {
                return new CustomFieldData(fieldName, FieldType.Bool);
            }
            if (type == typeof(Vector3))
            {
                return new CustomFieldData(fieldName, FieldType.Vector3);
            }
            if (type == typeof(GameObject))
            {
                return new CustomFieldData(fieldName, FieldType.GameObject);
            }
            if (type.IsClass && type.IsSerializable)
            {
                return new CustomFieldData(fieldName, FieldType.Model)
                {
                    modelTypeName = type.Name
                };
            }

            return null;
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
            DrawBatchImportTargetOptions();
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

        private void DrawBatchImportTargetOptions()
        {
            if (batchImportTarget == BatchImportTarget.CameraPointList)
            {
                DrawBatchImportCameraPointListSelector();
            }
            else if (batchImportTarget == BatchImportTarget.SceneObjectList)
            {
                DrawBatchImportSceneObjectListSelector();
            }
        }

        private void DrawBatchImportCameraPointListSelector()
        {
            if (currentConfig.cameraPointLists.Count == 0)
            {
                EditorGUILayout.HelpBox("当前没有相机点位列表，请先创建一个列表，或点击下方按钮自动创建。", MessageType.Info);
                if (GUILayout.Button("创建相机点位列表", GUILayout.Width(140)))
                {
                    currentConfig.cameraPointLists.Add(new CameraPointListData("ImportedCameraPoints", "批量导入的相机点位"));
                    batchImportCameraPointListIndex = 0;
                    EditorUtility.SetDirty(currentConfig);
                }
                return;
            }

            batchImportCameraPointListIndex = Mathf.Clamp(batchImportCameraPointListIndex, 0, currentConfig.cameraPointLists.Count - 1);
            string[] listNames = currentConfig.cameraPointLists
                .Select((list, index) => string.IsNullOrEmpty(list.listName) ? $"未命名相机点位列表 {index + 1}" : list.listName)
                .ToArray();
            batchImportCameraPointListIndex = EditorGUILayout.Popup("相机点位列表", batchImportCameraPointListIndex, listNames);
        }

        private void DrawBatchImportSceneObjectListSelector()
        {
            if (currentConfig.sceneObjectLists.Count == 0)
            {
                EditorGUILayout.HelpBox("当前没有场景物体列表，请先创建一个列表，或点击下方按钮自动创建。", MessageType.Info);
                if (GUILayout.Button("创建场景物体列表", GUILayout.Width(140)))
                {
                    currentConfig.sceneObjectLists.Add(new SceneObjectListData("ImportedObjects", "批量导入的物体"));
                    batchImportSceneObjectListIndex = 0;
                    EditorUtility.SetDirty(currentConfig);
                }
                return;
            }

            batchImportSceneObjectListIndex = Mathf.Clamp(batchImportSceneObjectListIndex, 0, currentConfig.sceneObjectLists.Count - 1);
            string[] listNames = currentConfig.sceneObjectLists
                .Select((list, index) => string.IsNullOrEmpty(list.listName) ? $"未命名场景物体列表 {index + 1}" : list.listName)
                .ToArray();
            batchImportSceneObjectListIndex = EditorGUILayout.Popup("场景物体列表", batchImportSceneObjectListIndex, listNames);
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

            ImportObjectsToSelectedTarget(importedObjects);

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

        private void ImportObjectsToSelectedTarget(List<GameObject> importedObjects)
        {
            if (batchImportTarget == BatchImportTarget.CameraPointList)
            {
                EnsureCameraPointListExists();
            }
            else if (batchImportTarget == BatchImportTarget.SceneObjectList)
            {
                EnsureSceneObjectListExists();
            }

            foreach (GameObject obj in importedObjects)
            {
                if (obj == null)
                {
                    continue;
                }

                switch (batchImportTarget)
                {
                    case BatchImportTarget.SingleCameraPoints:
                        currentConfig.singleCameraPoints.Add(CreateCameraPointData(obj));
                        break;
                    case BatchImportTarget.CameraPointList:
                        currentConfig.cameraPointLists[batchImportCameraPointListIndex].cameraPoints.Add(CreateCameraPointData(obj));
                        break;
                    case BatchImportTarget.SingleSceneObjects:
                        currentConfig.singleSceneObjects.Add(CreateSceneObjectData(obj));
                        break;
                    case BatchImportTarget.SceneObjectList:
                        currentConfig.sceneObjectLists[batchImportSceneObjectListIndex].sceneObjects.Add(CreateSceneObjectData(obj));
                        break;
                }
            }
        }

        private void EnsureCameraPointListExists()
        {
            if (currentConfig.cameraPointLists.Count == 0)
            {
                currentConfig.cameraPointLists.Add(new CameraPointListData("ImportedCameraPoints", "批量导入的相机点位"));
                batchImportCameraPointListIndex = 0;
            }

            batchImportCameraPointListIndex = Mathf.Clamp(batchImportCameraPointListIndex, 0, currentConfig.cameraPointLists.Count - 1);
        }

        private void EnsureSceneObjectListExists()
        {
            if (currentConfig.sceneObjectLists.Count == 0)
            {
                currentConfig.sceneObjectLists.Add(new SceneObjectListData("ImportedObjects", "批量导入的物体"));
                batchImportSceneObjectListIndex = 0;
            }

            batchImportSceneObjectListIndex = Mathf.Clamp(batchImportSceneObjectListIndex, 0, currentConfig.sceneObjectLists.Count - 1);
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

        private string GetGeneratedFieldTypeName(FieldType fieldType)
        {
            switch (fieldType)
            {
                case FieldType.String:
                    return "string";
                case FieldType.Int:
                    return "int";
                case FieldType.Bool:
                    return "bool";
                case FieldType.Vector3:
                    return "Vector3";
                case FieldType.GameObject:
                    return "GameObject";
                case FieldType.Model:
                    return "object";
                default:
                    return "string";
            }
        }

        private string GetUniqueGeneratedName(string requestedName, HashSet<string> usedNames)
        {
            string baseName = string.IsNullOrEmpty(requestedName) ? "GeneratedItem" : requestedName;
            string uniqueName = baseName;
            int suffix = 2;
            while (usedNames.Contains(uniqueName))
            {
                uniqueName = baseName + suffix;
                suffix++;
            }

            usedNames.Add(uniqueName);
            return uniqueName;
        }

        private string GetCustomFieldValueProperty(FieldType fieldType)
        {
            switch (fieldType)
            {
                case FieldType.String:
                    return "stringValue";
                case FieldType.Int:
                    return "intValue";
                case FieldType.Bool:
                    return "boolValue";
                case FieldType.Vector3:
                    return "vector3Value";
                case FieldType.GameObject:
                    return "gameObjectValue";
                case FieldType.Model:
                    return "modelValue";
                default:
                    return "stringValue";
            }
        }

        private void AppendCustomFieldAssignment(StringBuilder sb, string targetExpression, CustomFieldData field, string sourceFieldsExpression = "source.customFields")
        {
            string safeFieldName = GetSafeFieldName(field.fieldName);
            string sourceFieldName = EscapeStringLiteral(field.fieldName);
            string valueProperty = GetCustomFieldValueProperty(field.fieldType);
            string indent = targetExpression.Substring(0, targetExpression.Length - targetExpression.TrimStart().Length);
            sb.AppendLine($"{indent}var {safeFieldName}Field = {sourceFieldsExpression}.Find(item => item.fieldName == \"{sourceFieldName}\");");
            sb.AppendLine($"{indent}if ({safeFieldName}Field != null)");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    {targetExpression.TrimStart()}.{safeFieldName} = {safeFieldName}Field.{valueProperty};");
            sb.AppendLine($"{indent}}}");
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
            sb.AppendLine($"/// 配置工具 - 自动生成");
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

            var usedTypeNames = new HashSet<string> { className, "CameraPoint", "SceneObject" };
            var cameraPointListTypeNames = new Dictionary<CameraPointListData, string>();
            var sceneObjectListTypeNames = new Dictionary<SceneObjectListData, string>();
            var customModelTypeNames = new Dictionary<CustomModelData, string>();
            var usedFieldNames = new HashSet<string> { "cameraPoints", "sceneObjects" };
            var cameraPointListFieldNames = new Dictionary<CameraPointListData, string>();
            var sceneObjectListFieldNames = new Dictionary<SceneObjectListData, string>();
            var customModelFieldNames = new Dictionary<CustomModelData, string>();

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
                    string typeName = GetGeneratedFieldTypeName(field.fieldType);
                    sb.AppendLine($"            public {typeName} {GetSafeFieldName(field.fieldName)};");
                }
                sb.AppendLine("        }");
                sb.AppendLine();
                sb.AppendLine($"        public List<CameraPoint> cameraPoints = new List<CameraPoint>();");
            }

            foreach (var list in currentConfig.cameraPointLists)
            {
                string listClassName = GetUniqueGeneratedName(GetSafeClassName(list.listName), usedTypeNames);
                cameraPointListTypeNames[list] = listClassName;
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
                        string typeName = GetGeneratedFieldTypeName(field.fieldType);
                        sb.AppendLine($"                public {typeName} {GetSafeFieldName(field.fieldName)};");
                    }
                    sb.AppendLine("            }");
                }
                sb.AppendLine($"            public List<Point> points = new List<Point>();");
                sb.AppendLine("        }");
                sb.AppendLine();
                string listFieldName = GetUniqueGeneratedName(GetSafeFieldName(list.listName), usedFieldNames);
                cameraPointListFieldNames[list] = listFieldName;
                sb.AppendLine($"        public List<{listClassName}> {listFieldName} = new List<{listClassName}>();");
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
                    string typeName = GetGeneratedFieldTypeName(field.fieldType);
                    sb.AppendLine($"            public {typeName} {GetSafeFieldName(field.fieldName)};");
                }
                sb.AppendLine("        }");
                sb.AppendLine();
                sb.AppendLine($"        public List<SceneObject> sceneObjects = new List<SceneObject>();");
            }

            foreach (var list in currentConfig.sceneObjectLists)
            {
                string listClassName = GetUniqueGeneratedName(GetSafeClassName(list.listName), usedTypeNames);
                sceneObjectListTypeNames[list] = listClassName;
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
                        string typeName = GetGeneratedFieldTypeName(field.fieldType);
                        sb.AppendLine($"                public {typeName} {GetSafeFieldName(field.fieldName)};");
                    }
                    sb.AppendLine("            }");
                }
                sb.AppendLine($"            public List<Object> objects = new List<Object>();");
                sb.AppendLine("        }");
                sb.AppendLine();
                string listFieldName = GetUniqueGeneratedName(GetSafeFieldName(list.listName), usedFieldNames);
                sceneObjectListFieldNames[list] = listFieldName;
                sb.AppendLine($"        public List<{listClassName}> {listFieldName} = new List<{listClassName}>();");
            }

            foreach (var model in currentConfig.customModels)
            {
                string modelClassName = GetUniqueGeneratedName(GetSafeClassName(model.modelName), usedTypeNames);
                customModelTypeNames[model] = modelClassName;
                sb.AppendLine("        [System.Serializable]");
                sb.AppendLine($"        public class {modelClassName}");
                sb.AppendLine("        {");
                foreach (var field in model.fields)
                {
                    sb.AppendLine($"            public {GetGeneratedFieldTypeName(field.fieldType)} {GetSafeFieldName(field.fieldName)};");
                }
                sb.AppendLine("        }");
                sb.AppendLine();
                string modelFieldName = GetUniqueGeneratedName(GetSafeFieldName(model.modelName), usedFieldNames);
                customModelFieldNames[model] = modelFieldName;
                sb.AppendLine($"        public List<{modelClassName}> {modelFieldName} = new List<{modelClassName}>();");
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
                string listClassName = cameraPointListTypeNames[list];
                string fieldName = cameraPointListFieldNames[list];
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
                string listClassName = sceneObjectListTypeNames[list];
                string fieldName = sceneObjectListFieldNames[list];
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
            foreach (var model in currentConfig.customModels)
            {
                string modelClassName = customModelTypeNames[model];
                string fieldName = customModelFieldNames[model];
                sb.AppendLine($"            {fieldName}.Clear();");
                sb.AppendLine("            {");
                sb.AppendLine($"                var sourceModel = buffer.customModels.Find(item => item.modelName == \"{EscapeStringLiteral(model.modelName)}\");");
                sb.AppendLine("                if (sourceModel != null)");
                sb.AppendLine("                {");
                sb.AppendLine($"                    var item = new {modelClassName}();");
                foreach (var field in model.fields)
                {
                    AppendCustomFieldAssignment(sb, "                    item", field, "sourceModel.fields");
                }
                sb.AppendLine($"                    {fieldName}.Add(item);");
                sb.AppendLine("                }");
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

            string pendingObjectName = "Temp_" + className + "_" + Guid.NewGuid().ToString("N");
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

            EditorApplication.delayCall += CompletePendingGenerate;
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
                    DestroyImmediate(managerObj);
                    EditorUtility.DisplayDialog("警告", $"脚本已生成，但 Unity 未能解析生成的组件类型。请检查 Console 是否有编译错误。\n脚本保存位置: {savePath}", "确定");
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
            EditorUtility.DisplayDialog("成功", $"配置已生成并添加到场景中\n脚本保存位置: {savePath}", "确定");
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

        private enum ToolbarPageType
        {
            CameraPoints,
            SceneObjects,
            Style,
            CustomConfigRoot,
            SingleCustomConfig,
            CustomConfigList,
            BatchImport
        }

        private class ToolbarPage
        {
            public string title;
            public ToolbarPageType pageType;
            public CustomConfigData singleCustomConfig;
            public CustomConfigListData customConfigList;

            public ToolbarPage(string title, ToolbarPageType pageType, CustomConfigData singleCustomConfig = null, CustomConfigListData customConfigList = null)
            {
                this.title = title;
                this.pageType = pageType;
                this.singleCustomConfig = singleCustomConfig;
                this.customConfigList = customConfigList;
            }
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
