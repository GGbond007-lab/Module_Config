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

        public void Capture(ConfigToolData config)
        {
            singleCameraPoints = CopyCameraPoints(config.singleCameraPoints);
            cameraPointLists = CopyCameraPointLists(config.cameraPointLists);
            singleSceneObjects = CopySceneObjects(config.singleSceneObjects);
            sceneObjectLists = CopySceneObjectLists(config.sceneObjectLists);
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
                    boolValue = field.boolValue
                };
                result.Add(copy);
            }
            return result;
        }
    }
}
