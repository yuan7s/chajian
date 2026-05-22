# Form5 配置属性透明窗口 设计

## 目标

- 透明前置窗口，实时显示当前零件/选中零件的配置属性
- 全部属性/关键属性切换
- 关键属性列表在窗口内置面板维护

## 窗口特性

- `Opacity = 0.25`（高透明）
- `FormBorderStyle = None`
- `TopMost = True`
- `BackColor` 设为透明色，文字自然适应背景
- 可拖拽移动（EnableDrag）

## 布局

| 控件 | 说明 |
|------|------|
| Label (顶部) | 显示零件名/选中零件名 |
| DataGridView | 两列：属性名、属性值，只读 |
| CheckBox (底部) | "只显示关键属性" 切换 |
| Button (底部) | "设置" 展开设置面板 |
| Panel (底部) | 设置面板，初始隐藏 |

### 设置面板

| 控件 | 说明 |
|------|------|
| TextBox | 输入新属性名 |
| Button | "添加" 添加属性 |
| ListBox | 显示当前关键属性列表 |
| Button | "删除选中" |
| Button | "保存" 持久化 |

## 功能

### 实时更新
- 监听 `_swApp.ActiveDocChangeNotify` 和 `NewSelectionNotify` 事件
- 事件触发时刷新 DataGridView

### 数据来源
- 优先：`SelectionManager.GetSelectedObject6(1, -1)` → `Component2.GetModelDoc2()`
- 默认：`swApp.ActiveDoc`
- 调用 `modelDoc.GetCustomInfoNames2(confString)` 遍历属性

### 切换逻辑
- CheckBox 选中：仅显示关键属性列表中的属性
- CheckBox 未选中：显示全部属性

### 关键属性持久化
- `My.Settings.CustomPropertyKeys`（`StringCollection`）
- 添加/删除时自动保存

## Form1 集成

- 新增 `Button19`，点击通过 `GetSelectedSwApp()` 获取 SW 实例传给 Form5

## 新建文件

| 文件 | 说明 |
|------|------|
| Form5.vb | 逻辑代码 |
| Form5.Designer.vb | 窗体布局 |
| Form5.resx | 资源 |
