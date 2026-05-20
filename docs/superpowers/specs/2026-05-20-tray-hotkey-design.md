# 托盘图标 + 全局快捷键设计

## 目标

- Form1 关闭时隐藏到系统托盘而非退出
- 全局快捷键 Ctrl+F1 切换主窗口显示/隐藏
- Form2 显示当前快捷键信息

## 托盘图标

### NotifyIcon
- 图标：使用应用程序默认图标
- 托盘图标工具提示："外部程序"
- 双击托盘图标 → 显示并激活主窗口

### 右键菜单
- "显示主窗口" → `Form1.Show()` + `BringToFront()`
- "退出" → 移除托盘图标，释放资源，`Application.Exit()`

### 窗口关闭行为
- `Form1.FormClosing`：拦截关闭，`e.Cancel = True`，`Hide()` 隐藏到托盘
- `ShutDownStyle` 改为 `AfterAllFormsClose`（Application.Designer.vb）

### 生命周期
- Form1.Load 时创建 NotifyIcon
- 退出时 Dispose NotifyIcon

## 全局快捷键 Ctrl+F1

### 注册
- Win32 `RegisterHotKey(Me.Handle, 1, MOD_CONTROL, VK_F1)` 在 Form1.Load 中注册
- 重写 `WndProc` 捕获 `WM_HOTKEY`（0x0312）
- 收到热键时调用 `ToggleVisibility()`

### ToggleVisibility
- 如果 `Visible` → `Hide()`
- 如果不 `Visible` → `Show()` + `BringToFront()` + `WindowState = Normal`

### 注销
- Form1.FormClosing 中（真正退出时）`UnregisterHotKey`

## Form2 快捷键显示

- 新增只读 Label，文本："全局快捷键: Ctrl+F1 — 显示/隐藏主窗口"
- 布局：放在现有控件下方或合适位置

## 修改文件

| 文件 | 变更 |
|------|------|
| Form1.vb | NotifyIcon、右键菜单、WndProc、热键注册/注销、FormClosing 拦截 |
| Form2.Designer.vb | 新增快捷键说明 Label |
| Form2.resx | 新增 Label 的资源定义 |
| Application.Designer.vb | ShutDownStyle → AfterAllFormsClose |
