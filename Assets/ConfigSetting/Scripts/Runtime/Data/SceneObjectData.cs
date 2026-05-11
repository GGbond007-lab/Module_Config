using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConfigTool
{
    [Serializable]
    public class SceneObjectData
    {
        public string objectName;
        public string objectId;
        public GameObject referenceObject;
        public List<CustomFieldData> customFields = new List<CustomFieldData>();

        public SceneObjectData()
        {
            objectName = "NewSceneObject";
            objectId = "";
        }

        public SceneObjectData(GameObject obj)
        {
            referenceObject = obj;
            objectName = obj != null ? obj.name : "NullObject";
            objectId = "";
        }

        public SceneObjectData(GameObject obj, string id)
        {
            referenceObject = obj;
            objectName = obj != null ? obj.name : "NullObject";
            objectId = id;
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
    public class SceneObjectListData
    {
        public string listName;
        public string listDescription;
        public List<SceneObjectData> sceneObjects = new List<SceneObjectData>();

        public SceneObjectListData()
        {
            listName = "NewSceneObjectList";
            listDescription = "";
        }

        public SceneObjectListData(string name, string description = "")
        {
            listName = name;
            listDescription = description;
        }
    }
}