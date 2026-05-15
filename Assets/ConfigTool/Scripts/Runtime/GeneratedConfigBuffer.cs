using System.Collections.Generic;
using UnityEngine;

namespace ConfigTool
{
    public class GeneratedConfigBuffer : MonoBehaviour
    {
        public List<CustomModelData> customModels = new List<CustomModelData>();
        public List<CustomConfigData> singleCustomConfigs = new List<CustomConfigData>();

        public void Capture(ConfigToolData config)
        {
            customModels = CopyCustomModels(config.customModels);
            singleCustomConfigs = CopyCustomConfigs(config.singleCustomConfigs);
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
                    value = CopyModelInstance(config.value),
                    entries = CopyCustomConfigEntries(config.entries)
                });
            }
            return result;
        }

        private static List<CustomConfigEntryData> CopyCustomConfigEntries(IEnumerable<CustomConfigEntryData> source)
        {
            var result = new List<CustomConfigEntryData>();
            foreach (CustomConfigEntryData entry in source)
            {
                if (entry == null)
                {
                    continue;
                }

                result.Add(new CustomConfigEntryData
                {
                    entryName = entry.entryName,
                    scriptParameterName = entry.scriptParameterName,
                    entryKind = entry.entryKind,
                    modelTypeName = entry.modelTypeName,
                    value = CopyModelInstance(entry.value),
                    configs = CopyCustomConfigs(entry.configs)
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
                    floatValue = field.floatValue,
                    boolValue = field.boolValue,
                    vector3Value = field.vector3Value,
                    gameObjectValue = field.gameObjectValue,
                    materialValue = field.materialValue,
                    textureValue = field.textureValue,
                    modelTypeName = field.modelTypeName,
                    modelValue = CopyModelInstance(field.modelValue)
                };
                result.Add(copy);
            }
            return result;
        }
    }
}
