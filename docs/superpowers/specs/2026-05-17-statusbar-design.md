
# Form1 状态栏重构设计

## 目标

将 Form1 底部的 ComboBox1、Label1、Label2、Label3、Button15 重构为使用 .NET StatusStrip 的统一状态栏，提升多 SolidWorks 进程场景下的可辨识性和 UI 整洁度。

## 当前问题

- ComboBox1、Label1、Label2、Label3 散落在窗体底部，视觉上不成组
- ComboBox1 仅显示文档名，无法区分多个同名文档的不同 SW 进程
- Label2 为静态"工作目录"文本，Label1 无前缀标签，信息表达不够清晰
- Button15（刷新）位于 ComboBox1 上方，不是直观的位置

## 新布局

StatusStrip 固定在窗体底部，从左到右排列：

| 序号 | 控件 | 说明 |
|------|------|------|
| 1 | ToolStripButton | 刷新按钮，替代 Button15，文本"刷新" |
| 2 | ToolStripComboBox | 替代 ComboBox1，显示 `PID: 文档名` |
| 3 | ToolStripSeparator | 竖线分隔 |
| 4 | ToolStripStatusLabel | 静态前缀 `选中文件:` |
| 5 | ToolStripStatusLabel | 动态文本，替代 Label1 |
| 6 | ToolStripSeparator | 竖线分隔 |
| 7 | ToolStripStatusLabel | 静态前缀 `工作目录:`，替代 Label2 |
| 8 | ToolStripStatusLabel | 动态文本，替代 Label3 |

## 移除控件

- ComboBox1（替换为 ToolStripComboBox）
- Label1、Label2、Label3（替换为 ToolStripStatusLabel）
- Button15（替换为 ToolStripButton）

## 代码变更

### ToolStripComboBox 填充 (`PopulateSolidWorksProcesses`)

每项格式：`PID: 文档名`，例如 `12345: 底板.SLDASM`。保留 `SwProcessInfo` 类供内部使用，`ToString()` 返回 `PID: Title` 格式。

### 状态更新 (`UpdateStatusBar`，原 `UpdateLabel1`)

- 选中文件标签：显示 SW 选中对象的文件名，无选择时显示文档标题
- 工作目录标签：显示文档所在目录路径
- 未连接时显示"未连接"/空白

### 事件绑定

- ToolStripButton Click → 原 Button15 的刷新逻辑
- ToolStripComboBox SelectedIndexChanged → 原 ComboBox1 的进程切换逻辑
- 保持所有现有 SW 事件处理（ActiveDocChangeNotify 等），更新调用目标

### 不在本次范围

- Form2、Form3 的状态栏相关改动
- 现有按钮功能的任何逻辑变更