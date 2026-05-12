# ConfigTool 配置编辑器

## 概述

ConfigTool 是一个用于 Unity 项目的配置编辑器模块，用来管理场景相机点位、场景物体、材质风格配置和自定义字段，并支持从配置生成运行时管理器脚本。

当前模块已统一使用 `ConfigTool` 命名：

- 运行时命名空间：`ConfigTool`
- 编辑器命名空间：`ConfigTool.Editor`
- 生成代码命名空间：`ConfigTool.Generated`
- 运行时程序集：`ConfigTool.Runtime`
- 编辑器程序集：`ConfigTool.Editor`

## 功能

### 配置数据

配置文件类型为 `ConfigToolData`，是一个 `ScriptableObject`，可保存以下数据：

- 基本信息：项目名、版本、描述
- 单个相机点位
- 相机点位列表
- 单个场景物体
- 场景物体列表
- 风格材质配置

### 相机点位

相机点位由 `CameraPointData` 表示，包含：

- 点位名称
- 观察位置 `Vector3`
- 目标位置 `Vector3`
- 自定义字段列表

自定义字段支持 `String`、`Int`、`Bool` 三种类型。

### 场景物体

场景物体由 `SceneObjectData` 表示，包含：

- 物体名称
- 物体 ID
- `GameObject` 引用
- 自定义字段列表

编辑器支持将场景中的物体拖拽到配置中，也支持按名称前缀、后缀和父级层级批量导入。

### 风格材质

风格材质由 `StyleMaterialData` 表示，包含：

- 材质名称
- `Material` 引用
- 自定义字段列表

`StyleManager` 提供运行时查询接口，例如按名称获取材质配置、获取所有材质配置名称等。当前 `ApplyStyle` 主要负责查找配置并输出应用日志，实际材质替换逻辑可按项目需要继续扩展。

### 运行时管理器

`ConfigToolRuntimeManager` 会在运行时把 `ConfigToolData` 中的配置加载为运行时数据：

- `RuntimeCameraPoint`
- `RuntimeSceneObject`
- `SerializedCustomField`

运行时自定义字段支持 `string`、`int`、`bool` 读取。

### 代码生成

编辑器窗口底部支持：

- 生成代码预览
- 生成运行时脚本并添加到场景对象

生成代码会放在 `ConfigTool.Generated` 命名空间下。字段名和类名会经过安全化处理，避免空格、符号或数字开头导致生成的 C# 无法编译。

## 文件结构

```text
Assets/ConfigSetting/
├── README.md
└── Scripts/
    ├── Editor/
    │   ├── BatchImportWindow.cs
    │   ├── ConfigTool.Editor.asmdef
    │   ├── ConfigToolEditor.cs
    │   └── CustomPropertyDrawers.cs
    └── Runtime/
        ├── CodeGenerator.cs
        ├── ConfigTool.Runtime.asmdef
        ├── ConfigToolRuntimeManager.cs
        ├── SceneObjectHelper.cs
        ├── StyleManager.cs
        └── Data/
            ├── CameraPointData.cs
            ├── ConfigToolData.cs
            ├── CustomFieldData.cs
            └── SceneObjectData.cs
```

## 快速开始

### 创建配置文件

方式一：Unity 菜单栏选择 `ConfigSetting/创建新配置`。

方式二：在 Project 窗口右键选择 `Create/ConfigSetting/Configuration Data`。

默认创建的配置资源类型为 `ConfigToolData`。

### 打开配置编辑器

Unity 菜单栏选择：

```text
ConfigSetting/配置编辑器
```

也可以选中一个 `ConfigToolData` 资源，在 Inspector 中点击打开按钮。

### 配置相机点位

1. 打开 `ConfigSetting/配置编辑器`。
2. 选择或创建一个 `ConfigToolData`。
3. 在相机点位页签中添加单个点位或点位列表。
4. 设置点位名称、观察位置、目标位置。
5. 根据需要添加 `String`、`Int`、`Bool` 自定义字段。

### 配置场景物体

单个添加：

1. 在场景物体页签中点击添加物体。
2. 设置物体名称、ID 和 `GameObject` 引用。
3. 根据需要添加自定义字段。

拖拽添加：

1. 在场景中或 Hierarchy 中选中物体。
2. 将物体拖拽到编辑器的拖拽区域。

批量导入：

1. 切换到批量导入页签，或打开 `ConfigSetting/批量导入工具`。
2. 设置名称前缀、后缀或父级名称。
3. 预览将导入的物体。
4. 执行导入。

### 生成运行时脚本

1. 选中或加载一个 `ConfigToolData`。
2. 点击编辑器底部的生成代码预览。
3. 确认生成内容后，点击生成并添加到场景。
4. 选择脚本保存位置。

生成后，Unity 会导入脚本并尝试把生成的组件添加到场景对象上。生成流程会先在临时场景对象上保存一份配置快照，脚本编译完成后再把相机点位、场景物体、场景对象引用和自定义字段写入生成组件，避免 Unity 编译和域重载期间丢失配置内容。

