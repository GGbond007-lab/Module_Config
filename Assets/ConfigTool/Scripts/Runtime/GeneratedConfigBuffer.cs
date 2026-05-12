using System.Collections.Generic;
using UnityEngine;

namespace ConfigTool
{
    public class GeneratedConfigBuffer : MonoBehaviour
    {
        public List<CameraPointData> singleCameraPoints = new List<CameraPointData>();
        public List<CameraPointListData> cameraPointLists = new List<CameraPointListData>();
        public List<SceneObjectData> singleSceneObjects = new List<SceneObjectData>();
        public List<SceneObjectListData> sceneObjectLists = new List<SceneObjectListData>();
        public List<CustomModelData> customModels = new List<CustomModelData>();
        public List<CustomConfigData> singleCustomConfigs = new List<CustomConfigData>();
        public List<CustomConfigListData> customConfigLists = new List<CustomConfigListData>();

        public void Capture(ConfigToolData config)
        {
            singleCameraPoints = CopyCameraPoints(config.singleCameraPoints);
            cameraPointLists = CopyCameraPointLists(config.cameraPointLists);
            singleSceneObjects = CopySceneObjects(config.singleSceneObjects);
            sceneObjectLists = CopySceneObjectLists(config.sceneObjectLists);
            customModels = CopyCustomModels(config.customModels);
            singleCustomConfigs = CopyCustomConfigs(config.singleCustomConfigs);
            customConfigLists = CopyCustomConfigLists(config.customConfigLists);
        }

        private static List<CameraPointData> CopyCameraPoints(IEnumerable<CameraPointData> source)
        {
            var result = new List<CameraPointData>();
            foreach (CameraPointData point in source)
            {
                if (point == null)
                {
                    continue;
                }

                var copy = new CameraPointData(point.pointName, point.position, point.targetPosition)
                {
                    customFields = CopyCustomFields(point.customFields)
                };
                result.Add(copy);
            }
            return result;
        }

        private static List<CameraPointListData> CopyCameraPointLists(IEnumerable<CameraPointListData> source)
        {
            var result = new List<CameraPointListData>();
            foreach (CameraPointListData list in source)
            {
                if (list == null)
                {
                    continue;
                }

                var copy = new CameraPointListData(list.listName, list.listDescription)
                {
                    cameraPoints = CopyCameraPoints(list.cameraPoints)
                };
                result.Add(copy);
            }
            return result;
        }

        private static List<SceneObjectData> CopySceneObjects(IEnumerable<SceneObjectData> source)
        {
            var result = new List<SceneObjectData>();
            foreach (SceneObjectData sceneObject in source)
            {
                if (sceneObject == null)
                {
                    continue;
                }

                var copy = new SceneObjectData(sceneObject.referenceObject, sceneObject.objectId)
                {
                    objectName = sceneObject.objectName,
                    customFields = CopyCustomFields(sceneObject.customFields)
                };
                result.Add(copy);
            }
            return result;
        }

        private static List<SceneObjectListData> CopySceneObjectLists(IEnumerable<SceneObjectListData> source)
        {
            var result = new List<SceneObjectListData>();
            foreach (SceneObjectListData list in source)
            {
                if (list == null)
                {
                    continue;
                }

                var copy = new SceneObjectListData(list.listName, list.listDescription)
                {
                    sceneObjects = CopySceneObjects(list.sceneObjects)
                };
                result.Add(copy);
            }
            return result;
        }

        private static List<CustomModelData> CopyCustomModels(IEnumerable<CustomModelData> source)
        {
            var result = new List<CustomModelData>();
            foreach (CustomModelData model in source)
            {
                if (model == null)
                {
                    continue;
                }

                var copy = new CustomModelData
                {
                    modelName = model.modelName,
                    sourceTypeName = model.sourceTypeName,
                    fields = CopyCustomFields(model.fields)
                };
                result.Add(copy);
            }
            return result;
        }

        private static List<CustomConfigData> CopyCustomConfigs(IEnumerable<CustomConfigData> source)
        {
            var result = new List<CustomConfigData>();
            foreach (CustomConfigData config in source)
            {
                if (config == null)
                {
                    continue;
                }

                result.Add(new CustomConfigData
                {
                    configName = config.configName,
                    modelTypeName = config.modelTypeName,
                    value = CopyModelInstance(config.value)
                });
            }
            return result;
        }

        private static List<CustomConfigListData> CopyCustomConfigLists(IEnumerable<CustomConfigListData> source)
        {
            var result = new List<CustomConfigListData>();
            foreach (CustomConfigListData list in source)
            {
                if (list == null)
                {
                    continue;
                }

                result.Add(new CustomConfigListData
                {
                    listName = list.listName,
                    listDescription = list.listDescription,
                    modelTypeName = list.modelTypeName,
                    configs = CopyCustomConfigs(list.configs)
                });
            }
            return result;
        }

        private static CustomModelInstanceData CopyModelInstance(CustomModelInstanceData source)
        {
            return new CustomModelInstanceData
            {
                fields = source == null ? new List<CustomFieldData>() : CopyCustomFields(source.fields)
            };
        }

        private static List<CustomFieldData> CopyCustomFields(IEnumerable<CustomFieldData> source)
        {
            var result = new List<CustomFieldData>();
            foreach (CustomFieldData field in source)
            {
                if (field == null)
                {
                    continue;
                }

                var copy = new CustomFieldData(field.fieldName, field.fieldType)
                {
                    stringValue = field.stringValue,
                    intValue = field.intValue,
                    boolValue = field.boolValue,
                    vector3Value = field.vector3Value,
                    gameObjectValue = field.gameObjectValue,
                    modelTypeName = field.modelTypeName,
                    modelValue = CopyModelInstance(field.modelValue)
                };
                result.Add(copy);
            }
            return result;
        }
    }
}
