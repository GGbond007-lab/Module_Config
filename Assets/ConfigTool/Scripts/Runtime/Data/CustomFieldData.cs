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
        public bool boolValue;
        public Vector3 vector3Value;
        public GameObject gameObjectValue;
        public string modelTypeName;
        public CustomModelInstanceData modelValue;

        public CustomFieldData()
        {
            fieldName = "NewField";
            fieldType = FieldType.String;
            stringValue = "";
            intValue = 0;
            boolValue = false;
            vector3Value = Vector3.zero;
            gameObjectValue = null;
            modelTypeName = "";
            modelValue = new CustomModelInstanceData();
        }

        public CustomFieldData(string name, FieldType type)
        {
            fieldName = name;
            fieldType = type;
            stringValue = "";
            intValue = 0;
            boolValue = false;
            vector3Value = Vector3.zero;
            gameObjectValue = null;
            modelTypeName = "";
            modelValue = new CustomModelInstanceData();
        }

        public object GetValue()
        {
            switch (fieldType)
            {
                case FieldType.String:
                    return stringValue;
                case FieldType.Int:
                    return intValue;
                case FieldType.Bool:
                    return boolValue;
                case FieldType.Vector3:
                    return vector3Value;
                case FieldType.GameObject:
                    return gameObjectValue;
                case FieldType.Model:
                    return modelValue;
                default:
                    return null;
            }
        }
    }

    public enum FieldType
    {
        String,
        Int,
        Bool,
        Vector3,
        GameObject,
        Model
    }

    [Serializable]
    public class CustomConfigData
    {
        public string configName;
        public string modelTypeName;
        public CustomModelInstanceData value = new CustomModelInstanceData();

        public CustomConfigData()
        {
            configName = "NewConfig";
            modelTypeName = "";
        }
    }

    [Serializable]
    public class CustomConfigListData
    {
        public string listName;
        public string listDescription;
        public string modelTypeName;
        public List<CustomConfigData> configs = new List<CustomConfigData>();

        public CustomConfigListData()
        {
            listName = "NewConfigList";
            listDescription = "";
            modelTypeName = "";
        }
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