## 运行时使用示例

### 读取配置数据

```csharp
using UnityEngine;
using ConfigTool;

public class ConfigToolExample : MonoBehaviour
{
    [SerializeField] private ConfigToolRuntimeManager manager;

    private void Start()
    {
        if (manager == null)
        {
            manager = FindFirstObjectByType<ConfigToolRuntimeManager>();
        }

        if (manager == null)
        {
            return;
        }

        foreach (RuntimeCameraPoint point in manager.GetAllCameraPoints())
        {
            Debug.Log($"Camera point: {point.pointName}, position: {point.position}");
        }

        foreach (RuntimeSceneObject sceneObject in manager.GetAllSceneObjects())
        {
            Debug.Log($"Scene object: {sceneObject.objectName}, id: {sceneObject.objectId}");
        }
    }
}
```

### 读取自定义字段

```csharp
using UnityEngine;
using ConfigTool;

public class CustomFieldExample : MonoBehaviour
{
    [SerializeField] private ConfigToolRuntimeManager manager;

    private void Start()
    {
        RuntimeCameraPoint point = manager.GetCameraPoint("Entrance");
        if (point != null)
        {
            string pointId = point.GetCustomData<string>("pointId");
            int floor = point.GetCustomData<int>("floor");
            bool isDefault = point.GetCustomData<bool>("isDefault");

            Debug.Log($"{pointId}, {floor}, {isDefault}");
        }
    }
}
```

### 查询风格材质

```csharp
using UnityEngine;
using ConfigTool;

public class StyleMaterialExample : MonoBehaviour
{
    [SerializeField] private StyleManager styleManager;

    private void Start()
    {
        if (styleManager == null)
        {
            styleManager = FindFirstObjectByType<StyleManager>();
        }

        if (styleManager == null)
        {
            return;
        }

        foreach (string materialName in styleManager.GetAllStyleMaterialNames())
        {
            Debug.Log($"Style material: {materialName}");
        }

        StyleMaterialData materialData = styleManager.GetStyleMaterial("Default");
        if (materialData != null && materialData.material != null)
        {
            Debug.Log($"Found material: {materialData.material.name}");
        }
    }
}
```

## 菜单

| 菜单路径 | 功能 |
| --- | --- |
| `ConfigSetting/配置编辑器` | 打开主配置编辑器窗口 |
| `ConfigSetting/创建新配置` | 创建新的 `ConfigToolData` 配置文件 |
| `ConfigSetting/生成运行时管理器` | 为当前选中的配置生成运行时管理器脚本 |
| `ConfigSetting/批量导入工具` | 打开批量导入工具窗口 |

## 程序集

### ConfigTool.Runtime

运行时程序集，包含：

- `ConfigToolData`
- `CameraPointData`
- `SceneObjectData`
- `CustomFieldData`
- `ConfigToolRuntimeManager`
- `StyleManager`
- `SceneObjectHelper`
- `CodeGenerator`

### ConfigTool.Editor

编辑器程序集，仅在 Unity Editor 中使用，包含：

- `ConfigToolEditor`
- `BatchImportWindow`
- `CustomPropertyDrawers`

该程序集引用 `ConfigTool.Runtime`。

## 注意事项

1. 修改配置后请保存资源，避免数据只停留在编辑器内存中。
2. 生成脚本后 Unity 需要完成一次脚本导入和编译，组件才会被添加到场景对象上。
3. 如果 Console 中存在编译错误，Unity 无法解析新生成的组件类型，生成流程会提示手动添加脚本。
4. `ConfigToolRuntimeManager` 默认在 `Awake` 中初始化，也可以手动调用 `Initialize()` 或 `Refresh()`。
5. 自定义字段名用于生成代码时会被转换成合法 C# 字段名；运行时通过原始字段名查询自定义字段。
6. `SceneObjectHelper.FindObjectsWithComponent<T>()` 使用 `FindObjectsByType<T>(FindObjectsSortMode.None)`，适合 Unity 6000 及较新版本。
7. `StyleManager.ApplyStyle` 当前是可扩展入口，不会自动替换场景中所有材质。

## 技术要求

- Unity 6000.3 或更高版本
- .NET Standard 2.1

项目当前已通过：

```text
dotnet build Module_Config.slnx --no-restore
```

验证结果为 `0` 个警告、`0` 个错误。

## 扩展建议

可以从以下方向扩展：

- 在 `StyleManager.ApplyStyle` 中实现实际材质替换逻辑。
- 在 `CodeGenerator.cs` 中补充更多生成模板。
- 为批量导入增加去重策略，避免重复导入同一个场景物体。
- 为 `ConfigToolRuntimeManager` 增加按 ID、标签或自定义字段索引的快速查询缓存。
- 为编辑器窗口补充更完整的数据校验，例如重复 ID、空名称、无效引用提示。

## 许可

该模块可自由用于个人和商业项目。
