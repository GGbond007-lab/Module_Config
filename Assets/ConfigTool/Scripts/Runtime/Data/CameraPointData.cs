using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConfigTool
{
    [Serializable]
    public class CameraPointData
    {
        public string pointName;
        public Vector3 position;
        public Vector3 targetPosition;
        public List<CustomFieldData> customFields = new List<CustomFieldData>();

        public CameraPointData()
        {
            pointName = "NewCameraPoint";
            position = Vector3.zero;
            targetPosition = Vector3.forward;
        }

        public CameraPointData(string name, Vector3 pos)
        {
            pointName = name;
            position = pos;
            targetPosition = pos + Vector3.forward;
        }

        public CameraPointData(string name, Vector3 pos, Vector3 targetPos)
        {
            pointName = name;
            position = pos;
            targetPosition = targetPos;
        }

        public void AddCustomField(string fieldName, FieldType fieldType)
        {
            customFields.Add(new CustomFieldData(fieldName, fieldType));
        }

        public void RemoveCustomField(int index)
        {
            if (index >= 0 && index < customFields.Count)
            {
                customFields.RemoveAt(index);
            }
        }
    }

    [Serializable]
    public class CameraPointListData
    {
        public string listName;
        public string listDescription;
        public List<CameraPointData> cameraPoints = new List<CameraPointData>();

        public CameraPointListData()
        {
            listName = "NewCameraPointList";
            listDescription = "";
        }

        public CameraPointListData(string name, string description = "")
        {
            listName = name;
            listDescription = description;
        }
    }
}