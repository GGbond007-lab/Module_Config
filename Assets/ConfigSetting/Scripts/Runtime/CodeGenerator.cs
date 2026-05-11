using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ConfigTool
{
    public class CodeGenerator
    {
        public static string GenerateRuntimeManager(ConfigToolData config)
        {
            if (config == null)
            {
                Debug.LogError("Cannot generate code: Configuration is null");
                return null;
            }

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("namespace ConfigTool.Generated");
            sb.AppendLine("{");

            string className = GetSafeClassName(config.projectName);
            sb.AppendLine($"    [Serializable]");
            sb.AppendLine($"    public class {className}Config : ScriptableObject");
            sb.AppendLine("    {");

            if (config.singleCameraPoints.Count > 0)
            {
                sb.AppendLine("        [Serializable]");
                sb.AppendLine("        public class CameraPointData");
                sb.AppendLine("        {");
                sb.AppendLine("            public string pointName = \"\";");
                sb.AppendLine("            public Vector3 position = Vector3.zero;");
                sb.AppendLine("            public Quaternion rotation = Quaternion.identity;");
                GenerateCustomFields(config.singleCameraPoints[0].customFields, sb, 4);
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            foreach (var list in config.cameraPointLists)
            {
                string listClassName = GetSafeClassName(list.listName);
                sb.AppendLine("        [Serializable]");
                sb.AppendLine($"        public class {listClassName}CameraPointData");
                sb.AppendLine("        {");
                sb.AppendLine("            public string pointName = \"\";");
                sb.AppendLine("            public Vector3 position = Vector3.zero;");
                sb.AppendLine("            public Quaternion rotation = Quaternion.identity;");
                if (list.cameraPoints.Count > 0)
                {
                    GenerateCustomFields(list.cameraPoints[0].customFields, sb, 4);
                }
                else
                {
                    GenerateCustomFields(new List<CustomFieldData>(), sb, 4);
                }
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            if (config.singleSceneObjects.Count > 0)
            {
                sb.AppendLine("        [Serializable]");
                sb.AppendLine("        public class SceneObjectData");
                sb.AppendLine("        {");
                sb.AppendLine("            public string objectName = \"\";");
                sb.AppendLine("            public string objectId = \"\";");
                sb.AppendLine("            public GameObject referenceObject;");
                GenerateCustomFields(config.singleSceneObjects[0].customFields, sb, 4);
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            foreach (var list in config.sceneObjectLists)
            {
                string listClassName = GetSafeClassName(list.listName);
                sb.AppendLine("        [Serializable]");
                sb.AppendLine($"        public class {listClassName}ObjectData");
                sb.AppendLine("        {");
                sb.AppendLine("            public string objectName = \"\";");
                sb.AppendLine("            public string objectId = \"\";");
                sb.AppendLine("            public GameObject referenceObject;");
                if (list.sceneObjects.Count > 0)
                {
                    GenerateCustomFields(list.sceneObjects[0].customFields, sb, 4);
                }
                else
                {
                    GenerateCustomFields(new List<CustomFieldData>(), sb, 4);
                }
                sb.AppendLine("        }");
                sb.AppendLine();
            }

            sb.AppendLine("        public List<CameraPointData> cameraPoints = new List<CameraPointData>();");
            foreach (var list in config.cameraPointLists)
            {
                string listClassName = GetSafeClassName(list.listName);
                string fieldName = GetSafeFieldName(list.listName);
                sb.AppendLine($"        public List<{listClassName}CameraPointData> {fieldName} = new List<{listClassName}CameraPointData>();");
            }
            sb.AppendLine("        public List<SceneObjectData> sceneObjects = new List<SceneObjectData>();");
            foreach (var list in config.sceneObjectLists)
            {
                string listClassName = GetSafeClassName(list.listName);
                string fieldName = GetSafeFieldName(list.listName);
                sb.AppendLine($"        public List<{listClassName}ObjectData> {fieldName} = new List<{listClassName}ObjectData>();");
            }

            sb.AppendLine("    }");
            sb.AppendLine();

            sb.AppendLine($"    public class {className}RuntimeManager : MonoBehaviour");
            sb.AppendLine("    {");
            sb.AppendLine($"        [SerializeField] private {className}Config config;");
            sb.AppendLine();
            sb.AppendLine("        public List<CameraPointData> CameraPoints { get; private set; } = new List<CameraPointData>();");
            foreach (var list in config.cameraPointLists)
            {
                string listClassName = GetSafeClassName(list.listName);
                string fieldName = GetSafeFieldName(list.listName);
                sb.AppendLine($"        public List<{listClassName}CameraPointData> {listClassName}CameraPoints {{ get; private set; }} = new List<{listClassName}CameraPointData>();");
            }
            sb.AppendLine("        public List<SceneObjectData> SceneObjects { get; private set; } = new List<SceneObjectData>();");
            foreach (var list in config.sceneObjectLists)
            {
                string listClassName = GetSafeClassName(list.listName);
                string fieldName = GetSafeFieldName(list.listName);
                sb.AppendLine($"        public List<{listClassName}ObjectData> {listClassName}Objects {{ get; private set; }} = new List<{listClassName}ObjectData>();");
            }
            sb.AppendLine();

            sb.AppendLine("        private void Awake()");
            sb.AppendLine("        {");
            sb.AppendLine("            Initialize();");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public void Initialize()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (config == null)");
            sb.AppendLine("            {");
            sb.AppendLine("                Debug.LogError($\"[{gameObject.name}] {className}RuntimeManager: Config is not assigned!\");");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            LoadConfig();");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        private void LoadConfig()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (config == null) return;");
            sb.AppendLine();
            sb.AppendLine("            CameraPoints.Clear();");
            sb.AppendLine("            CameraPoints.AddRange(config.cameraPoints);");
            foreach (var list in config.cameraPointLists)
            {
                string listClassName = GetSafeClassName(list.listName);
                string fieldName = GetSafeFieldName(list.listName);
                sb.AppendLine($"            {listClassName}CameraPoints.Clear();");
                sb.AppendLine($"            {listClassName}CameraPoints.AddRange(config.{fieldName});");
            }
            sb.AppendLine("            SceneObjects.Clear();");
            sb.AppendLine("            SceneObjects.AddRange(config.sceneObjects);");
            foreach (var list in config.sceneObjectLists)
            {
                string listClassName = GetSafeClassName(list.listName);
                string fieldName = GetSafeFieldName(list.listName);
                sb.AppendLine($"            {listClassName}Objects.Clear();");
                sb.AppendLine($"            {listClassName}Objects.AddRange(config.{fieldName});");
            }
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public CameraPointData GetCameraPoint(string pointName)");
            sb.AppendLine("        {");
            sb.AppendLine("            return CameraPoints.Find(p => p.pointName == pointName);");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public SceneObjectData GetSceneObject(string objectId)");
            sb.AppendLine("        {");
            sb.AppendLine("            return SceneObjects.Find(o => o.objectId == objectId);");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public SceneObjectData GetSceneObjectByName(string objectName)");
            sb.AppendLine("        {");
            sb.AppendLine("            return SceneObjects.Find(o => o.objectName == objectName);");
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine("        public void Refresh()");
            sb.AppendLine("        {");
            sb.AppendLine("            LoadConfig();");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static void GenerateCustomFields(List<CustomFieldData> fields, StringBuilder sb, int indentSpaces)
        {
            string indent = new string(' ', indentSpaces);
            foreach (var field in fields)
            {
                string typeName = field.fieldType == FieldType.String ? "string" : "int";
                string defaultValue = field.fieldType == FieldType.String ? "\"\"" : "0";
                sb.AppendLine($"{indent}public {typeName} {GetSafeFieldName(field.fieldName)} = {defaultValue};");
            }
        }

        public static void SaveGeneratedCode(string code, string filePath)
        {
            if (string.IsNullOrEmpty(code))
            {
                Debug.LogError("Cannot save code: Code is empty");
                return;
            }

            try
            {
                string directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(filePath, code);
                Debug.Log($"Generated code saved to: {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save generated code: {e.Message}");
            }
        }

        public static string GetSafeClassName(string name)
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
            if (result.Length > 0 && char.IsDigit(result[0]))
            {
                result = "_" + result;
            }

            return result;
        }

        public static string GetSafeFieldName(string name)
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
            if (result.Length > 0 && char.IsDigit(result[0]))
            {
                result = "_" + result;
            }

            return result;
        }
    }
}
