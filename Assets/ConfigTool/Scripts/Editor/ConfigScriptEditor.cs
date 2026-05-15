using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ConfigTool.Editor
{
    public class ConfigScriptEditor : EditorWindow
    {
        private ConfigToolData currentConfig;
        private ConfigToolData editingConfig;
        private string configScriptName = "";
        private string generatedPreview = "";
        private ConfigToolData previousConfig;
        private string bottomMessage = "";
        private bool bottomMessageIsError;
        private double bottomMessageExpireTime;
        private Vector2 scrollPosition;
        private string checkedConfigScriptName = "";
        private bool hasCheckedConfigScriptName;
        private Type cachedConfigScriptType;
        private bool lockedScriptParameterNames;
        private List<Component> cachedConfigComponents = new List<Component>();
        private bool cachedConfigComponentsSearched;
        private int selectedConfigComponentIndex;
        private string cachedStructureMatchKey = "";
        private bool cachedStructureMatches;
        private string cachedStructureMismatchMessage = "";
        private float fieldNameColumnWidth = 140f;
        private float fieldValueColumnWidth = 260f;
        private readonly Dictionary<CustomConfigEntryData, BatchState> batchStates = new Dictionary<CustomConfigEntryData, BatchState>();
        private readonly Dictionary<CustomConfigEntryData, SingleObjectState> singleObjectStates = new Dictionary<CustomConfigEntryData, SingleObjectState>();
        private readonly Dictionary<CustomConfigEntryData, bool> scriptEntryFoldouts = new Dictionary<CustomConfigEntryData, bool>();

        private const string PendingConfigScriptPathKey = "ConfigScriptEditor.Pending.ConfigScriptPath";
        private const string PendingConfigClassNameKey = "ConfigScriptEditor.Pending.ConfigClassName";
        private const string PendingObjectNameKey = "ConfigScriptEditor.Pending.ObjectName";
        private const string PendingRetryCountKey = "ConfigScriptEditor.Pending.RetryCount";
        private const int PendingMaxRetries = 120;

        private bool HasCurrentScriptCheck => hasCheckedConfigScriptName && checkedConfigScriptName == configScriptName;

        [MenuItem("ConfigSetting/配置脚本编辑器")]
    public static void ShowWindow()
        {
            var window = GetWindow<ConfigScriptEditor>("配置脚本编辑器");
            window.minSize = new Vector2(800, 600);
        }

        [InitializeOnLoadMethod]
        private static void ResumePendingAddToScene()
        {
            if (SessionState.GetBool(PendingConfigScriptPathKey + ".Active", false))
            {
                EditorApplication.delayCall += CompletePendingAddToScene;
            }
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is ConfigToolData config)
            {
                currentConfig = config;
                Repaint();
            }
        }

        private void OnEnable()
        {
            EditorApplication.hierarchyChanged += ClearConfigComponentCache;
            Undo.undoRedoPerformed += ClearConfigComponentCache;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= ClearConfigComponentCache;
            Undo.undoRedoPerformed -= ClearConfigComponentCache;
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            DrawHeader();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawConfigContentEditor();
            DrawScriptGeneration();
            if (!HasCurrentScriptCheck || cachedConfigScriptType == null)
            {
                DrawPreview();
            }
            EditorGUILayout.EndScrollView();
            DrawBottomMessage();
            EditorGUILayout.EndVertical();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("配置脚本编辑器", EditorStyles.boldLabel);
            currentConfig = (ConfigToolData)EditorGUILayout.ObjectField("配置结构", currentConfig, typeof(ConfigToolData), false);
            if (currentConfig != previousConfig)
            {
                previousConfig = currentConfig;
                editingConfig = currentConfig == null ? null : CreateEmptyEditingConfig(currentConfig);
                batchStates.Clear();
                singleObjectStates.Clear();
                scriptEntryFoldouts.Clear();
                lockedScriptParameterNames = false;
                ClearCheckedScriptState();
                if (currentConfig != null)
                {
                    configScriptName = "";
                }
            }

            Type scriptType = HasCurrentScriptCheck ? cachedConfigScriptType : null;
            List<Component> sceneComponents = scriptType == null ? new List<Component>() : GetExistingConfigComponents(scriptType);
            Component sceneComponent = GetSelectedConfigComponent(sceneComponents);
            bool structureMatches = sceneComponent != null && IsComponentStructureMatching(sceneComponent, out _);
            bool structureMismatch = sceneComponent != null && !structureMatches;
            bool allowEditScriptName = !HasCurrentScriptCheck || scriptType == null || structureMismatch;
            Color originalContentColor = GUI.contentColor;
            if (HasCurrentScriptCheck && !allowEditScriptName && structureMatches)
            {
                GUI.contentColor = Color.green;
            }
            else if (HasCurrentScriptCheck && structureMismatch)
            {
                GUI.contentColor = Color.red;
            }

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!allowEditScriptName))
            {
                configScriptName = EditorGUILayout.TextField("配置脚本名", configScriptName);
            }
            GUI.contentColor = originalContentColor;
            using (new EditorGUI.DisabledScope(!allowEditScriptName && HasCurrentScriptCheck))
            {
                if (GUILayout.Button("检测脚本名", GUILayout.Width(96)))
                {
                    CheckConfigScriptName();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(GetHeaderHelpText(scriptType, sceneComponent, structureMatches), MessageType.Info);
            if (scriptType != null)
            {
                DrawExistingScriptWorkflow(scriptType, sceneComponents, sceneComponent, structureMatches);
            }
            EditorGUILayout.EndVertical();
        }

        private string GetHeaderHelpText(Type scriptType, Component sceneComponent, bool structureMatches)
        {
            if (!HasCurrentScriptCheck)
            {
                return "输入配置脚本名后点击“检测脚本名”，再决定生成新脚本或使用已有脚本实例。";
            }

            if (scriptType == null)
            {
                return "项目中还没有该配置脚本，可以刷新预览或生成脚本。";
            }

            if (sceneComponent == null)
            {
                return "项目中已存在该配置脚本，但场景中还没有实例，可生成场景实例并赋值。";
            }

            return structureMatches
                ? "场景中已存在匹配当前配置结构的同名配置实例，脚本名已锁定。"
                : "场景中存在同名脚本实例，但它与当前配置结构不匹配，可以修改配置脚本名。";
        }

        private void DrawConfigContentEditor()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("配置内容编辑", EditorStyles.boldLabel);
            if (currentConfig == null || editingConfig == null)
            {
                EditorGUILayout.HelpBox("请先选择配置结构。", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            foreach (CustomConfigData config in editingConfig.singleCustomConfigs)
            {
                string configName = string.IsNullOrEmpty(config.configName) ? "未命名配置" : config.configName;
                config.value ??= new CustomModelInstanceData();
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField(configName, EditorStyles.boldLabel);
                foreach (CustomConfigEntryData entry in config.entries)
                {
                    DrawConfigEntryEditor(entry);
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawConfigEntryEditor(CustomConfigEntryData entry)
        {
            string entryName = string.IsNullOrEmpty(entry.entryName) ? entry.modelTypeName : entry.entryName;
            bool isExpanded = GetScriptEntryFoldout(entry);
            Rect borderRect = BeginConfigEntryFrame(isExpanded);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            isExpanded = EditorGUILayout.Foldout(isExpanded, entryName, true);
            SetScriptEntryFoldout(entry, isExpanded);
            GUILayout.Label(entry.entryKind == CustomConfigEntryKind.Model ? "单项" : "列表", EditorStyles.miniLabel, GUILayout.Width(36));
            EditorGUILayout.EndHorizontal();
            if (isExpanded)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("Inspector名字", entry.entryName);
                }
                Color originalContentColor = GUI.contentColor;
                if (lockedScriptParameterNames)
                {
                    GUI.contentColor = Color.green;
                }
                using (new EditorGUI.DisabledScope(lockedScriptParameterNames))
                {
                    entry.scriptParameterName = EditorGUILayout.TextField("生成脚本参数名称", entry.scriptParameterName);
                }
                GUI.contentColor = originalContentColor;
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("Model", entry.modelTypeName);
                }

                if (entry.entryKind == CustomConfigEntryKind.Model)
                {
                    DrawSingleObjectAssignSection(entry);
                    DrawModelInstanceEditor(entry.value, entry.modelTypeName);
                }
                else
                {
                    DrawModelListEditor(entry, entryName);
                }
            }
            EditorGUILayout.EndVertical();
            EndConfigEntryFrame(borderRect, isExpanded);
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

        private bool GetScriptEntryFoldout(CustomConfigEntryData entry)
        {
            if (!scriptEntryFoldouts.TryGetValue(entry, out bool isExpanded))
            {
                isExpanded = true;
                scriptEntryFoldouts[entry] = true;
            }
            return isExpanded;
        }

        private void SetScriptEntryFoldout(CustomConfigEntryData entry, bool isExpanded)
        {
            scriptEntryFoldouts[entry] = isExpanded;
        }

        private void DrawSingleObjectAssignSection(CustomConfigEntryData entry)
        {
            SingleObjectState state = GetSingleObjectState(entry);
            GameObject selectedObject = Selection.activeGameObject;
            if (state.target != selectedObject)
            {
                state.target = selectedObject;
                state.propertyOptions = state.target == null ? new List<PropertyOption>() : GetPropertyOptions(state.target);
                state.selectedPropertyByField.Clear();
                state.optionCacheByField.Clear();
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("从当前选中物体赋值", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("当前目标物体", state.target, typeof(GameObject), true);
            }

            if (state.target == null)
            {
                EditorGUILayout.HelpBox("请在 Hierarchy 面板选中一个场景物体。", MessageType.Info);
            }
            else if (state.propertyOptions.Count > 0)
            {
                DrawSingleObjectMappings(entry.value, state);
                if (GUILayout.Button("应用到当前 Model", GUILayout.Height(24)))
                {
                    ApplySingleObjectMappings(entry.value, state.target, state);
                    Repaint();
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSingleObjectMappings(CustomModelInstanceData instance, SingleObjectState state)
        {
            foreach (CustomFieldData field in instance.fields)
            {
                if (field.fieldType == FieldType.Model)
                {
                    DrawSingleObjectMappings(field.modelValue, state);
                    continue;
                }

                List<PropertyOption> options = state.propertyOptions.Where(option => IsPropertyCompatible(GetCustomFieldRuntimeType(field), option.valueType)).ToList();
                options.Insert(0, PropertyOption.KeepUnchanged);

                if (!state.selectedPropertyByField.TryGetValue(field.fieldName, out int selectedIndex))
                {
                    selectedIndex = 0;
                }

                selectedIndex = Mathf.Clamp(selectedIndex, 0, options.Count - 1);
                state.selectedPropertyByField[field.fieldName] = EditorGUILayout.Popup(field.fieldName, selectedIndex, options.Select(option => option.displayName).ToArray());
                state.optionCacheByField[field.fieldName] = options;
            }
        }

        private void ApplySingleObjectMappings(CustomModelInstanceData instance, GameObject target, SingleObjectState state)
        {
            foreach (CustomFieldData field in instance.fields)
            {
                if (field.fieldType == FieldType.Model)
                {
                    ApplySingleObjectMappings(field.modelValue, target, state);
                    continue;
                }

                if (!state.optionCacheByField.TryGetValue(field.fieldName, out List<PropertyOption> options) || !state.selectedPropertyByField.TryGetValue(field.fieldName, out int selectedIndex) || options.Count == 0)
                {
                    continue;
                }

                selectedIndex = Mathf.Clamp(selectedIndex, 0, options.Count - 1);
                PropertyOption option = options[selectedIndex];
                if (option == PropertyOption.KeepUnchanged)
                {
                    continue;
                }

                AssignFieldValue(field, option.getter(target));
            }
        }

        private void DrawModelListEditor(CustomConfigEntryData entry, string entryName)
        {
            EditorGUILayout.LabelField($"{entryName} 列表项: {entry.configs.Count}");

            DrawBatchSection(entry);

            EditorGUI.indentLevel++;
            for (int i = 0; i < entry.configs.Count; i++)
            {
                CustomConfigData item = entry.configs[i];
                EditorGUILayout.BeginVertical("box");
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("项名称", item.configName);
                }
                DrawModelInstanceEditor(item.value, item.modelTypeName);
                EditorGUILayout.EndVertical();
            }
            EditorGUI.indentLevel--;
        }

        private void DrawModelInstanceEditor(CustomModelInstanceData instance, string modelTypeName)
        {
            if (instance == null)
            {
                return;
            }

            foreach (CustomFieldData field in instance.fields)
            {
                DrawFieldEditor(field);
            }
        }

        private void DrawFieldEditor(CustomFieldData field)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(field.fieldName, GUILayout.Width(fieldNameColumnWidth));
            DrawColumnResizer(ref fieldNameColumnWidth, 80f, 300f);
            switch (field.fieldType)
            {
                case FieldType.String:
                    field.stringValue = EditorGUILayout.TextField(field.stringValue, GUILayout.Width(fieldValueColumnWidth));
                    DrawColumnResizer(ref fieldValueColumnWidth, 140f, 520f);
                    break;
                case FieldType.Int:
                    field.intValue = EditorGUILayout.IntField(field.intValue);
                    break;
                case FieldType.Float:
                    field.floatValue = EditorGUILayout.FloatField(field.floatValue);
                    break;
                case FieldType.Bool:
                    field.boolValue = EditorGUILayout.Toggle(field.boolValue);
                    break;
                case FieldType.Vector3:
                    field.vector3Value = EditorGUILayout.Vector3Field("", field.vector3Value);
                    break;
                case FieldType.GameObject:
                    field.gameObjectValue = (GameObject)EditorGUILayout.ObjectField(field.gameObjectValue, typeof(GameObject), true);
                    break;
                case FieldType.Material:
                    field.materialValue = (Material)EditorGUILayout.ObjectField(field.materialValue, typeof(Material), false);
                    break;
                case FieldType.Texture:
                    field.textureValue = (Texture)EditorGUILayout.ObjectField(field.textureValue, typeof(Texture), false);
                    break;
                case FieldType.Model:
                    EditorGUILayout.LabelField(field.modelTypeName);
                    break;
            }
            EditorGUILayout.EndHorizontal();

            if (field.fieldType == FieldType.Model)
            {
                EditorGUI.indentLevel++;
                DrawModelInstanceEditor(field.modelValue, field.modelTypeName);
                EditorGUI.indentLevel--;
            }
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

        private void DrawBatchSection(CustomConfigEntryData entry)
        {
            BatchState state = GetBatchState(entry);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("批量处理", EditorStyles.boldLabel);
            state.mode = (BatchMode)EditorGUILayout.EnumPopup("处理方式", state.mode);
            if (state.mode == BatchMode.Prefix)
            {
                state.prefix = EditorGUILayout.TextField("前缀", state.prefix);
            }
            else if (state.mode == BatchMode.Suffix)
            {
                state.suffix = EditorGUILayout.TextField("后缀", state.suffix);
            }
            else
            {
                state.parent = (GameObject)EditorGUILayout.ObjectField("父节点", state.parent, typeof(GameObject), true);
            }

            if (GUILayout.Button("收集物体并提取公共属性", GUILayout.Height(24)))
            {
                if (ValidateBatchState(state, out string errorMessage))
                {
                    ClearBottomMessage();
                    state.objects = CollectObjects(state);
                    state.propertyOptions = BuildPropertyOptions(state.objects);
                }
                else
                {
                    ShowBottomError(errorMessage);
                    state.objects.Clear();
                    state.propertyOptions.Clear();
                }
            }

            EditorGUILayout.LabelField($"已收集物体: {state.objects.Count}");
            if (state.objects.Count > 0)
            {
                DrawBatchMappings(entry, state);
                if (GUILayout.Button("应用批量赋值", GUILayout.Height(24)))
                {
                    ApplyBatch(entry, state);
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawBatchMappings(CustomConfigEntryData entry, BatchState state)
        {
            CustomModelInstanceData template = entry.configs.FirstOrDefault()?.value ?? CreateModelInstance(entry.modelTypeName);
            foreach (CustomFieldData field in template.fields)
            {
                List<PropertyOption> options = state.propertyOptions.Where(option => IsPropertyCompatible(GetCustomFieldRuntimeType(field), option.valueType)).ToList();
                options.Insert(0, PropertyOption.KeepUnchanged);

                if (!state.selectedPropertyByField.TryGetValue(field.fieldName, out int selectedIndex))
                {
                    selectedIndex = 0;
                }

                selectedIndex = Mathf.Clamp(selectedIndex, 0, options.Count - 1);
                string[] labels = options.Select(option => option.displayName).ToArray();
                state.selectedPropertyByField[field.fieldName] = EditorGUILayout.Popup(field.fieldName, selectedIndex, labels);
                state.optionCacheByField[field.fieldName] = options;
            }
        }

        private bool ValidateBatchState(BatchState state, out string errorMessage)
        {
            if (state.mode == BatchMode.Prefix && string.IsNullOrWhiteSpace(state.prefix))
            {
                errorMessage = "前缀不能为空，请输入前缀后再提取公共属性。";
                return false;
            }

            if (state.mode == BatchMode.Suffix && string.IsNullOrWhiteSpace(state.suffix))
            {
                errorMessage = "后缀不能为空，请输入后缀后再提取公共属性。";
                return false;
            }

            if (state.mode == BatchMode.Children && state.parent == null)
            {
                errorMessage = "父节点不能为空，请选择父节点后再提取公共属性。";
                return false;
            }

            errorMessage = "";
            return true;
        }

        private List<GameObject> CollectObjects(BatchState state)
        {
            IEnumerable<GameObject> objects = FindObjectsByType<GameObject>(FindObjectsSortMode.None).Where(item => item.scene.IsValid());
            if (state.mode == BatchMode.Prefix)
            {
                objects = objects.Where(item => item.name.StartsWith(state.prefix));
            }
            else if (state.mode == BatchMode.Suffix)
            {
                objects = objects.Where(item => item.name.EndsWith(state.suffix));
            }
            else if (state.parent != null)
            {
                objects = state.parent.GetComponentsInChildren<Transform>(true).Select(item => item.gameObject).Where(item => item != state.parent);
            }
            else
            {
                objects = Enumerable.Empty<GameObject>();
            }

            return objects.Distinct().OrderBy(item => item.name).ToList();
        }

        private List<PropertyOption> BuildPropertyOptions(List<GameObject> objects)
        {
            if (objects.Count == 0)
            {
                return new List<PropertyOption>();
            }

            Dictionary<string, PropertyOption> commonOptions = GetPropertyOptions(objects[0]).ToDictionary(option => option.key, option => option);
            foreach (GameObject obj in objects.Skip(1))
            {
                HashSet<string> keys = GetPropertyOptions(obj).Select(option => option.key).ToHashSet();
                foreach (string key in commonOptions.Keys.ToList())
                {
                    if (!keys.Contains(key))
                    {
                        commonOptions.Remove(key);
                    }
                }
            }

            return commonOptions.Values.OrderBy(option => option.displayName).ToList();
        }

        private List<PropertyOption> GetPropertyOptions(GameObject obj)
        {
            var options = new List<PropertyOption>
            {
                new PropertyOption("GameObject/name", "GameObject.name", typeof(string), target => target.name),
                new PropertyOption("GameObject/self", "GameObject", typeof(GameObject), target => target),
                new PropertyOption("Transform/position", "Transform.position", typeof(Vector3), target => target.transform.position),
                new PropertyOption("Transform/localPosition", "Transform.localPosition", typeof(Vector3), target => target.transform.localPosition),
                new PropertyOption("Transform/eulerAngles", "Transform.eulerAngles", typeof(Vector3), target => target.transform.eulerAngles),
                new PropertyOption("Transform/localEulerAngles", "Transform.localEulerAngles", typeof(Vector3), target => target.transform.localEulerAngles)
            };

            foreach (Component component in obj.GetComponents<Component>().Where(item => item != null))
            {
                Type componentType = component.GetType();
                foreach (PropertyInfo property in componentType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    {
                        continue;
                    }

                    string key = componentType.FullName + "/" + property.Name;
                    options.Add(new PropertyOption(key, componentType.Name + "." + property.Name, property.PropertyType, target =>
                    {
                        Component targetComponent = target.GetComponent(componentType);
                        return targetComponent == null ? null : property.GetValue(targetComponent);
                    }));
                }

                foreach (FieldInfo field in componentType.GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    string key = componentType.FullName + "/" + field.Name;
                    options.Add(new PropertyOption(key, componentType.Name + "." + field.Name, field.FieldType, target =>
                    {
                        Component targetComponent = target.GetComponent(componentType);
                        return targetComponent == null ? null : field.GetValue(targetComponent);
                    }));
                }
            }

            return options.GroupBy(option => option.key).Select(group => group.First()).ToList();
        }

        private static bool IsPropertyCompatible(Type fieldType, Type valueType)
        {
            if (fieldType == null || valueType == null)
            {
                return false;
            }

            return fieldType.IsAssignableFrom(valueType) || valueType.IsAssignableFrom(fieldType);
        }

        private static Type GetCustomFieldRuntimeType(CustomFieldData field)
        {
            if (!string.IsNullOrEmpty(field.sourceTypeName))
            {
                Type sourceType = Type.GetType(field.sourceTypeName);
                if (sourceType != null)
                {
                    return sourceType;
                }
            }

            switch (field.fieldType)
            {
                case FieldType.String: return typeof(string);
                case FieldType.Int: return typeof(int);
                case FieldType.Float: return typeof(float);
                case FieldType.Bool: return typeof(bool);
                case FieldType.Vector3: return typeof(Vector3);
                case FieldType.GameObject: return typeof(GameObject);
                case FieldType.Material: return typeof(Material);
                case FieldType.Texture: return typeof(Texture);
                default: return null;
            }
        }

        private void ApplyBatch(CustomConfigEntryData entry, BatchState state)
        {
            int count = entry.configs.Count;
            for (int i = 0; i < count; i++)
            {
                CustomConfigData item = entry.configs[i];
                item.modelTypeName = entry.modelTypeName;
                item.value ??= CreateModelInstance(entry.modelTypeName);
                if (i < state.objects.Count)
                {
                    ApplyBatchMappings(item.value, state.objects[i], state);
                }
                else
                {
                    item.value = CreateModelInstance(entry.modelTypeName);
                }
            }
            Repaint();
        }

        private void ApplyBatchMappings(CustomModelInstanceData instance, GameObject obj, BatchState state)
        {
            foreach (CustomFieldData field in instance.fields)
            {
                if (field.fieldType == FieldType.Model)
                {
                    ApplyBatchMappings(field.modelValue, obj, state);
                    continue;
                }

                if (!state.optionCacheByField.TryGetValue(field.fieldName, out List<PropertyOption> options) || !state.selectedPropertyByField.TryGetValue(field.fieldName, out int selectedIndex) || options.Count == 0)
                {
                    continue;
                }

                selectedIndex = Mathf.Clamp(selectedIndex, 0, options.Count - 1);
                PropertyOption option = options[selectedIndex];
                if (option == PropertyOption.KeepUnchanged)
                {
                    continue;
                }

                AssignFieldValue(field, option.getter(obj));
            }
        }

        private static void AssignFieldValue(CustomFieldData field, object value)
        {
            switch (field.fieldType)
            {
                case FieldType.String:
                    field.stringValue = value as string ?? "";
                    break;
                case FieldType.Int:
                    field.intValue = value is int intValue ? intValue : 0;
                    break;
                case FieldType.Float:
                    field.floatValue = value is float floatValue ? floatValue : 0f;
                    break;
                case FieldType.Bool:
                    field.boolValue = value is bool boolValue && boolValue;
                    break;
                case FieldType.Vector3:
                    field.vector3Value = value is Vector3 vector3Value ? vector3Value : Vector3.zero;
                    break;
                case FieldType.GameObject:
                    field.gameObjectValue = value as GameObject;
                    break;
                case FieldType.Material:
                    field.materialValue = value as Material;
                    break;
                case FieldType.Texture:
                    field.textureValue = value as Texture;
                    break;
                case FieldType.Model:
                    break;
            }
        }

        private BatchState GetBatchState(CustomConfigEntryData entry)
        {
            if (!batchStates.TryGetValue(entry, out BatchState state))
            {
                state = new BatchState();
                batchStates[entry] = state;
            }
            return state;
        }

        private SingleObjectState GetSingleObjectState(CustomConfigEntryData entry)
        {
            if (!singleObjectStates.TryGetValue(entry, out SingleObjectState state))
            {
                state = new SingleObjectState();
                singleObjectStates[entry] = state;
            }
            return state;
        }

        private void DrawScriptGeneration()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("生成场景配置脚本", EditorStyles.boldLabel);
            if (!HasCurrentScriptCheck)
            {
                EditorGUILayout.HelpBox("请先点击顶部的“检测脚本名”。", MessageType.Info);
            }
            else if (cachedConfigScriptType == null)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("刷新预览", GUILayout.Height(28)))
                {
                    GeneratePreview();
                }
                if (GUILayout.Button("生成脚本并添加到场景", GUILayout.Height(28)))
                {
                    ApplyConfigScriptWorkflow();
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("项目中已存在该配置脚本，生成和预览功能已隐藏。请在顶部场景实例区域操作。", MessageType.Info);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawExistingScriptWorkflow(Type scriptType, List<Component> sceneComponents, Component sceneComponent, bool structureMatches)
        {
            if (sceneComponent == null)
            {
                if (!IsScriptTypeStructureMatching(scriptType, out string mismatchMessage))
                {
                    EditorGUILayout.HelpBox(mismatchMessage, MessageType.Warning);
                    return;
                }

                if (GUILayout.Button("生成场景实例并赋值", GUILayout.Height(28)))
                {
                    CreateSceneInstanceForExistingScript(scriptType);
                }
                return;
            }

            DrawConfigComponentSelector(sceneComponents, scriptType);

            if (!structureMatches)
            {
                IsComponentStructureMatching(sceneComponent, out string mismatchMessage);
                EditorGUILayout.HelpBox(mismatchMessage, MessageType.Warning);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("场景实例", sceneComponent, scriptType, true);
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("读取实例字段", GUILayout.Height(28)))
            {
                LoadComponentDataToEditingConfig(sceneComponent);
            }

            if (GUILayout.Button("应用到实例", GUILayout.Height(28)))
            {
                ApplyEditingConfigToComponent(sceneComponent);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawConfigComponentSelector(List<Component> sceneComponents, Type scriptType)
        {
            selectedConfigComponentIndex = Mathf.Clamp(selectedConfigComponentIndex, 0, sceneComponents.Count - 1);
            if (sceneComponents.Count <= 1)
            {
                return;
            }

            string[] options = sceneComponents.Select(component => component.gameObject.name).ToArray();
            int selectedIndex = EditorGUILayout.Popup("选择场景实例", selectedConfigComponentIndex, options);
            if (selectedIndex != selectedConfigComponentIndex)
            {
                selectedConfigComponentIndex = selectedIndex;
                ClearStructureMatchCache();
            }
        }

        private void DrawPreview()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("代码预览", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(generatedPreview, GUILayout.MinHeight(360));
            EditorGUILayout.EndVertical();
        }

        private void DrawBottomMessage()
        {
            if (string.IsNullOrEmpty(bottomMessage))
            {
                return;
            }

            if (EditorApplication.timeSinceStartup >= bottomMessageExpireTime)
            {
                ClearBottomMessage();
                return;
            }

            GUIStyle style = new GUIStyle(EditorStyles.helpBox)
            {
                normal = { textColor = bottomMessageIsError ? Color.red : Color.green },
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            EditorGUILayout.LabelField(bottomMessage, style, GUILayout.MinHeight(28));
            Repaint();
        }

        private void ShowBottomError(string message)
        {
            ShowBottomMessage(message, true);
        }

        private void ShowBottomSuccess(string message)
        {
            ShowBottomMessage(message, false);
        }

        private void ShowBottomMessage(string message, bool isError)
        {
            bottomMessage = message;
            bottomMessageIsError = isError;
            bottomMessageExpireTime = EditorApplication.timeSinceStartup + 3d;
        }

        private void ClearBottomMessage()
        {
            bottomMessage = "";
            bottomMessageIsError = false;
            bottomMessageExpireTime = 0d;
        }

        private void GeneratePreview()
        {
            if (!ValidateCheckedConfig(out string errorMessage))
            {
                ShowBottomError(errorMessage);
                return;
            }

            ClearBottomMessage();
            generatedPreview = GenerateConfigScript(configScriptName);
        }

        private void ApplyConfigScriptWorkflow()
        {
            if (!ValidateCheckedConfig(out string errorMessage))
            {
                ShowBottomError(errorMessage);
                return;
            }

            ClearBottomMessage();
            GenerateAndAddToScene();
        }

        private void GenerateAndAddToScene()
        {
            string className = configScriptName;
            string savePath = EditorUtility.SaveFilePanelInProject("保存配置脚本", className, "cs", "请选择配置脚本保存位置");
            if (string.IsNullOrEmpty(savePath))
            {
                return;
            }

            generatedPreview = GenerateConfigScript(className);
            File.WriteAllText(savePath, generatedPreview);

            string pendingObjectName = "Temp_" + className + "_" + Guid.NewGuid().ToString("N");
            GameObject tempObj = new GameObject(pendingObjectName);
            GeneratedConfigBuffer buffer = tempObj.AddComponent<GeneratedConfigBuffer>();
            buffer.Capture(editingConfig);
            Undo.RegisterCreatedObjectUndo(tempObj, "Create scene config");
            EditorUtility.SetDirty(buffer);
            EditorUtility.SetDirty(tempObj);
            EditorSceneManager.MarkSceneDirty(tempObj.scene);

            SessionState.SetBool(PendingConfigScriptPathKey + ".Active", true);
            SessionState.SetString(PendingConfigScriptPathKey, savePath);
            SessionState.SetString(PendingConfigClassNameKey, className);
            SessionState.SetString(PendingObjectNameKey, pendingObjectName);
            SessionState.SetInt(PendingRetryCountKey, 0);

            AssetDatabase.ImportAsset(savePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
            EditorApplication.delayCall += CompletePendingAddToScene;
        }

        private static void CompletePendingAddToScene()
        {
            if (!SessionState.GetBool(PendingConfigScriptPathKey + ".Active", false))
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += CompletePendingAddToScene;
                return;
            }

            string savePath = SessionState.GetString(PendingConfigScriptPathKey, "");
            string className = SessionState.GetString(PendingConfigClassNameKey, "");
            string objectName = SessionState.GetString(PendingObjectNameKey, "");
            GameObject target = GameObject.Find(objectName);
            if (target == null)
            {
                ClearPendingAddToScene();
                return;
            }

            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(savePath);
            Type scriptType = script != null ? script.GetClass() : null;
            if (scriptType == null || !typeof(Component).IsAssignableFrom(scriptType))
            {
                int retryCount = SessionState.GetInt(PendingRetryCountKey, 0) + 1;
                SessionState.SetInt(PendingRetryCountKey, retryCount);
                if (retryCount >= PendingMaxRetries)
                {
                    ClearPendingAddToScene();
                    DestroyImmediate(target);
                    EditorUtility.DisplayDialog("警告", $"脚本已生成，但 Unity 未能解析组件类型。请检查 Console。\n脚本保存位置: {savePath}", "确定");
                    return;
                }

                EditorApplication.delayCall += CompletePendingAddToScene;
                return;
            }

            target.name = className;
            Component component = target.GetComponent(scriptType) ?? target.AddComponent(scriptType);
            GeneratedConfigBuffer buffer = target.GetComponent<GeneratedConfigBuffer>();
            if (buffer != null)
            {
                ApplyGeneratedConfigData(component, buffer.singleCustomConfigs);
            }
            if (buffer != null)
            {
                DestroyImmediate(buffer);
            }

            EditorSceneManager.MarkSceneDirty(target.scene);
            Selection.activeGameObject = target;
            EditorGUIUtility.PingObject(target);
            RefreshOpenEditorAfterPendingAdd(component);
            ClearPendingAddToScene();
            EditorUtility.DisplayDialog("成功", $"配置脚本已生成并添加到场景。\n脚本保存位置: {savePath}", "确定");
        }

        private static void RefreshOpenEditorAfterPendingAdd(Component component)
        {
            if (!HasOpenInstances<ConfigScriptEditor>())
            {
                return;
            }

            ConfigScriptEditor window = GetWindow<ConfigScriptEditor>("配置脚本编辑器");
            window.configScriptName = component.GetType().Name;
            window.checkedConfigScriptName = window.configScriptName;
            window.hasCheckedConfigScriptName = true;
            window.cachedConfigScriptType = component.GetType();
            window.lockedScriptParameterNames = true;
            window.ApplyExistingScriptFieldNames(component.GetType());
            window.cachedConfigComponents = new List<Component> { component };
            window.cachedConfigComponentsSearched = true;
            window.selectedConfigComponentIndex = 0;
            window.ClearStructureMatchCache();
            window.Repaint();
        }

        private void CheckConfigScriptName()
        {
            if (string.IsNullOrWhiteSpace(configScriptName))
            {
                ShowBottomError("配置脚本名为空，请输入要检测的脚本类名。");
                return;
            }

            if (!IsValidCSharpIdentifier(configScriptName))
            {
                ShowBottomError($"配置脚本名“{configScriptName}”不是合法 C# 类名，请使用英文、数字或下划线命名，且不能以数字开头。");
                return;
            }

            checkedConfigScriptName = configScriptName;
            hasCheckedConfigScriptName = true;
            ClearConfigComponentCache();
            cachedConfigScriptType = TypeCache.GetTypesDerivedFrom<Component>()
                .FirstOrDefault(type => type.Name == checkedConfigScriptName);
            if (cachedConfigScriptType == null)
            {
                lockedScriptParameterNames = false;
                ClearScriptParameterNames();
            }
            else
            {
                lockedScriptParameterNames = true;
                ApplyExistingScriptFieldNames(cachedConfigScriptType);
            }
            ClearBottomMessage();
        }

        private void ApplyExistingScriptFieldNames(Type scriptType)
        {
            foreach (CustomConfigData config in editingConfig.singleCustomConfigs)
            {
                foreach (CustomConfigEntryData entry in config.entries)
                {
                    if (TryFindExistingScriptField(scriptType, entry, out FieldInfo field))
                    {
                        entry.scriptParameterName = field.Name;
                    }
                }
            }
        }

        private bool TryFindExistingScriptField(Type scriptType, CustomConfigEntryData entry, out FieldInfo result)
        {
            Type modelType = GetModelRuntimeType(entry.modelTypeName);
            Type expectedType = entry.entryKind == CustomConfigEntryKind.Model ? modelType : modelType == null ? null : typeof(List<>).MakeGenericType(modelType);
            result = expectedType == null ? null : scriptType.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(field => field.FieldType == expectedType);
            return result != null;
        }

        private void ClearScriptParameterNames()
        {
            foreach (CustomConfigData config in editingConfig.singleCustomConfigs)
            {
                foreach (CustomConfigEntryData entry in config.entries)
                {
                    entry.scriptParameterName = "";
                }
            }
        }

        private void ClearCheckedScriptState()
        {
            checkedConfigScriptName = "";
            hasCheckedConfigScriptName = false;
            cachedConfigScriptType = null;
            lockedScriptParameterNames = false;
            ClearConfigComponentCache();
        }

        private List<Component> GetExistingConfigComponents(Type scriptType)
        {
            if (cachedConfigComponentsSearched)
            {
                return cachedConfigComponents;
            }

            cachedConfigComponents = FindObjectsByType(scriptType, FindObjectsSortMode.None)
                .OfType<Component>()
                .Where(component => component.gameObject.scene.IsValid())
                .OrderBy(component => component.gameObject.name)
                .ToList();
            cachedConfigComponentsSearched = true;
            selectedConfigComponentIndex = Mathf.Clamp(selectedConfigComponentIndex, 0, cachedConfigComponents.Count - 1);
            return cachedConfigComponents;
        }

        private Component GetSelectedConfigComponent(List<Component> components)
        {
            if (components == null || components.Count == 0)
            {
                return null;
            }

            selectedConfigComponentIndex = Mathf.Clamp(selectedConfigComponentIndex, 0, components.Count - 1);
            return components[selectedConfigComponentIndex];
        }

        private void ClearConfigComponentCache()
        {
            cachedConfigComponents.Clear();
            cachedConfigComponentsSearched = false;
            selectedConfigComponentIndex = 0;
            ClearStructureMatchCache();
        }

        private void CreateSceneInstanceForExistingScript(Type scriptType)
        {
            if (!ValidateCheckedConfig(out string errorMessage))
            {
                ShowBottomError(errorMessage);
                return;
            }

            if (!IsScriptTypeStructureMatching(scriptType, out string mismatchMessage))
            {
                ShowBottomError(mismatchMessage);
                return;
            }

            ClearBottomMessage();
            GameObject obj = new GameObject(configScriptName);
            Undo.RegisterCreatedObjectUndo(obj, "Create scene config instance");
            Component component = obj.AddComponent(scriptType);
            ApplyGeneratedConfigData(component, editingConfig.singleCustomConfigs);
            EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(obj.scene);
            Selection.activeGameObject = obj;
            EditorGUIUtility.PingObject(obj);
            cachedConfigComponents = new List<Component> { component };
            cachedConfigComponentsSearched = true;
            selectedConfigComponentIndex = 0;
            ClearStructureMatchCache();
            Repaint();
        }

        private void ApplyEditingConfigToComponent(Component component)
        {
            if (!ValidateCheckedConfig(out string errorMessage))
            {
                ShowBottomError(errorMessage);
                return;
            }

            ClearBottomMessage();
            ApplyGeneratedConfigData(component, editingConfig.singleCustomConfigs);
            EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
            Selection.activeGameObject = component.gameObject;
            EditorGUIUtility.PingObject(component.gameObject);
            ShowBottomSuccess($"已应用到场景实例: {component.gameObject.name}");
        }

        private static void ClearPendingAddToScene()
        {
            SessionState.EraseBool(PendingConfigScriptPathKey + ".Active");
            SessionState.EraseString(PendingConfigScriptPathKey);
            SessionState.EraseString(PendingConfigClassNameKey);
            SessionState.EraseString(PendingObjectNameKey);
            SessionState.EraseInt(PendingRetryCountKey);
        }

        private bool ValidateCheckedConfig(out string errorMessage)
        {
            if (!ValidateConfig(out errorMessage))
            {
                return false;
            }

            if (!HasCurrentScriptCheck)
            {
                errorMessage = "配置脚本名已修改，请先点击“检测脚本名”确认当前脚本状态。";
                return false;
            }

            return true;
        }

        private bool ValidateConfig(out string errorMessage)
        {
            if (currentConfig == null || editingConfig == null)
            {
                errorMessage = "请先选择配置结构。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(configScriptName))
            {
                errorMessage = "配置脚本名为空，请输入一个用于生成脚本的类名。";
                return false;
            }

            if (!IsValidCSharpIdentifier(configScriptName))
            {
                errorMessage = $"配置脚本名“{configScriptName}”不是合法 C# 类名，请使用英文、数字或下划线命名，且不能以数字开头。";
                return false;
            }

            foreach (CustomConfigData config in editingConfig.singleCustomConfigs)
            {
                foreach (CustomConfigEntryData entry in config.entries)
                {
                    string inspectorName = string.IsNullOrWhiteSpace(entry.entryName) ? "未命名配置项" : entry.entryName;
                    if (string.IsNullOrWhiteSpace(entry.scriptParameterName))
                    {
                        errorMessage = $"{inspectorName} 的生成脚本参数名称为空，请填写一个用于脚本字段的参数名。";
                        return false;
                    }

                    if (!IsValidCSharpIdentifier(GetGeneratedEntryFieldName(entry)))
                    {
                        errorMessage = $"{inspectorName} 的生成脚本参数名称不能生成合法字段名，请使用字母、数字或下划线命名。";
                        return false;
                    }
                }
            }

            foreach (CustomModelData model in currentConfig.customModels)
            {
                if (!IsValidCSharpIdentifier(GetSafeClassName(model.modelName)))
                {
                    errorMessage = $"Model 名称 {model.modelName} 不能生成合法类型名。";
                    return false;
                }

                foreach (CustomFieldData field in model.fields)
                {
                    if (!IsValidCSharpIdentifier(field.fieldName))
                    {
                        errorMessage = $"字段名称 {field.fieldName} 不是合法字段名。";
                        return false;
                    }
                }
            }

            errorMessage = "";
            return true;
        }

        private string GenerateConfigScript(string className)
        {
            var sb = new StringBuilder();
            var usedFieldNames = new HashSet<string>();
            var modelTypeNames = GetModelTypeNames();

            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"public class {className} : MonoBehaviour");
            sb.AppendLine("{");

            foreach (CustomConfigData config in editingConfig.singleCustomConfigs)
            {
                foreach (CustomConfigEntryData entry in config.entries)
                {
                    if (!modelTypeNames.TryGetValue(entry.modelTypeName, out string modelClassName))
                    {
                        continue;
                    }

                    string fieldName = GetUniqueGeneratedName(GetGeneratedEntryFieldName(entry), usedFieldNames);
                    string headerName = string.IsNullOrEmpty(entry.entryName) ? entry.modelTypeName : entry.entryName;
                    sb.AppendLine($"    [Header(\"{EscapeStringLiteral(headerName)}\")]");
                    if (entry.entryKind == CustomConfigEntryKind.Model)
                    {
                        sb.AppendLine($"    public {modelClassName} {fieldName} = new {modelClassName}();");
                    }
                    else
                    {
                        sb.AppendLine($"    public List<{modelClassName}> {fieldName} = new List<{modelClassName}>();");
                    }
                    sb.AppendLine();
                }
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        private bool IsComponentStructureMatching(Component component, out string message)
        {
            if (component == null)
            {
                message = "场景实例为空。";
                return false;
            }

            return IsScriptTypeStructureMatching(component.GetType(), out message);
        }

        private bool IsScriptTypeStructureMatching(Type scriptType, out string message)
        {
            if (scriptType == null)
            {
                message = "配置脚本类型为空。";
                return false;
            }

            string cacheKey = GetStructureMatchCacheKey(scriptType);
            if (cachedStructureMatchKey == cacheKey)
            {
                message = cachedStructureMismatchMessage;
                return cachedStructureMatches;
            }

            foreach ((string fieldName, Type fieldType) in GetExpectedConfigFields())
            {
                FieldInfo field = scriptType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
                if (field == null)
                {
                    message = $"脚本 {scriptType.Name} 中缺少字段 {fieldName}，与当前配置结构不匹配，请更换配置脚本名或重新生成匹配脚本。";
                    CacheStructureMatchResult(cacheKey, false, message);
                    return false;
                }

                if (field.FieldType != fieldType)
                {
                    message = $"脚本 {scriptType.Name} 的字段 {fieldName} 类型不匹配，当前为 {field.FieldType.Name}，需要 {fieldType.Name}。请更换配置脚本名或重新生成匹配脚本。";
                    CacheStructureMatchResult(cacheKey, false, message);
                    return false;
                }
            }

            message = "";
            CacheStructureMatchResult(cacheKey, true, message);
            return true;
        }

        private string GetStructureMatchCacheKey(Type scriptType)
        {
            return currentConfig.GetInstanceID() + ":" + scriptType.FullName + ":" + configScriptName + ":" + editingConfig.singleCustomConfigs.Sum(config => config.entries.Count);
        }

        private void CacheStructureMatchResult(string cacheKey, bool matches, string message)
        {
            cachedStructureMatchKey = cacheKey;
            cachedStructureMatches = matches;
            cachedStructureMismatchMessage = message;
        }

        private void ClearStructureMatchCache()
        {
            cachedStructureMatchKey = "";
            cachedStructureMatches = false;
            cachedStructureMismatchMessage = "";
        }

        private List<(string fieldName, Type fieldType)> GetExpectedConfigFields()
        {
            var result = new List<(string fieldName, Type fieldType)>();
            foreach (CustomConfigData config in editingConfig.singleCustomConfigs)
            {
                foreach (CustomConfigEntryData entry in config.entries)
                {
                    Type modelType = GetModelRuntimeType(entry.modelTypeName);
                    if (modelType == null)
                    {
                        continue;
                    }

                    Type fieldType = entry.entryKind == CustomConfigEntryKind.Model ? modelType : typeof(List<>).MakeGenericType(modelType);
                    result.Add((GetGeneratedEntryFieldName(entry), fieldType));
                }
            }
            return result;
        }

        private Type GetModelRuntimeType(string modelName)
        {
            CustomModelData model = currentConfig.customModels.FirstOrDefault(item => item.modelName == modelName);
            if (model == null)
            {
                return null;
            }

            string typeName = string.IsNullOrEmpty(model.sourceTypeName) ? GetSafeClassName(model.modelName) : model.sourceTypeName;
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
                })
                .FirstOrDefault(type => type.FullName == typeName || type.Name == typeName);
        }

        private static void ApplyGeneratedConfigData(Component component, List<CustomConfigData> configs)
        {
            if (component == null || configs == null)
            {
                return;
            }

            Type componentType = component.GetType();
            foreach (CustomConfigData config in configs)
            {
                foreach (CustomConfigEntryData entry in config.entries)
                {
                    FieldInfo targetField = componentType.GetField(GetGeneratedEntryFieldName(entry), BindingFlags.Instance | BindingFlags.Public);
                    if (targetField == null)
                    {
                        continue;
                    }

                    if (entry.entryKind == CustomConfigEntryKind.Model)
                    {
                        if (entry.value == null || entry.value.fields.Count == 0)
                        {
                            continue;
                        }

                        object targetValue = targetField.GetValue(component) ?? Activator.CreateInstance(targetField.FieldType);
                        ApplyModelInstanceData(targetValue, entry.value);
                        targetField.SetValue(component, targetValue);
                    }
                    else
                    {
                        object listValue = targetField.GetValue(component) ?? Activator.CreateInstance(targetField.FieldType);
                        MethodInfo clearMethod = targetField.FieldType.GetMethod("Clear");
                        MethodInfo addMethod = targetField.FieldType.GetMethod("Add");
                        Type itemType = targetField.FieldType.GetGenericArguments().FirstOrDefault();
                        if (clearMethod == null || addMethod == null || itemType == null)
                        {
                            continue;
                        }

                        clearMethod.Invoke(listValue, null);
                        foreach (CustomConfigData sourceConfig in entry.configs)
                        {
                            if (sourceConfig.value == null || sourceConfig.value.fields.Count == 0)
                            {
                                continue;
                            }

                            object itemValue = Activator.CreateInstance(itemType);
                            ApplyModelInstanceData(itemValue, sourceConfig.value);
                            addMethod.Invoke(listValue, new[] { itemValue });
                        }
                        targetField.SetValue(component, listValue);
                    }
                }
            }
        }

        private static void ApplyModelInstanceData(object target, CustomModelInstanceData source)
        {
            if (target == null || source == null)
            {
                return;
            }

            Type targetType = target.GetType();
            foreach (CustomFieldData sourceField in source.fields)
            {
                FieldInfo targetField = targetType.GetField(sourceField.fieldName, BindingFlags.Instance | BindingFlags.Public);
                if (targetField == null)
                {
                    continue;
                }

                if (sourceField.fieldType == FieldType.Model)
                {
                    if (sourceField.modelValue == null || sourceField.modelValue.fields.Count == 0)
                    {
                        continue;
                    }

                    object nestedValue = targetField.GetValue(target) ?? Activator.CreateInstance(targetField.FieldType);
                    ApplyModelInstanceData(nestedValue, sourceField.modelValue);
                    targetField.SetValue(target, nestedValue);
                }
                else
                {
                    object value = GetCustomFieldValue(sourceField);
                    if (value != null || !targetField.FieldType.IsValueType)
                    {
                        targetField.SetValue(target, value);
                    }
                }
            }
        }

        private void LoadComponentDataToEditingConfig(Component component)
        {
            if (component == null || editingConfig == null)
            {
                return;
            }

            Type componentType = component.GetType();
            foreach (CustomConfigData config in editingConfig.singleCustomConfigs)
            {
                foreach (CustomConfigEntryData entry in config.entries)
                {
                    FieldInfo sourceField = componentType.GetField(GetGeneratedEntryFieldName(entry), BindingFlags.Instance | BindingFlags.Public);
                    if (sourceField == null)
                    {
                        continue;
                    }

                    object sourceValue = sourceField.GetValue(component);
                    if (entry.entryKind == CustomConfigEntryKind.Model)
                    {
                        ReadModelInstanceData(entry.value, sourceValue);
                    }
                    else
                    {
                        LoadListEntryData(entry, sourceValue);
                    }
                }
            }
            ShowBottomSuccess($"已读取场景实例字段: {component.gameObject.name}");
        }

        private void LoadListEntryData(CustomConfigEntryData entry, object sourceValue)
        {
            if (sourceValue is not IEnumerable sourceList || sourceValue is string)
            {
                return;
            }

            entry.configs.Clear();
            int index = 0;
            foreach (object itemValue in sourceList)
            {
                var item = new CustomConfigData
                {
                    configName = $"Item {index + 1}",
                    modelTypeName = entry.modelTypeName,
                    value = CreateModelInstance(entry.modelTypeName)
                };
                ReadModelInstanceData(item.value, itemValue);
                entry.configs.Add(item);
                index++;
            }
        }

        private static void ReadModelInstanceData(CustomModelInstanceData target, object source)
        {
            if (target == null || source == null)
            {
                return;
            }

            Type sourceType = source.GetType();
            foreach (CustomFieldData targetField in target.fields)
            {
                FieldInfo sourceField = sourceType.GetField(targetField.fieldName, BindingFlags.Instance | BindingFlags.Public);
                if (sourceField == null)
                {
                    continue;
                }

                object value = sourceField.GetValue(source);
                if (targetField.fieldType == FieldType.Model)
                {
                    ReadModelInstanceData(targetField.modelValue, value);
                }
                else
                {
                    AssignFieldValue(targetField, value);
                }
            }
        }

        private static object GetCustomFieldValue(CustomFieldData field)
        {
            switch (field.fieldType)
            {
                case FieldType.String: return field.stringValue;
                case FieldType.Int: return field.intValue;
                case FieldType.Float: return field.floatValue;
                case FieldType.Bool: return field.boolValue;
                case FieldType.Vector3: return field.vector3Value;
                case FieldType.GameObject: return field.gameObjectValue;
                case FieldType.Material: return field.materialValue;
                case FieldType.Texture: return field.textureValue;
                default: return null;
            }
        }

        private CustomModelInstanceData CreateModelInstance(string modelTypeName)
        {
            return CreateModelInstance(modelTypeName, new HashSet<string>());
        }

        private CustomModelInstanceData CreateModelInstance(string modelTypeName, HashSet<string> visited)
        {
            if (currentConfig == null || string.IsNullOrEmpty(modelTypeName) || !visited.Add(modelTypeName))
            {
                return new CustomModelInstanceData();
            }

            CustomModelData model = currentConfig.customModels.FirstOrDefault(item => item.modelName == modelTypeName);
            var instance = new CustomModelInstanceData
            {
                fields = model == null ? new List<CustomFieldData>() : model.fields.Select(field => new CustomFieldData(field.fieldName, field.fieldType)
                {
                    sourceTypeName = field.sourceTypeName,
                    modelTypeName = field.modelTypeName,
                    modelValue = field.fieldType == FieldType.Model ? CreateModelInstance(field.modelTypeName, visited) : new CustomModelInstanceData()
                }).ToList()
            };
            visited.Remove(modelTypeName);
            return instance;
        }

        private ConfigToolData CreateEmptyEditingConfig(ConfigToolData source)
        {
            var result = CreateInstance<ConfigToolData>();
            result.customModels = CopyModelTemplates(source.customModels);
            result.singleCustomConfigs = source.singleCustomConfigs.Select(CopyConfigTemplate).ToList();
            return result;
        }

        private List<CustomModelData> CopyModelTemplates(IEnumerable<CustomModelData> source)
        {
            return source.Select(model => new CustomModelData
            {
                modelName = model.modelName,
                sourceTypeName = model.sourceTypeName,
                fields = model.fields.Select(CopyFieldTemplate).ToList()
            }).ToList();
        }

        private CustomConfigData CopyConfigTemplate(CustomConfigData source)
        {
            return new CustomConfigData
            {
                configName = source.configName,
                modelTypeName = source.modelTypeName,
                value = CreateModelInstance(source.modelTypeName),
                entries = source.entries.Select(CopyEntryTemplate).ToList()
            };
        }

        private CustomConfigEntryData CopyEntryTemplate(CustomConfigEntryData source)
        {
            return new CustomConfigEntryData
            {
                entryName = source.entryName,
                scriptParameterName = "",
                entryKind = source.entryKind,
                modelTypeName = source.modelTypeName,
                value = source.entryKind == CustomConfigEntryKind.Model ? CreateModelInstance(source.modelTypeName) : new CustomModelInstanceData(),
                configs = source.configs.Select(item => new CustomConfigData
                {
                    configName = item.configName,
                    modelTypeName = source.modelTypeName,
                    value = CreateModelInstance(source.modelTypeName)
                }).ToList()
            };
        }

        private CustomFieldData CopyFieldTemplate(CustomFieldData source)
        {
            return new CustomFieldData(source.fieldName, source.fieldType)
            {
                sourceTypeName = source.sourceTypeName,
                modelTypeName = source.modelTypeName,
                modelValue = source.fieldType == FieldType.Model ? CreateModelInstance(source.modelTypeName) : new CustomModelInstanceData()
            };
        }

        private Dictionary<string, string> GetModelTypeNames()
        {
            var result = new Dictionary<string, string>();
            foreach (CustomModelData model in currentConfig.customModels)
            {
                if (model == null || string.IsNullOrEmpty(model.modelName))
                {
                    continue;
                }

                result[model.modelName] = string.IsNullOrEmpty(model.sourceTypeName) ? GetSafeClassName(model.modelName) : model.sourceTypeName;
            }
            return result;
        }

        private static string GetSafeClassName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "GeneratedConfig";
            }

            var sb = new StringBuilder();
            bool nextUpper = true;
            foreach (char c in name)
            {
                if (c == '_' || c <= 127 && char.IsLetterOrDigit(c))
                {
                    sb.Append(nextUpper ? char.ToUpperInvariant(c) : c);
                    nextUpper = false;
                }
                else
                {
                    nextUpper = true;
                }
            }

            string result = sb.Length == 0 ? "GeneratedConfig" : sb.ToString();
            return char.IsDigit(result[0]) ? "_" + result : result;
        }

        private static string GetGeneratedEntryFieldName(CustomConfigEntryData entry)
        {
            if (!string.IsNullOrEmpty(entry.scriptParameterName))
            {
                return ToLowerCamelCase(entry.scriptParameterName, "configItem");
            }

            string suffix = entry.entryKind == CustomConfigEntryKind.ModelList ? "List" : "";
            return ToLowerCamelCase(entry.modelTypeName + suffix, "configItem");
        }

        private static string ToLowerCamelCase(string value, string fallback)
        {
            string safeName = GetSafeClassName(value);
            if (string.IsNullOrEmpty(safeName))
            {
                safeName = fallback;
            }

            string result = char.ToLowerInvariant(safeName[0]) + safeName.Substring(1);
            return char.IsDigit(result[0]) ? "_" + result : result;
        }

        private static string GetUniqueGeneratedName(string requestedName, HashSet<string> usedNames)
        {
            string baseName = string.IsNullOrEmpty(requestedName) ? "generatedItem" : requestedName;
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

        private static string EscapeStringLiteral(string value)
        {
            return (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static bool IsValidCSharpIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || char.IsDigit(value[0]))
            {
                return false;
            }

            return value.All(c => c == '_' || c <= 127 && char.IsLetterOrDigit(c));
        }

        private enum BatchMode
        {
            Prefix,
            Suffix,
            Children
        }

        private class BatchState
        {
        public BatchMode mode;
        public string prefix = "";
        public string suffix = "";
        public GameObject parent;
        public List<GameObject> objects = new List<GameObject>();
        public List<PropertyOption> propertyOptions = new List<PropertyOption>();
        public Dictionary<string, int> selectedPropertyByField = new Dictionary<string, int>();
        public Dictionary<string, List<PropertyOption>> optionCacheByField = new Dictionary<string, List<PropertyOption>>();
        }

        private class SingleObjectState
        {
        public GameObject target;
        public List<PropertyOption> propertyOptions = new List<PropertyOption>();
        public Dictionary<string, int> selectedPropertyByField = new Dictionary<string, int>();
        public Dictionary<string, List<PropertyOption>> optionCacheByField = new Dictionary<string, List<PropertyOption>>();
        }

        private class PropertyOption
        {
        public static readonly PropertyOption KeepUnchanged = new PropertyOption("__keep_unchanged__", "保持不变", typeof(void), _ => null);

        public string key;
        public string displayName;
        public Type valueType;
        public Func<GameObject, object> getter;

        public PropertyOption(string key, string displayName, Type valueType, Func<GameObject, object> getter)
            {
                this.key = key;
                this.displayName = displayName;
                this.valueType = valueType;
                this.getter = getter;
            }
        }
    }
}
