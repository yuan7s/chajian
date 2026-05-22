# Form5 配置属性透明窗口 设计

## 目标

- 透明前置窗口，实时显示当前零件/选中零件的配置属性
- 按钮切换全部属性/关键属性
- 关键属性列表 + 透明度 + 颜色设置在窗口内置面板维护

## 窗口特性

- `Opacity = 0.25`（默认，可调节）
- `FormBorderStyle = None`
- `TopMost = True`
- `BackColor` 可配置（默认系统窗口色）
- 可拖拽移动（EnableDrag）

## 布局

| 控件 | 说明 |
|------|------|
| Label (顶部) | 显示零件名/选中零件名 |
| DataGridView | 两列：属性名、属性值，只读 |
| Button "切换" (底部左) | 全部属性 ↔ 关键属性，文字随状态变化 |
| Button "设置" (底部右) | 展开/收起设置面板 |
| Panel (底部) | 设置面板，初始隐藏 |

### 设置面板

| 控件 | 说明 |
|------|------|
| TrackBar | 透明度滑块 (0.15 ~ 1.0)，拖动时实时生效 |
| Label | 显示当前透明度百分比 |
| ComboBox | 预设颜色方案：自适应(默认)、终端绿、白字 |
| Label | "关键属性列表" |
| TextBox + Button "添加" | 输入新属性名并添加 |
| ListBox | 显示当前关键属性列表 |
| Button "删除选中" | 删除选中的属性项 |
| CheckBox "始终置顶" | 控制 TopMost |

## 功能

### 实时更新
- 监听 `_swApp.ActiveDocChangeNotify` 和 `NewSelectionNotify` 事件
- 事件触发时刷新 DataGridView

### 数据来源
- 优先：`SelectionManager.GetSelectedObject6(1, -1)` → `Component2.GetModelDoc2()`
- 默认：`swApp.ActiveDoc`
- 调用 `modelDoc.GetCustomInfoNames2(confString)` 遍历属性

### 切换逻辑
- 按钮点击切换显示模式
- 按钮文本 "关键属性" → 点击切到关键属性模式（按钮变 "全部属性"）
- 按钮文本 "全部属性" → 点击切到全部属性模式（按钮变 "关键属性"）

### 透明度
- TrackBar 范围 15~100，映射 Opacity 0.15~1.0
- 拖动实时更新 `Me.Opacity`
- 保存到 `My.Settings.Form5_Opacity`

### 颜色方案
- 自适应：`BackColor = SystemColors.Window`，`ForeColor = SystemColors.WindowText`
- 终端绿：`BackColor = Black`，`ForeColor = Color.Lime`
- 白字深底：`BackColor = Color.FromArgb(30,30,30)`，`ForeColor = Color.White`
- 保存到 `My.Settings.Form5_ColorScheme`

### 关键属性持久化
- `My.Settings.CustomPropertyKeys`（`StringCollection`）
- 添加/删除时自动保存

### 始终置顶
- CheckBox 控制 `TopMost`

## Form1 集成

- 新增 `Button19`（GroupBox2 内），点击通过 `GetSelectedSwApp()` 获取 SW 实例传给 Form5

## 新建文件

| 文件 | 说明 |
|------|------|
| Form5.vb | 逻辑代码 |
| Form5.Designer.vb | 窗体布局 |
| Form5.resx | 资源 |
