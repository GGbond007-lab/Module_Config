using System;
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

        public CustomFieldData()
        {
            fieldName = "NewField";
            fieldType = FieldType.String;
            stringValue = "";
            intValue = 0;
            boolValue = false;
        }

        public CustomFieldData(string name, FieldType type)
        {
            fieldName = name;
            fieldType = type;
            stringValue = "";
            intValue = 0;
            boolValue = false;
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
                default:
                    return null;
            }
        }
    }

    public enum FieldType
    {
        String,
        Int,
        Bool
    }
}
