using System.Collections.Generic;
using UnityEngine;

namespace ConfigTool
{
    public class StyleManager : BaseConfigToolManager
    {
        [Header("Style Settings")]
        [SerializeField] private bool applyOnStart = true;
        [SerializeField] private string defaultStyleName = "";

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            if (applyOnStart && !string.IsNullOrEmpty(defaultStyleName))
            {
                ApplyStyle(defaultStyleName);
            }
        }

        public void ApplyStyle(string styleName)
        {
            if (configData == null)
            {
                Debug.LogError($"[{gameObject.name}] StyleManager: No configuration assigned!");
                return;
            }

            StyleMaterialData materialData = configData.styleMaterials.Find(m => m.materialName == styleName);
            if (materialData == null)
            {
                Debug.LogWarning($"[{gameObject.name}] StyleManager: Style material '{styleName}' not found!");
                return;
            }

            Debug.Log($"[{gameObject.name}] StyleManager: Applied style '{styleName}'");
        }

        public StyleMaterialData GetStyleMaterial(string materialName)
        {
            if (configData == null) return null;
            return configData.styleMaterials.Find(m => m.materialName == materialName);
        }

        public List<string> GetAllStyleMaterialNames()
        {
            List<string> names = new List<string>();
            if (configData != null)
            {
                foreach (var material in configData.styleMaterials)
                {
                    if (material != null)
                    {
                        names.Add(material.materialName);
                    }
                }
            }
            return names;
        }

        public List<StyleMaterialData> GetAllStyleMaterials()
        {
            if (configData == null) return new List<StyleMaterialData>();
            return configData.styleMaterials;
        }
    }
}
