using UnityEngine;
using UnityEditor;

namespace ConfigTool.Editor
{
    [CustomEditor(typeof(ConfigToolData))]
    public class ConfigToolDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("配置内容", EditorStyles.boldLabel);
            SerializedProperty configsProp = serializedObject.FindProperty("singleCustomConfigs");
            if (configsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("当前没有通过配置编辑器顶部 + 添加的配置。", MessageType.Info);
            }
            else
            {
                DrawConfigList(configsProp);
            }

            serializedObject.ApplyModifiedProperties();

            if (GUILayout.Button("在配置编辑器中打开", GUILayout.Height(30)))
            {
                ConfigToolEditor.ShowWindow();
                Selection.activeObject = target;
            }
        }

        private void DrawConfigList(SerializedProperty configsProp)
        {
            for (int i = 0; i < configsProp.arraySize; i++)
            {
                SerializedProperty configProp = configsProp.GetArrayElementAtIndex(i);
                SerializedProperty configNameProp = configProp.FindPropertyRelative("configName");
                EditorGUILayout.BeginVertical("box");
                string configName = string.IsNullOrEmpty(configNameProp.stringValue) ? "未命名配置" : configNameProp.stringValue;
                Rect titleRect = GUILayoutUtility.GetRect(0f, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
                configProp.isExpanded = EditorGUI.Foldout(titleRect, configProp.isExpanded, GUIContent.none, true);
                GUIStyle centeredTitleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter
                };
                EditorGUI.LabelField(titleRect, configName, centeredTitleStyle);
                if (configProp.isExpanded)
                {
                    DrawConfigEntries(configProp.FindPropertyRelative("entries"));
                }
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawConfigEntries(SerializedProperty entriesProp)
        {
            if (entriesProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("当前配置没有 Model 或 Model 列表。", MessageType.Info);
                return;
            }

            EditorGUI.indentLevel++;
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                SerializedProperty entryProp = entriesProp.GetArrayElementAtIndex(i);
                SerializedProperty entryNameProp = entryProp.FindPropertyRelative("entryName");
                SerializedProperty entryKindProp = entryProp.FindPropertyRelative("entryKind");
                SerializedProperty modelTypeNameProp = entryProp.FindPropertyRelative("modelTypeName");
                string entryName = string.IsNullOrEmpty(entryNameProp.stringValue) ? modelTypeNameProp.stringValue : entryNameProp.stringValue;
                string kindName = entryKindProp.enumValueIndex == (int)CustomConfigEntryKind.ModelList ? "Model 列表" : "Model";

                EditorGUILayout.BeginVertical("box");
                entryProp.isExpanded = EditorGUILayout.Foldout(entryProp.isExpanded, $"{entryName} ({kindName})", true);
                if (entryProp.isExpanded)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(modelTypeNameProp, new GUIContent("Model"));
                    }

                    if (entryKindProp.enumValueIndex == (int)CustomConfigEntryKind.Model)
                    {
                        DrawModelStructure(entryProp.FindPropertyRelative("value"));
                    }
                    else
                    {
                        DrawModelList(entryProp.FindPropertyRelative("configs"));
                    }
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUI.indentLevel--;
        }

        private void DrawModelList(SerializedProperty configsProp)
        {
            EditorGUILayout.LabelField($"列表项: {configsProp.arraySize}");
            EditorGUI.indentLevel++;
            for (int i = 0; i < configsProp.arraySize; i++)
            {
                SerializedProperty configProp = configsProp.GetArrayElementAtIndex(i);
                SerializedProperty configNameProp = configProp.FindPropertyRelative("configName");
                string itemName = string.IsNullOrEmpty(configNameProp.stringValue) ? $"列表项 {i + 1}" : configNameProp.stringValue;

                EditorGUILayout.BeginVertical("box");
                configProp.isExpanded = EditorGUILayout.Foldout(configProp.isExpanded, itemName, true);
                if (configProp.isExpanded)
                {
                    DrawModelStructure(configProp.FindPropertyRelative("value"));
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUI.indentLevel--;
        }

        private void DrawModelStructure(SerializedProperty instanceProp)
        {
            SerializedProperty fieldsProp = instanceProp.FindPropertyRelative("fields");
            if (fieldsProp.arraySize == 0)
            {
                EditorGUILayout.HelpBox("当前 Model 没有字段。", MessageType.Info);
                return;
            }

            EditorGUI.indentLevel++;
            for (int i = 0; i < fieldsProp.arraySize; i++)
            {
                SerializedProperty fieldProp = fieldsProp.GetArrayElementAtIndex(i);
                SerializedProperty fieldNameProp = fieldProp.FindPropertyRelative("fieldName");
                SerializedProperty fieldTypeProp = fieldProp.FindPropertyRelative("fieldType");
                FieldType fieldType = (FieldType)fieldTypeProp.enumValueIndex;
                if (fieldType == FieldType.Model)
                {
                    string modelTypeName = fieldProp.FindPropertyRelative("modelTypeName").stringValue;
                    fieldProp.isExpanded = EditorGUILayout.Foldout(fieldProp.isExpanded, $"{fieldNameProp.stringValue}: {modelTypeName}", true);
                    if (fieldProp.isExpanded)
                    {
                        DrawModelStructure(fieldProp.FindPropertyRelative("modelValue"));
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(fieldNameProp.stringValue, fieldType.ToString());
                }
            }
            EditorGUI.indentLevel--;
        }
    }
}
