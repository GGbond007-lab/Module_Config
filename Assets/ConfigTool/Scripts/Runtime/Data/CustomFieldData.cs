using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConfigTool
{
    [Serializable]
    public class CustomFieldData
    {
        public string fieldName;
        public FieldType fieldType;
        public string stringValue;
        public int intValue;
        public float floatValue;
        public bool boolValue;
        public Vector3 vector3Value;
        public GameObject gameObjectValue;
        public Material materialValue;
        public Texture textureValue;
        public string modelTypeName;
        public CustomModelInstanceData modelValue;

        public CustomFieldData()
        {
            fieldName = "NewField";
            fieldType = FieldType.String;
            stringValue = "";
            intValue = 0;
            floatValue = 0f;
            boolValue = false;
            vector3Value = Vector3.zero;
            gameObjectValue = null;
            materialValue = null;
            textureValue = null;
            modelTypeName = "";
            modelValue = new CustomModelInstanceData();
        }

        public CustomFieldData(string name, FieldType type)
        {
            fieldName = name;
            fieldType = type;
            stringValue = "";
            intValue = 0;
            floatValue = 0f;
            boolValue = false;
            vector3Value = Vector3.zero;
            gameObjectValue = null;
            materialValue = null;
            textureValue = null;
            modelTypeName = "";
            modelValue = new CustomModelInstanceData();
        }
    }

    public enum FieldType
    {
        String,
        Int,
        Float,
        Bool,
        Vector3,
        GameObject,
        Material,
        Texture,
        Model
    }

    [Serializable]
    public class CustomConfigData
    {
        public string configName;
        public string modelTypeName;
        public CustomModelInstanceData value = new CustomModelInstanceData();
        public List<CustomConfigEntryData> entries = new List<CustomConfigEntryData>();

        public CustomConfigData()
        {
            configName = "";
            modelTypeName = "";
        }
    }

    [Serializable]
    public class CustomConfigEntryData
    {
        public string entryName;
        public string scriptParameterName;
        public CustomConfigEntryKind entryKind;
        public string modelTypeName;
        public CustomModelInstanceData value = new CustomModelInstanceData();
        public List<CustomConfigData> configs = new List<CustomConfigData>();
    }

    public enum CustomConfigEntryKind
    {
        Model,
        ModelList
    }

    [Serializable]
    public class CustomModelInstanceData
    {
        public List<CustomFieldData> fields = new List<CustomFieldData>();
    }

    [Serializable]
    public class CustomModelData
    {
        public string modelName;
        public string sourceTypeName;
        public List<CustomFieldData> fields = new List<CustomFieldData>();

        public CustomModelData()
        {
            modelName = "NewModel";
            sourceTypeName = "";
        }
    }
}
