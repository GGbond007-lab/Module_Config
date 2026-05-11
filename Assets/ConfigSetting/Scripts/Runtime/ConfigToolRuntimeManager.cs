using System;
using System.Collections.Generic;
using UnityEngine;

namespace ConfigTool
{
    [Serializable]
    public class SerializedCustomField
    {
        public string fieldName;
        public FieldType fieldType;
        public string stringValue;
        public int intValue;
        public bool boolValue;

        public SerializedCustomField()
        {
        }

        public SerializedCustomField(string name, FieldType type, string strValue = "", int intValue = 0, bool boolValue = false)
        {
            fieldName = name;
            fieldType = type;
            stringValue = strValue;
            this.intValue = intValue;
            this.boolValue = boolValue;
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

        public void SetValue(object value)
        {
            if (fieldType == FieldType.String && value is string strValue)
            {
                stringValue = strValue;
            }
            else if (fieldType == FieldType.Int && value is int intVal)
            {
                intValue = intVal;
            }
            else if (fieldType == FieldType.Bool && value is bool boolVal)
            {
                boolValue = boolVal;
            }
        }
    }

    public abstract class BaseConfigToolManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] protected ConfigToolData configData;
        [SerializeField] protected bool autoInitialize = true;

        protected bool isInitialized = false;

        protected virtual void Awake()
        {
            if (autoInitialize)
            {
                Initialize();
            }
        }

        public virtual void Initialize()
        {
            if (configData == null)
            {
                Debug.LogError($"[{gameObject.name}] ConfigToolManager: Configuration data is not assigned!");
                return;
            }

            isInitialized = true;
            OnInitialized();
        }

        protected virtual void OnInitialized()
        {
        }

        public virtual void Refresh()
        {
            if (!isInitialized)
            {
                Initialize();
                return;
            }
            OnRefresh();
        }

        protected virtual void OnRefresh()
        {
        }

        public ConfigToolData GetConfigData()
        {
            return configData;
        }

        public void SetConfigData(ConfigToolData newConfig)
        {
            configData = newConfig;
            Refresh();
        }
    }

    [Serializable]
    public class RuntimeCameraPoint
    {
        public string pointName;
        public Vector3 position;
        public Vector3 targetPosition;
        public List<SerializedCustomField> customFields;

        public RuntimeCameraPoint()
        {
            customFields = new List<SerializedCustomField>();
        }

        public T GetCustomData<T>(string key)
        {
            var field = customFields?.Find(f => f.fieldName == key);
            if (field == null)
                return default(T);

            if (typeof(T) == typeof(string))
                return (T)(object)field.stringValue;
            if (typeof(T) == typeof(int))
                return (T)(object)field.intValue;
            if (typeof(T) == typeof(bool))
                return (T)(object)field.boolValue;

            return default(T);
        }

        public object GetCustomData(string key)
        {
            var field = customFields?.Find(f => f.fieldName == key);
            return field?.GetValue();
        }
    }

    [Serializable]
    public class RuntimeSceneObject
    {
        public string objectName;
        public string objectId;
        public GameObject referenceObject;
        public List<SerializedCustomField> customFields;

        public RuntimeSceneObject()
        {
            customFields = new List<SerializedCustomField>();
        }

        public T GetCustomData<T>(string key)
        {
            var field = customFields?.Find(f => f.fieldName == key);
            if (field == null)
                return default(T);

            if (typeof(T) == typeof(string))
                return (T)(object)field.stringValue;
            if (typeof(T) == typeof(int))
                return (T)(object)field.intValue;
            if (typeof(T) == typeof(bool))
                return (T)(object)field.boolValue;

            return default(T);
        }

        public object GetCustomData(string key)
        {
            var field = customFields?.Find(f => f.fieldName == key);
            return field?.GetValue();
        }
    }

    public class ConfigToolRuntimeManager : BaseConfigToolManager
    {
        [Header("Runtime Data - Camera Points")]
        [SerializeField] private List<RuntimeCameraPoint> runtimeCameraPoints = new List<RuntimeCameraPoint>();

        [Header("Runtime Data - Scene Objects")]
        [SerializeField] private List<RuntimeSceneObject> runtimeSceneObjects = new List<RuntimeSceneObject>();

        protected override void OnInitialized()
        {
            base.OnInitialized();
            LoadCameraPoints();
            LoadSceneObjects();
        }

        private void LoadCameraPoints()
        {
            runtimeCameraPoints.Clear();

            if (configData == null) return;

            foreach (var point in configData.singleCameraPoints)
            {
                var runtimePoint = new RuntimeCameraPoint
                {
                    pointName = point.pointName,
                    position = point.position,
                    targetPosition = point.targetPosition,
                    customFields = new List<SerializedCustomField>()
                };

                foreach (var field in point.customFields)
                {
                    runtimePoint.customFields.Add(new SerializedCustomField(
                        field.fieldName,
                        field.fieldType,
                        field.stringValue,
                        field.intValue,
                        field.boolValue
                    ));
                }

                runtimeCameraPoints.Add(runtimePoint);
            }
        }

        private void LoadSceneObjects()
        {
            runtimeSceneObjects.Clear();

            if (configData == null) return;

            foreach (var obj in configData.singleSceneObjects)
            {
                var runtimeObj = new RuntimeSceneObject
                {
                    objectName = obj.objectName,
                    objectId = obj.objectId,
                    referenceObject = obj.referenceObject,
                    customFields = new List<SerializedCustomField>()
                };

                foreach (var field in obj.customFields)
                {
                    runtimeObj.customFields.Add(new SerializedCustomField(
                        field.fieldName,
                        field.fieldType,
                        field.stringValue,
                        field.intValue,
                        field.boolValue
                    ));
                }

                runtimeSceneObjects.Add(runtimeObj);
            }
        }

        public RuntimeCameraPoint GetCameraPoint(string pointName)
        {
            return runtimeCameraPoints.Find(p => p.pointName == pointName);
        }

        public RuntimeCameraPoint GetCameraPointById(int id)
        {
            if (id >= 0 && id < runtimeCameraPoints.Count)
            {
                return runtimeCameraPoints[id];
            }
            return null;
        }

        public RuntimeSceneObject GetSceneObject(string objectId)
        {
            return runtimeSceneObjects.Find(o => o.objectId == objectId);
        }

        public RuntimeSceneObject GetSceneObjectByName(string objectName)
        {
            return runtimeSceneObjects.Find(o => o.objectName == objectName);
        }

        public List<RuntimeCameraPoint> GetAllCameraPoints()
        {
            return new List<RuntimeCameraPoint>(runtimeCameraPoints);
        }

        public List<RuntimeSceneObject> GetAllSceneObjects()
        {
            return new List<RuntimeSceneObject>(runtimeSceneObjects);
        }
    }
}
