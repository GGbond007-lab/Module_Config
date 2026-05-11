using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConfigTool
{
    public static class SceneObjectHelper
    {
        public static List<GameObject> FindObjectsByNameFilter(string namePrefix = "", string nameSuffix = "", string parentName = "")
        {
            List<GameObject> results = new List<GameObject>();
            Scene currentScene = SceneManager.GetActiveScene();

            if (!currentScene.isLoaded)
            {
                Debug.LogWarning("No active scene is loaded.");
                return results;
            }

            GameObject[] rootObjects = currentScene.GetRootGameObjects();

            foreach (GameObject root in rootObjects)
            {
                if (!string.IsNullOrEmpty(parentName))
                {
                    if (root.name == parentName)
                    {
                        results.AddRange(FindChildObjectsRecursive(root.transform, namePrefix, nameSuffix));
                    }
                }
                else
                {
                    if (MatchesFilter(root.name, namePrefix, nameSuffix))
                    {
                        results.Add(root);
                    }
                    results.AddRange(FindChildObjectsRecursive(root.transform, namePrefix, nameSuffix));
                }
            }

            return results;
        }

        public static List<GameObject> FindChildObjectsRecursive(Transform parent, string namePrefix = "", string nameSuffix = "")
        {
            List<GameObject> results = new List<GameObject>();

            foreach (Transform child in parent)
            {
                if (MatchesFilter(child.name, namePrefix, nameSuffix))
                {
                    results.Add(child.gameObject);
                }
                results.AddRange(FindChildObjectsRecursive(child, namePrefix, nameSuffix));
            }

            return results;
        }

        public static bool MatchesFilter(string objectName, string namePrefix, string nameSuffix)
        {
            bool prefixMatch = string.IsNullOrEmpty(namePrefix) || objectName.StartsWith(namePrefix);
            bool suffixMatch = string.IsNullOrEmpty(nameSuffix) || objectName.EndsWith(nameSuffix);
            return prefixMatch && suffixMatch;
        }

        public static List<GameObject> FindObjectsWithComponent<T>(string namePrefix = "", string nameSuffix = "") where T : Component
        {
            List<GameObject> results = new List<GameObject>();
            T[] components = Object.FindObjectsByType<T>(FindObjectsSortMode.None);

            foreach (T component in components)
            {
                if (component != null && component.gameObject != null)
                {
                    if (MatchesFilter(component.gameObject.name, namePrefix, nameSuffix))
                    {
                        results.Add(component.gameObject);
                    }
                }
            }

            return results;
        }

        public static List<GameObject> FindObjectsInLayer(int layer, string namePrefix = "", string nameSuffix = "")
        {
            List<GameObject> results = new List<GameObject>();
            Scene currentScene = SceneManager.GetActiveScene();

            if (!currentScene.isLoaded)
            {
                return results;
            }

            GameObject[] rootObjects = currentScene.GetRootGameObjects();

            foreach (GameObject root in rootObjects)
            {
                if (root.layer == layer && MatchesFilter(root.name, namePrefix, nameSuffix))
                {
                    results.Add(root);
                }
                results.AddRange(FindChildObjectsInLayerRecursive(root.transform, layer, namePrefix, nameSuffix));
            }

            return results;
        }

        private static List<GameObject> FindChildObjectsInLayerRecursive(Transform parent, int layer, string namePrefix, string nameSuffix)
        {
            List<GameObject> results = new List<GameObject>();

            foreach (Transform child in parent)
            {
                if (child.gameObject.layer == layer && MatchesFilter(child.name, namePrefix, nameSuffix))
                {
                    results.Add(child.gameObject);
                }
                results.AddRange(FindChildObjectsInLayerRecursive(child, layer, namePrefix, nameSuffix));
            }

            return results;
        }

        public static List<GameObject> FindObjectsWithTag(string tag, string namePrefix = "", string nameSuffix = "")
        {
            List<GameObject> results = new List<GameObject>();
            GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(tag);

            foreach (GameObject obj in taggedObjects)
            {
                if (MatchesFilter(obj.name, namePrefix, nameSuffix))
                {
                    results.Add(obj);
                }
            }

            return results;
        }

        public static void CreateParentIfNotExists(string parentName)
        {
            GameObject parent = GameObject.Find(parentName);
            if (parent == null)
            {
                parent = new GameObject(parentName);
            }
        }

        public static GameObject GetOrCreateParent(string parentName)
        {
            GameObject parent = GameObject.Find(parentName);
            if (parent == null)
            {
                parent = new GameObject(parentName);
            }
            return parent;
        }

        public static Vector3 CalculateBoundingBoxCenter(GameObject[] objects)
        {
            if (objects == null || objects.Length == 0)
            {
                return Vector3.zero;
            }

            Vector3 min = Vector3.one * float.MaxValue;
            Vector3 max = Vector3.one * float.MinValue;

            foreach (GameObject obj in objects)
            {
                if (obj == null) continue;

                Renderer renderer = obj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    min = Vector3.Min(min, renderer.bounds.min);
                    max = Vector3.Max(max, renderer.bounds.max);
                }
                else
                {
                    min = Vector3.Min(min, obj.transform.position);
                    max = Vector3.Max(max, obj.transform.position);
                }
            }

            return (min + max) / 2f;
        }

        public static Vector3 CalculateCenterOfPositions(GameObject[] objects)
        {
            if (objects == null || objects.Length == 0)
            {
                return Vector3.zero;
            }

            Vector3 sum = Vector3.zero;
            int count = 0;

            foreach (GameObject obj in objects)
            {
                if (obj != null)
                {
                    sum += obj.transform.position;
                    count++;
                }
            }

            return count > 0 ? sum / count : Vector3.zero;
        }
    }
}
