using UnityEngine;
using UnityEditor;

namespace ConfigTool.Editor
{
    [CustomPropertyDrawer(typeof(CustomFieldData))]
    public class CustomFieldDataDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float originalWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 80;

            Rect fieldNameRect = new Rect(position.x, position.y, 120, position.height);
            Rect typeRect = new Rect(position.x + 125, position.y, 70, position.height);
            Rect valueRect = new Rect(position.x + 200, position.y, position.width - 220, position.height);

            var fieldNameProp = property.FindPropertyRelative("fieldName");
            var fieldTypeProp = property.FindPropertyRelative("fieldType");
            var stringValueProp = property.FindPropertyRelative("stringValue");
            var intValueProp = property.FindPropertyRelative("intValue");
            var boolValueProp = property.FindPropertyRelative("boolValue");

            EditorGUI.PropertyField(fieldNameRect, fieldNameProp, GUIContent.none);
            EditorGUI.PropertyField(typeRect, fieldTypeProp, GUIContent.none);

            FieldType fieldType = (FieldType)fieldTypeProp.enumValueIndex;

            EditorGUI.BeginChangeCheck();
            if (fieldType == FieldType.String)
            {
                EditorGUI.PropertyField(valueRect, stringValueProp, GUIContent.none);
            }
            else if (fieldType == FieldType.Int)
            {
                EditorGUI.PropertyField(valueRect, intValueProp, GUIContent.none);
            }
            else if (fieldType == FieldType.Bool)
            {
                EditorGUI.PropertyField(valueRect, boolValueProp, GUIContent.none);
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(property.serializedObject.targetObject);
            }

            EditorGUIUtility.labelWidth = originalWidth;
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(CameraPointData))]
    public class CameraPointDataDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, property.displayName);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float yOffset = EditorGUIUtility.singleLineHeight + 2;

                var pointNameProp = property.FindPropertyRelative("pointName");
                var positionProp = property.FindPropertyRelative("position");
                var targetPositionProp = property.FindPropertyRelative("targetPosition");
                var customFieldsProp = property.FindPropertyRelative("customFields");

                Rect nameRect = new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(nameRect, pointNameProp);
                yOffset += EditorGUIUtility.singleLineHeight + 2;

                Rect posRect = new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(posRect, positionProp);
                yOffset += EditorGUIUtility.singleLineHeight + 2;

                Rect targetPosRect = new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(targetPosRect, targetPositionProp);
                yOffset += EditorGUIUtility.singleLineHeight + 2;

                Rect fieldsRect = new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(fieldsRect, customFieldsProp);

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight * 4 + 20 + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("customFields"));
            }
            return EditorGUIUtility.singleLineHeight;
        }
    }

    [CustomPropertyDrawer(typeof(SceneObjectData))]
    public class SceneObjectDataDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, property.displayName);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float yOffset = EditorGUIUtility.singleLineHeight + 2;

                var objectNameProp = property.FindPropertyRelative("objectName");
                var objectIdProp = property.FindPropertyRelative("objectId");
                var referenceObjectProp = property.FindPropertyRelative("referenceObject");
                var customFieldsProp = property.FindPropertyRelative("customFields");

                Rect nameRect = new Rect(position.x, position.y + yOffset, position.width / 2 - 5, EditorGUIUtility.singleLineHeight);
                Rect idRect = new Rect(position.x + position.width / 2 + 5, position.y + yOffset, position.width / 2 - 5, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(nameRect, objectNameProp);
                EditorGUI.PropertyField(idRect, objectIdProp);
                yOffset += EditorGUIUtility.singleLineHeight + 2;

                Rect refRect = new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(refRect, referenceObjectProp);
                yOffset += EditorGUIUtility.singleLineHeight + 2;

                Rect fieldsRect = new Rect(position.x, position.y + yOffset, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(fieldsRect, customFieldsProp);

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight * 4 + 20 + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("customFields"));
            }
            return EditorGUIUtility.singleLineHeight;
        }
    }

    [CustomEditor(typeof(ConfigToolData))]
    public class ConfigToolDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (GUILayout.Button("在配置编辑器中打开", GUILayout.Height(30)))
            {
                ConfigToolEditor.ShowWindow();
                Selection.activeObject = target;
            }
        }
    }
}
