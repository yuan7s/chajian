# Form4 编码整理 - 设计文档

## 概述

新建 Form4 浮动配置窗口，对装配体执行批量"编码整理"操作：遍历组件 → 筛选过滤 → 将文档标题同步到配置自定义属性。

参照宏：`D:\solidworks\宏2\图号编码整理.swp`，将其硬编码的筛选条件提取为 UI 控件。

## UI 布局

```
┌─────────────────────────────────┐
│ 编码整理                         │
├─────────────────────────────────┤
│ 名称筛选（可选）                  │
│ [ TextBox: 包含文本             ]│
├─────────────────────────────────┤
│ 处理范围                         │
│ [√] 处理装配体   [√] 处理零件    │
├─────────────────────────────────┤
│ 排除条件                         │
│ [ ] 排除虚拟装配体               │
│ [ ] 排除标准件（零件类型=标准件） │
│ [ ] 排除外购件（零件类型=外购件） │
├─────────────────────────────────┤
│         [ 执行编码整理 ]          │
└─────────────────────────────────┘
```

### 控件清单

| 控件 | 类型 | 默认值 | 持久化 key |
|------|------|--------|------------|
| 名称筛选 | TextBox | 空 | `CodingCleanup_NameFilter` |
| 处理装配体 | CheckBox | True | `CodingCleanup_ProcessAsm` |
| 处理零件 | CheckBox | True | `CodingCleanup_ProcessPart` |
| 排除虚拟装配体 | CheckBox | False | `CodingCleanup_ExcludeVirtual` |
| 排除标准件 | CheckBox | False | `CodingCleanup_ExcludeStandard` |
| 排除外购件 | CheckBox | False | `CodingCleanup_ExcludePurchased` |
| 执行按钮 | Button | - | - |

### 窗口行为

- `TopMost = True`，浮动在 SW 之上
- 可拖拽移动（参照 Form3 的拖拽实现）
- 所有 CheckBox 和 TextBox 变更时自动保存到 `My.Settings`
- Form1.Button18 打开 Form4

## 执行逻辑

```
Sub ExecuteCodingCleanup(swApp, asmDoc)
    confString = asmDoc.GetActiveConfiguration.Name
    ProcessConfig(swApp, asmDoc, confString)        // 处理装配体自身当前配置
    For Each child In GetRootComponent.GetChildren  // 遍历子组件
        If ShouldSkip(child) Then Continue For      // 筛选过滤
        fopen = OpenDocSilent(child)
        If fopen IsNot Nothing Then
            childConfString = child.ReferencedConfiguration
            SyncTitleToCustomProperties(fopen, childConfString)
            fopen.Save3(0, 0, 0)
        End If
        If childType = swDocASSEMBLY Then
            ExecuteCodingCleanup(swApp, childModel) // 递归处理子装配体
        End If
    Next
End Sub
```

### 筛选条件（ShouldSkip）

按以下顺序判断，任一匹配则跳过：

1. **名称筛选**：TextBox 非空 且 `comp.Name` 不包含输入文本（排除型筛选：输入了关键词才筛选，不输入不筛选）
2. **类型筛选**：零件但未勾选"处理零件"，或装配体但未勾选"处理装配体"
3. **虚拟装配体**：勾选了排除 且 `comp.IsVirtual()` = True
4. **标准件**：勾选了排除 且 `GetCustomInfoValue(confString, "零件类型")` = "标准件"
5. **外购件**：勾选了排除 且 `GetCustomInfoValue(confString, "零件类型")` = "外购件"

### 同步属性（SyncTitleToCustomProperties）

从 `modelDoc.GetTitle()` 获取标题，去掉 ".SLDPRT"/".SLDASM" 扩展名后，写入配置的自定义属性：

| 属性名 | 值 |
|--------|-----|
| 物料编码 | c |
| 零件图号 | c |
| 文件名称 | c |

使用 `AddCustomInfo3` with `swCustomPropertyDeleteAndAdd` 写入。

## 涉及文件

| 文件 | 变更 |
|------|------|
| `Form4.vb` | 新建，窗口逻辑 + 持久化 + 拖拽 |
| `Form4.Designer.vb` | 新建，控件布局 |
| `Form4.resx` | 新建 |
| `Form1.vb` | 新增 Button18_Click，打开 Form4 |
| `Form1.Designer.vb` | 新增 Button18 |
| `外部程序.vbproj` | 添加 Form4 编译项 |
| `My Project/Settings.Designer.vb` | 新增 6 个 Settings 属性 |
| `My Project/Settings.settings` | 新增 6 个 Settings 项 |

## 参考代码

- 递归遍历 + 属性删除：`Form1.vb:1496-1542` (DelConfProps)
- 属性同步：`Form1.vb:349-359` (Button9_Click)
- Form3 拖拽模式：`Form3.vb:13-21`
- SWP 宏筛选逻辑：`If InStr(c, ".") > 0` + `If InStr(c, TextBox1.Value) > 0`
