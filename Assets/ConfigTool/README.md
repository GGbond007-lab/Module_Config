# ConfigTool 配置编辑器

## 概述

ConfigTool 是一个用于 Unity 项目的通用配置编辑器模块，支持自定义 Model 定义、配置数据管理，并能从配置生成运行时脚本。

当前模块已统一使用 `ConfigTool` 命名：

- 运行时命名空间：`ConfigTool`
- 编辑器命名空间：`ConfigTool.Editor`
- 运行时程序集：`ConfigTool.Runtime`
- 编辑器程序集：`ConfigTool.Editor`

## 功能

### 核心数据结构

**ConfigToolData**：配置数据容器（ScriptableObject），包含：
- `customModels`：自定义 Model 定义列表
- `singleCustomConfigs`：单个配置数据列表

**CustomModelData**：Model 定义，包含：
- `modelName`：模型名称
- `fields`：字段列表（CustomFieldData）

**CustomFieldData**：字段数据，支持多种类型：
- 基础类型：String、Int、Float、Bool、Vector3
- 引用类型：GameObject、Material、Texture
- 嵌套类型：Model（支持嵌套引用）

### Model 管理器

编辑器支持：
- 新增、删除自定义 Model 定义
- 从可序列化类型自动导入 Model
- 支持嵌套 Model（最大 5 层）
- 防止循环引用检测

### 配置编辑器

- 创建多个独立配置（通过顶部 + 按钮）
- 每个配置可包含多个配置项（单项或列表）
- 支持从场景物体批量赋值
- 支持按前缀/后缀/父节点批量收集物体

### 代码生成

- 预览生成代码
- 生成配置脚本并添加到场景
- 支持读取/写入场景实例数据

## 文件结构

```text
Assets/ConfigTool/
├── README.md
└── Scripts/
    ├── Editor/
    │   ├── ConfigTool.Editor.asmdef
    │   ├── ConfigToolEditor.cs
    │   ├── ConfigScriptEditor.cs
    │   └── CustomPropertyDrawers.cs
    └── Runtime/
        ├── ConfigTool.Runtime.asmdef
        ├── GeneratedConfigBuffer.cs
        └── Data/
            ├── ConfigToolData.cs
            └── CustomFieldData.cs
```

## 文件说明

### Editor 目录

| 文件 | 作用 |
| --- | --- |
| `ConfigToolEditor.cs` | 主配置编辑器窗口，管理 Model 定义和配置结构 |
| `ConfigScriptEditor.cs` | 配置脚本编辑器，编辑配置内容并生成脚本 |
| `CustomPropertyDrawers.cs` | Inspector 自定义绘制器，显示 ConfigToolData 结构 |
| `ConfigTool.Editor.asmdef` | 编辑器程序集定义 |

### Runtime 目录

| 文件 | 作用 |
| --- | --- |
| `ConfigToolData.cs` | 配置数据容器（ScriptableObject），存储 Model 和配置 |
| `CustomFieldData.cs` | 字段数据类，包含所有支持的字段类型和枚举定义 |
| `GeneratedConfigBuffer.cs` | 生成配置缓冲区，临时保存配置快照用于脚本生成 |
| `ConfigTool.Runtime.asmdef` | 运行时程序集定义 |

## 快速开始

### 创建配置文件

方式一：Unity 菜单栏选择 `ConfigSetting/创建新配置`

方式二：在 Project 窗口右键选择 `Create/ConfigSetting/Configuration Data`

### 打开配置编辑器

Unity 菜单栏选择：
```text
ConfigSetting/配置编辑器
```

也可以选中一个 `ConfigToolData` 资源，在 Inspector 中点击打开按钮。

### 定义 Model

1. 打开配置编辑器
2. 在 Model 管理器页签中查看现有 Model
3. 从文件夹导入可序列化类型，或手动创建

### 创建配置

1. 在配置编辑器顶部点击 `+` 添加新配置
2. 为配置命名
3. 添加配置项（单项或列表）
4. 选择对应的 Model 类型

### 编辑配置内容

1. 打开 `ConfigSetting/配置脚本编辑器`
2. 选择配置结构（ConfigToolData）
3. 在配置内容编辑区域设置字段值
4. 支持从场景物体赋值或批量赋值

### 生成脚本

1. 在配置脚本编辑器中输入脚本名
2. 点击「检测脚本名」验证
3. 点击「刷新预览」查看生成代码
4. 点击「生成脚本并添加到场景」完成生成

## 菜单

| 菜单路径 | 功能 |
| --- | --- |
| `ConfigSetting/配置编辑器` | 打开主配置编辑器窗口，管理 Model 和配置结构 |
| `ConfigSetting/配置脚本编辑器` | 打开脚本编辑器，编辑内容并生成脚本 |
| `ConfigSetting/创建新配置` | 创建新的 `ConfigToolData` 配置文件 |

## 程序集

### ConfigTool.Runtime

运行时程序集，包含：
- `ConfigToolData` - 配置数据容器
- `CustomFieldData` - 字段数据及相关枚举
- `CustomModelData` - Model 定义
- `CustomConfigData` - 配置数据
- `CustomConfigEntryData` - 配置项数据
- `CustomModelInstanceData` - Model 实例数据
- `GeneratedConfigBuffer` - 生成配置缓冲区

### ConfigTool.Editor

编辑器程序集，仅在 Unity Editor 中使用：
- `ConfigToolEditor` - 配置编辑器窗口
- `ConfigScriptEditor` - 配置脚本编辑器窗口
- `ConfigToolDataEditor` - Inspector 自定义绘制器

该程序集引用 `ConfigTool.Runtime`。

## 支持的字段类型

| 类型 | 说明 |
| --- | --- |
| String | 字符串 |
| Int | 整数 |
| Float | 浮点数 |
| Bool | 布尔值 |
| Vector3 | 三维向量 |
| GameObject | 游戏对象引用 |
| Material | 材质引用 |
| Texture | 纹理引用 |
| Model | 嵌套 Model 引用 |

## 注意事项

1. 修改配置后请保存资源，避免数据只停留在编辑器内存中
2. 生成脚本后 Unity 需要完成一次脚本导入和编译，组件才会被添加到场景对象上
3. 如果 Console 中存在编译错误，Unity 无法解析新生成的组件类型，生成流程会提示手动添加脚本
4. Model 嵌套最大支持 5 层深度
5. 编辑器会检测并阻止循环引用的 Model 引用

## 技术要求

- Unity 6000.3 或更高版本
- .NET Standard 2.1

## 扩展建议

- 增加更多字段类型支持（如 Color、Quaternion、Sprite 等）
- 添加配置导出/导入功能（如 JSON、CSV）
- 为 Model 增加注释字段
- 添加版本控制支持，记录配置变更历史

## 许可

该模块可自由用于个人和商业项目。
