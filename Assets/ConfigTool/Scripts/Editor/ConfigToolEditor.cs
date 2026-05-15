using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace ConfigTool.Editor
{
    public class ConfigToolEditor : EditorWindow
    {
        private ConfigToolData currentConfig;
        private Vector2 scrollPosition;
        private readonly List<ToolbarPage> toolbarPages = new List<ToolbarPage>();
        private int toolbarIndex = 0;
        private readonly Dictionary<CustomModelData, bool> customModelFoldouts = new Dictionary<CustomModelData, bool>();
        private readonly Dictionary<CustomConfigEntryData, bool> customConfigEntryFoldouts = new Dictionary<CustomConfigEntryData, bool>();
        private readonly Dictionary<string, Type> sourceTypeCache = new Dictionary<string, Type>();
        private readonly Dictionary<Type, List<CustomFieldData>> supportedFieldCache = new Dictionary<Type, List<CustomFieldData>>();
        private readonly Dictionary<CustomModelData, MonoScript> sourceMonoScriptCache = new Dictionary<CustomModelData, MonoScript>();
        private float fieldNameColumnWidth = 120f;
        private float fieldTypeColumnWidth = 96f;
        private float modelSelectorColumnWidth = 300f;
        private const int MaxModelNestingDepth = 5;
        private ConfigToolData toolbarConfig;
        private int toolbarConfigVersion = -1;
        private Vector2 modelTypeScrollPosition;
        private List<Type> serializableModelTypes;
        private string modelTypeSearch = "";
        private string serializableModelFolderPath = "Assets/DataModel";

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

        private void OnEnable()
        {
            if (Selection.activeObject is ConfigToolData)
            {
                currentConfig = (ConfigToolData)Selection.activeObject;
            }
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is ConfigToolData selectedConfig && selectedConfig != currentConfig)
            {
                currentConfig = selectedConfig;
                ClearEditorCaches();
                Repaint();
            }
        }

        private void ClearEditorCaches()
        {
            sourceTypeCache.Clear();
            supportedFieldCache.Clear();
            sourceMonoScriptCache.Clear();
            toolbarConfigVersion = -1;
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
            EditorGUILayout.BeginHorizontal();
            string[] pageNames = toolbarPages.Select(page => page.title).ToArray();
            toolbarIndex = Mathf.Clamp(toolbarIndex, 0, pageNames.Length - 1);
            toolbarIndex = GUILayout.Toolbar(toolbarIndex, pageNames);

            using (new EditorGUI.DisabledScope(currentConfig == null))
            {
                if (GUILayout.Button("+", GUILayout.Width(32), GUILayout.Height(22)))
                {
                    AddUnnamedCustomConfig();
                }

            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        private void BuildToolbarPages()
        {
            int currentVersion = currentConfig == null ? -1 : currentConfig.singleCustomConfigs.Count;
            if (toolbarConfig == currentConfig && toolbarConfigVersion == currentVersion && toolbarPages.Count > 0)
            {
                return;
            }

            toolbarConfig = currentConfig;
            toolbarConfigVersion = currentVersion;
            toolbarPages.Clear();
            toolbarPages.Add(new ToolbarPage("Model 管理器", ToolbarPageType.CustomConfigRoot));

            if (currentConfig != null)
            {
                foreach (var config in currentConfig.singleCustomConfigs)
                {
                    toolbarPages.Add(new ToolbarPage(string.IsNullOrEmpty(config.configName) ? "未命名配置" : config.configName, ToolbarPageType.SingleCustomConfig, config));
                }
            }
        }

        private void DrawToolbarPage(ToolbarPage page)
        {
            switch (page.pageType)
            {
                case ToolbarPageType.CustomConfigRoot:
                    DrawCustomConfigSection();
                    break;
                case ToolbarPageType.SingleCustomConfig:
                    DrawSingleCustomConfigPage(page.singleCustomConfig);
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

        private void DrawCustomFieldsList(List<CustomFieldData> fields, HashSet<string> structureVisited, int nestingDepth, bool allowNestedModelSelection, Action onNestedModelChanged)
        {
            EditorGUILayout.LabelField("字段结构:", GUILayout.Width(90));
            foreach (CustomFieldData field in fields)
            {
                DrawFieldStructure(field, structureVisited ?? new HashSet<string>(), nestingDepth, allowNestedModelSelection, onNestedModelChanged);
            }
        }

        private void DrawFieldStructure(CustomFieldData field, HashSet<string> visited, int nestingDepth, bool allowNestedModelSelection, Action onNestedModelChanged)
        {
            if (field.fieldType == FieldType.Model)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(field.fieldName, GUILayout.Width(fieldNameColumnWidth));
                DrawColumnResizer(ref fieldNameColumnWidth, 80f, 260f);
                EditorGUILayout.LabelField("Model", GUILayout.Width(64));
                if (allowNestedModelSelection)
                {
                    string selectedModel = DrawModelSelector("", field.modelTypeName, null, Mathf.Max(180f, modelSelectorColumnWidth * 0.67f));
                    if (selectedModel != field.modelTypeName)
                    {
                        field.modelTypeName = selectedModel;
                        field.modelValue = CreateModelInstance(field.modelTypeName);
                        onNestedModelChanged?.Invoke();
                        EditorUtility.SetDirty(currentConfig);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(string.IsNullOrEmpty(field.modelTypeName) ? "未指定" : field.modelTypeName, GUILayout.Width(Mathf.Max(180f, modelSelectorColumnWidth * 0.67f)));
                }
                EditorGUILayout.EndHorizontal();

                if (nestingDepth + 1 >= MaxModelNestingDepth)
                {
                    EditorGUILayout.HelpBox($"嵌套层级已达到上限 {MaxModelNestingDepth} 层，已停止继续展开。", MessageType.Info);
                    EditorGUILayout.EndVertical();
                    return;
                }

                bool repeatedModel = visited.Contains(field.modelTypeName);
                visited.Add($"{field.modelTypeName}:{nestingDepth}");
                if (repeatedModel && nestingDepth + 1 >= MaxModelNestingDepth)
                {
                    EditorGUILayout.HelpBox($"循环嵌套最多允许 {MaxModelNestingDepth} 层。", MessageType.Info);
                    EditorGUILayout.EndVertical();
                    return;
                }

                EditorGUI.indentLevel++;
                DrawModelInstanceFields(field.modelValue, field.modelTypeName, visited, nestingDepth + 1, allowNestedModelSelection, onNestedModelChanged);
                EditorGUI.indentLevel--;
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal("box");
            EditorGUILayout.LabelField(field.fieldName, GUILayout.Width(fieldNameColumnWidth));
            DrawColumnResizer(ref fieldNameColumnWidth, 80f, 260f);
            EditorGUILayout.LabelField(field.fieldType.ToString(), GUILayout.Width(fieldTypeColumnWidth));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawColumnResizer(ref float width, float minWidth, float maxWidth)
        {
            Rect rect = GUILayoutUtility.GetRect(4f, EditorGUIUtility.singleLineHeight, GUILayout.Width(4f));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
            int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition))
            {
                GUIUtility.hotControl = controlId;
                evt.Use();
            }
            else if (evt.type == EventType.MouseDrag && GUIUtility.hotControl == controlId)
            {
                width = Mathf.Clamp(width + evt.delta.x, minWidth, maxWidth);
                Repaint();
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && GUIUtility.hotControl == controlId)
            {
                GUIUtility.hotControl = 0;
                evt.Use();
            }
        }

        private void DrawCustomConfigSection()
        {
            EditorGUILayout.BeginVertical("box");
            //EditorGUILayout.LabelField("Model 管理器", EditorStyles.boldLabel);
            //EditorGUILayout.HelpBox("在这里新增、导入或生成 Model；顶部 + 会创建真正的配置，进入配置后可自由添加单项配置或列表项配置。", MessageType.Info);

            DrawCustomModelDefinitionsSection();
            EditorGUILayout.Space();
            DrawSerializableModelImportSection();
            EditorGUILayout.EndVertical();
        }

        private void DrawCustomModelDefinitionsSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"配置模块定义: {currentConfig.customModels.Count}", EditorStyles.boldLabel);

            for (int i = 0; i < currentConfig.customModels.Count; i++)
            {
                DrawCustomModel(currentConfig.customModels[i], i);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawCustomModel(CustomModelData model, int index)
        {
            bool isExpanded = GetCustomModelFoldout(model);
            Rect borderRect = BeginConfigEntryFrame(isExpanded);
            EditorGUILayout.BeginHorizontal();
            isExpanded = EditorGUILayout.Foldout(isExpanded, model.modelName, true);
            SetCustomModelFoldout(model, isExpanded);
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                currentConfig.customModels.RemoveAt(index);
                customModelFoldouts.Remove(model);
                EditorUtility.SetDirty(currentConfig);
                EditorGUILayout.EndHorizontal();
                EndConfigEntryFrame(borderRect, isExpanded);
                return;
            }
            EditorGUILayout.EndHorizontal();

            if (!isExpanded)
            {
                EditorGUILayout.LabelField($"字段数量: {model.fields.Count}");
                EndConfigEntryFrame(borderRect, isExpanded);
                EditorGUILayout.Space();
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("类名", model.modelName);
                EditorGUILayout.ObjectField("来源脚本", GetSourceMonoScript(model), typeof(MonoScript), false);
            }

            DrawCustomFieldsList(model.fields, null, 0, false, null);
            EndConfigEntryFrame(borderRect, isExpanded);
            EditorGUILayout.Space();
        }

        private void AddUnnamedCustomConfig()
        {
            var config = new CustomConfigData();
            currentConfig.singleCustomConfigs.Add(config);
            toolbarConfigVersion = -1;
            BuildToolbarPages();
            toolbarIndex = toolbarPages.Count - 1;
            EditorUtility.SetDirty(currentConfig);
        }

        private void DeleteSingleCustomConfig(CustomConfigData config)
        {
            if (config == null)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog("删除配置", $"确定删除配置 {config.configName}？", "删除", "取消"))
            {
                return;
            }

            currentConfig.singleCustomConfigs.Remove(config);
            toolbarIndex = 0;
            EditorUtility.SetDirty(currentConfig);
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
            if (GUILayout.Button("删除当前配置", GUILayout.Height(28)))
            {
                DeleteSingleCustomConfig(config);
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawCustomConfig(CustomConfigData config, Action removeAction)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            config.configName = EditorGUILayout.TextField("配置名称", config.configName);
            if (removeAction != null && GUILayout.Button("X", GUILayout.Width(25)))
            {
                removeAction();
                EditorUtility.SetDirty(currentConfig);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"配置项: {config.entries.Count}");
            if (GUILayout.Button("添加单项配置", GUILayout.Width(110)))
            {
                AddModelEntry(config);
            }
            if (GUILayout.Button("添加列表项配置", GUILayout.Width(130)))
            {
                AddModelListEntry(config);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel++;
            for (int i = 0; i < config.entries.Count; i++)
            {
                int entryIndex = i;
                DrawCustomConfigEntry(config.entries[i], () => config.entries.RemoveAt(entryIndex));
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void AddModelEntry(CustomConfigData config)
        {
            string modelName = DrawDefaultModelName();
            var entry = new CustomConfigEntryData
            {
                entryKind = CustomConfigEntryKind.Model,
                entryName = modelName,
                scriptParameterName = modelName,
                modelTypeName = modelName
            };
            ApplyModelTemplate(entry);
            config.entries.Add(entry);
            EditorUtility.SetDirty(currentConfig);
        }

        private void AddModelListEntry(CustomConfigData config)
        {
            string modelName = DrawDefaultModelName();
            config.entries.Add(new CustomConfigEntryData
            {
                entryKind = CustomConfigEntryKind.ModelList,
                entryName = string.IsNullOrEmpty(modelName) ? "" : modelName + "List",
                scriptParameterName = string.IsNullOrEmpty(modelName) ? "" : modelName + "List",
                modelTypeName = modelName
            });
            EditorUtility.SetDirty(currentConfig);
        }

        private void DrawCustomConfigEntry(CustomConfigEntryData entry, Action removeAction)
        {
            bool isExpanded = GetCustomConfigEntryFoldout(entry);
            Rect borderRect = BeginConfigEntryFrame(isExpanded);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(18));
            isExpanded = EditorGUILayout.Foldout(isExpanded, "", true);
            EditorGUILayout.EndVertical();
            SetCustomConfigEntryFoldout(entry, isExpanded);
            if (isExpanded)
            {
                EditorGUILayout.LabelField("Inspector名字", GUILayout.Width(92));
                //GUILayout.Space();
                entry.entryName = EditorGUILayout.TextField(entry.entryName, GUILayout.Width(180));
                GUILayout.FlexibleSpace();
            }
            else
            {
                EditorGUILayout.LabelField(string.IsNullOrEmpty(entry.entryName) ? "未命名配置" : entry.entryName, EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
            }
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                removeAction();
                EditorUtility.SetDirty(currentConfig);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EndConfigEntryFrame(borderRect, isExpanded);
                return;
            }
            EditorGUILayout.EndHorizontal();

            if (isExpanded)
            {
                EditorGUILayout.BeginHorizontal();
                // 固定宽度标签
                EditorGUILayout.LabelField("Model", GUILayout.Width(60));

                // 后面是你的选择器
                string selectedModel = DrawModelSelector("", entry.modelTypeName, null, modelSelectorColumnWidth * 0.67f);
                GUILayout.Space(10);
                if (selectedModel != entry.modelTypeName)
                {
                    entry.modelTypeName = selectedModel;
                    if (entry.entryKind == CustomConfigEntryKind.Model)
                    {
                        ApplyModelTemplate(entry);
                    }
                    else
                    {
                        foreach (CustomConfigData config in entry.configs)
                        {
                            config.modelTypeName = selectedModel;
                            ApplyModelTemplate(config);
                        }
                    }
                    EditorUtility.SetDirty(currentConfig);
                }
                EditorGUILayout.EndHorizontal();

                if (entry.entryKind == CustomConfigEntryKind.Model)
                {
                    DrawModelInstanceFields(entry.value, entry.modelTypeName);
                }
                else
                {
                    DrawCustomConfigEntryList(entry);
                }
            }

            EditorGUILayout.EndVertical();
            EndConfigEntryFrame(borderRect, isExpanded);
            EditorGUILayout.Space();
        }

        private Rect BeginConfigEntryFrame(bool isExpanded)
        {
            Rect rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            Color color = isExpanded ? Color.green : Color.black;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
            return rect;
        }

        private void EndConfigEntryFrame(Rect rect, bool isExpanded)
        {
            Color color = isExpanded ? Color.green : Color.black;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
            EditorGUILayout.EndVertical();
        }

        private bool GetCustomConfigEntryFoldout(CustomConfigEntryData entry)
        {
            if (!customConfigEntryFoldouts.TryGetValue(entry, out bool isExpanded))
            {
                isExpanded = true;
                customConfigEntryFoldouts[entry] = isExpanded;
            }

            return isExpanded;
        }

        private void SetCustomConfigEntryFoldout(CustomConfigEntryData entry, bool isExpanded)
        {
            customConfigEntryFoldouts[entry] = isExpanded;
        }

        private void DrawCustomConfigEntryList(CustomConfigEntryData entry)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"列表项: {entry.configs.Count}");
            if (GUILayout.Button("添加配置", GUILayout.Width(100)))
            {
                var config = new CustomConfigData
                {
                    modelTypeName = entry.modelTypeName
                };
                ApplyModelTemplate(config);
                if (entry.configs.Count > 0)
                {
                    SyncListItemNestedModels(entry.configs[0], config);
                }
                entry.configs.Add(config);
                EditorUtility.SetDirty(currentConfig);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel++;
            for (int i = 0; i < entry.configs.Count; i++)
            {
                int configIndex = i;
                DrawModelListItem(entry, entry.configs[i], configIndex, () => entry.configs.RemoveAt(configIndex));
            }
            EditorGUI.indentLevel--;
        }

        private void DrawModelListItem(CustomConfigEntryData entry, CustomConfigData config, int index, Action removeAction)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            config.configName = EditorGUILayout.TextField("项名称", config.configName);
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                removeAction();
                EditorUtility.SetDirty(currentConfig);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();
            bool canEditNestedModel = index == 0;
            DrawModelInstanceFields(config.value, config.modelTypeName, new HashSet<string>(), 0, canEditNestedModel, () => SyncListItemsFromFirst(entry));
            if (!canEditNestedModel)
            {
                EditorGUILayout.HelpBox("列表项的嵌套 Model 类型跟随第一个列表项。", MessageType.Info);
            }
            EditorGUILayout.EndVertical();
        }

        private void SyncListItemsFromFirst(CustomConfigEntryData entry)
        {
            if (entry == null || entry.configs.Count == 0)
            {
                return;
            }

            CustomConfigData source = entry.configs[0];
            for (int i = 1; i < entry.configs.Count; i++)
            {
                SyncListItemNestedModels(source, entry.configs[i]);
            }
            EditorUtility.SetDirty(currentConfig);
        }

        private void SyncListItemNestedModels(CustomConfigData source, CustomConfigData target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.modelTypeName = source.modelTypeName;
            SyncNestedModelTypes(source.value, target.value);
        }

        private void SyncNestedModelTypes(CustomModelInstanceData source, CustomModelInstanceData target)
        {
            if (source == null || target == null)
            {
                return;
            }

            foreach (CustomFieldData sourceField in source.fields)
            {
                if (sourceField.fieldType != FieldType.Model)
                {
                    continue;
                }

                CustomFieldData targetField = target.fields.FirstOrDefault(field => field.fieldName == sourceField.fieldName && field.fieldType == FieldType.Model);
                if (targetField == null)
                {
                    continue;
                }

                if (targetField.modelTypeName != sourceField.modelTypeName)
                {
                    targetField.modelTypeName = sourceField.modelTypeName;
                    targetField.modelValue = CreateModelInstance(targetField.modelTypeName);
                }
                SyncNestedModelTypes(sourceField.modelValue, targetField.modelValue);
            }
        }

        private string DrawDefaultModelName()
        {
            return DrawDefaultModelName(null);
        }

        private string DrawDefaultModelName(string ownerModelName)
        {
            return currentConfig.customModels
                .Select(model => model.modelName)
                .FirstOrDefault(name => !string.IsNullOrEmpty(name) && !WouldCreateRecursiveModelReference(ownerModelName, name)) ?? "";
        }

        private string DrawModelSelector(string label, string selectedModel)
        {
            return DrawModelSelector(label, selectedModel, null);
        }

        private string DrawModelSelector(string label, string selectedModel, string ownerModelName, float selectorWidth = -1f)
        {
            string[] modelNames = currentConfig.customModels
                .Select(model => model.modelName)
                .Where(name => !string.IsNullOrEmpty(name) && !WouldCreateRecursiveModelReference(ownerModelName, name))
                .ToArray();
            if (modelNames.Length == 0)
            {
                EditorGUILayout.LabelField(label, "请先添加 Model");
                return "";
            }

            int selectedIndex = Array.IndexOf(modelNames, selectedModel);
            selectedIndex = Mathf.Max(0, selectedIndex);
            if (!string.IsNullOrEmpty(label))
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(42));
            }
            float width = selectorWidth > 0f ? selectorWidth : modelSelectorColumnWidth;
            selectedIndex = EditorGUILayout.Popup(selectedIndex, modelNames, GUILayout.Width(width));
            if (selectorWidth <= 0f)
            {
                DrawColumnResizer(ref modelSelectorColumnWidth, 180f, 480f);
            }
            return modelNames[selectedIndex];
        }

        private bool WouldCreateRecursiveModelReference(string ownerModelName, string selectedModelName)
        {
            if (string.IsNullOrEmpty(ownerModelName))
            {
                return false;
            }

            return selectedModelName == ownerModelName || ModelReferencesModel(selectedModelName, ownerModelName, new HashSet<string>());
        }

        private bool ModelReferencesModel(string modelName, string targetModelName, HashSet<string> visited)
        {
            if (!visited.Add(modelName))
            {
                return false;
            }

            CustomModelData model = currentConfig.customModels.FirstOrDefault(item => item.modelName == modelName);
            if (model == null)
            {
                return false;
            }

            foreach (CustomFieldData field in model.fields)
            {
                if (field.fieldType != FieldType.Model)
                {
                    continue;
                }

                if (field.modelTypeName == targetModelName || ModelReferencesModel(field.modelTypeName, targetModelName, visited))
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyModelTemplate(CustomConfigData config)
        {
            CustomModelData model = currentConfig.customModels.FirstOrDefault(item => item.modelName == config.modelTypeName);
            config.value = new CustomModelInstanceData
            {
                fields = model == null ? new List<CustomFieldData>() : CloneFieldTemplates(model.fields)
            };
        }

        private void SyncModelUsages(string modelName)
        {
            foreach (CustomConfigData config in currentConfig.singleCustomConfigs)
            {
                SyncConfigData(config, modelName);
            }
            EditorUtility.SetDirty(currentConfig);
        }

        private void SyncConfigData(CustomConfigData config, string modelName)
        {
            if (config == null)
            {
                return;
            }

            if (config.modelTypeName == modelName)
            {
                SyncModelInstance(config.value, modelName);
            }

            foreach (CustomConfigEntryData entry in config.entries)
            {
                if (entry.modelTypeName == modelName)
                {
                    if (entry.entryKind == CustomConfigEntryKind.Model)
                    {
                        SyncModelInstance(entry.value, modelName);
                    }
                    else
                    {
                        foreach (CustomConfigData item in entry.configs)
                        {
                            item.modelTypeName = modelName;
                            SyncModelInstance(item.value, modelName);
                        }
                    }
                }
            }
        }

        private void SyncModelInstance(CustomModelInstanceData instance, string modelName)
        {
            SyncModelInstance(instance, modelName, new HashSet<string>());
        }

        private void SyncModelInstance(CustomModelInstanceData instance, string modelName, HashSet<string> visited)
        {
            CustomModelData model = currentConfig.customModels.FirstOrDefault(item => item.modelName == modelName);
            if (instance == null || model == null || !visited.Add(modelName))
            {
                return;
            }

            List<CustomFieldData> syncedFields = new List<CustomFieldData>();
            foreach (CustomFieldData template in model.fields)
            {
                CustomFieldData existing = instance.fields.FirstOrDefault(field => field.fieldName == template.fieldName && field.fieldType == template.fieldType);
                syncedFields.Add(existing == null ? CloneFieldTemplate(template, visited) : SyncFieldValue(existing, template, visited));
            }
            instance.fields = syncedFields;
            visited.Remove(modelName);
        }

        private CustomFieldData SyncFieldValue(CustomFieldData existing, CustomFieldData template, HashSet<string> visited)
        {
            existing.modelTypeName = template.modelTypeName;
            if (existing.fieldType == FieldType.Model)
            {
                SyncModelInstance(existing.modelValue, existing.modelTypeName, visited);
            }
            return existing;
        }

        private void ApplyModelTemplate(CustomConfigEntryData entry)
        {
            CustomModelData model = currentConfig.customModels.FirstOrDefault(item => item.modelName == entry.modelTypeName);
            entry.value = new CustomModelInstanceData
            {
                fields = model == null ? new List<CustomFieldData>() : CloneFieldTemplates(model.fields)
            };
        }

        private List<CustomFieldData> CloneFieldTemplates(List<CustomFieldData> fields)
        {
            return CloneFieldTemplates(fields, new HashSet<string>());
        }

        private List<CustomFieldData> CloneFieldTemplates(List<CustomFieldData> fields, HashSet<string> visited)
        {
            return fields.Select(field => CloneFieldTemplate(field, visited)).ToList();
        }

        private CustomFieldData CloneFieldTemplate(CustomFieldData field)
        {
            return CloneFieldTemplate(field, new HashSet<string>());
        }

        private CustomFieldData CloneFieldTemplate(CustomFieldData field, HashSet<string> visited)
        {
            return new CustomFieldData(field.fieldName, field.fieldType)
            {
                modelTypeName = field.modelTypeName,
                modelValue = field.fieldType == FieldType.Model ? CreateModelInstance(field.modelTypeName, visited) : new CustomModelInstanceData()
            };
        }

        private CustomModelInstanceData CreateModelInstance(string modelTypeName)
        {
            return CreateModelInstance(modelTypeName, new HashSet<string>());
        }

        private CustomModelInstanceData CreateModelInstance(string modelTypeName, HashSet<string> visited)
        {
            if (string.IsNullOrEmpty(modelTypeName) || !visited.Add(modelTypeName))
            {
                return new CustomModelInstanceData();
            }

            CustomModelData model = currentConfig.customModels.FirstOrDefault(item => item.modelName == modelTypeName);
            var instance = new CustomModelInstanceData
            {
                fields = model == null ? new List<CustomFieldData>() : CloneFieldTemplates(model.fields, visited)
            };
            visited.Remove(modelTypeName);
            return instance;
        }

        private void DrawModelInstanceFields(CustomModelInstanceData instance, string modelTypeName)
        {
            DrawModelInstanceFields(instance, modelTypeName, new HashSet<string>(), 0, true, null);
        }

        private void DrawModelInstanceFields(CustomModelInstanceData instance, string modelTypeName, HashSet<string> visited, int nestingDepth, bool allowNestedModelSelection, Action onNestedModelChanged)
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

            DrawCustomFieldsList(instance.fields, visited, nestingDepth, allowNestedModelSelection, onNestedModelChanged);
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
            EditorGUILayout.LabelField("从文件夹导入", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            serializableModelFolderPath = EditorGUILayout.TextField("扫描路径", serializableModelFolderPath);
            if (GUILayout.Button("选择", GUILayout.Width(60)))
            {
                string selectedPath = EditorUtility.OpenFolderPanel("选择文件夹", "Assets", "");
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

            sourceTypeCache.Clear();
            supportedFieldCache.Clear();
            sourceMonoScriptCache.Clear();
            serializableModelTypes = GetAllLoadedTypes()
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

        private Type GetSourceType(CustomModelData model)
        {
            if (string.IsNullOrEmpty(model.sourceTypeName))
            {
                return null;
            }

            if (sourceTypeCache.TryGetValue(model.sourceTypeName, out Type cachedType))
            {
                return cachedType;
            }

            cachedType = GetAllLoadedTypes()
                .FirstOrDefault(type => type.FullName == model.sourceTypeName || type.Name == model.sourceTypeName);
            sourceTypeCache[model.sourceTypeName] = cachedType;
            return cachedType;
        }

        private IEnumerable<Type> GetAllLoadedTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
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
                });
        }

        private MonoScript GetSourceMonoScript(CustomModelData model)
        {
            if (string.IsNullOrEmpty(model.sourceTypeName))
            {
                return null;
            }

            if (sourceMonoScriptCache.TryGetValue(model, out MonoScript cachedScript))
            {
                return cachedScript;
            }

            Type sourceType = GetSourceType(model);
            if (sourceType == null)
            {
                return null;
            }

            cachedScript = AssetDatabase.FindAssets($"{sourceType.Name} t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath<MonoScript>(path))
                .FirstOrDefault(script => script != null && script.GetClass() == sourceType);
            sourceMonoScriptCache[model] = cachedScript;
            return cachedScript;
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
            SyncModelUsages(model.modelName);
            EditorUtility.SetDirty(currentConfig);
        }

        private List<CustomFieldData> GetSupportedSerializableFields(Type type)
        {
            if (supportedFieldCache.TryGetValue(type, out List<CustomFieldData> cachedFields))
            {
                return cachedFields.Select(CloneFieldData).ToList();
            }

            cachedFields = type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                .Select(field => CreateFieldFromType(field.Name, field.FieldType))
                .Where(field => field != null)
                .ToList();
            supportedFieldCache[type] = cachedFields;
            return cachedFields.Select(CloneFieldData).ToList();
        }

        private static CustomFieldData CloneFieldData(CustomFieldData field)
        {
            return new CustomFieldData(field.fieldName, field.fieldType)
            {
                modelTypeName = field.modelTypeName
            };
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
            if (type == typeof(float))
            {
                return new CustomFieldData(fieldName, FieldType.Float);
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
            if (type == typeof(Material))
            {
                return new CustomFieldData(fieldName, FieldType.Material);
            }
            if (type == typeof(Texture))
            {
                return new CustomFieldData(fieldName, FieldType.Texture);
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

        private enum ToolbarPageType
        {
            CustomConfigRoot,
            SingleCustomConfig
        }

        private class ToolbarPage
        {
            public string title;
            public ToolbarPageType pageType;
            public CustomConfigData singleCustomConfig;

            public ToolbarPage(string title, ToolbarPageType pageType, CustomConfigData singleCustomConfig = null)
            {
                this.title = title;
                this.pageType = pageType;
                this.singleCustomConfig = singleCustomConfig;
            }
        }

    }
}
