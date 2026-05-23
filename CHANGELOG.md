# 更新日志

## v1.0.14 - 2026-05-23

### 新增

- 新增 WPF 属性浮窗，用于显示当前文档或装配体选中零部件的关键属性。
- 新增 WPF 配置属性设置窗口，并改为 XAML + 后台代码结构，便于 Rider 可视化编辑。
- 配置属性设置窗口支持关键属性增删、上移、下移排序，并即时刷新已打开的属性浮窗。

### 优化

- 属性浮窗去掉表头、收窄窗体、固定右上角初始位置，并保留透明背景下的黑色清晰文字。
- 配置属性设置窗口优化窄窗体布局，调整关键属性列表、输入框、上下移动按钮和底部按钮分组。
- 属性浮窗支持 `Ctrl+F2` 切换鼠标穿透，并在关闭时清理热键和 SolidWorks 事件。

### 修复

- 修复 WPF 属性浮窗不会随 SolidWorks 选择变化刷新的问题。
- 修复打开程序激活 SolidWorks 时会改变最大化窗口状态的问题。
- 修复材质或材料属性为空时显示为空白的问题，现在显示为“材质未设置”。
- 修复配置属性设置窗口中“鼠标穿透”等文字在窄窗体下溢出或覆盖控件的问题。

## v1.0.13 - 2026-05-23

### 新增

- Form3 重命名界面新增默认勾选的属性写入项：`文件名称`、`物料编码`、`零件图号`。
- Form3 新增“另存”流程，可生成副本、打开新文件，并复制关联工程图。
- Form1 装配体排序新增小型进度提示窗口，显示当前排序阶段。

### 优化

- 优化装配体排序速度，减少轻化组件强制还原、选择集操作、模型解析和调试输出。
- Form3 实时重名检查改为轻量检查，避免输入时遍历装配体。

### 修复

- 修复“另存”在装配体中可能替换原选中组件引用的问题。
- 重命名改用 SolidWorks 原生 `RenameDocument`，并同步修改工程图名称和引用。

## v1.0.12 - 2026-05-23

### 修复

- 修复 GitHub Actions 因第三方 Release action 的 Node 运行时限制导致发布失败的问题。
- 发布 Release 改为使用 GitHub-hosted runner 自带的 `gh release` 命令。
- 移除不必要的 workflow artifact 上传步骤，Release 页面只保留明确命名的 `chajian.zip`。
- 增加 `FORCE_JAVASCRIPT_ACTIONS_TO_NODE24=true`，让仍然需要的官方 JavaScript action 使用 Node 24。

## v1.0.11 - 2026-05-23

### 修复

- 修复 Release 页面只显示 `Full Changelog`，没有显示详细更新内容的问题。
- Release 正文改为自动读取 `CHANGELOG.md` 中当前 tag 对应的版本章节。
- 将 Release 压缩包名称固定为 `chajian.zip`，避免生成 `default.zip`。
- 改用 `softprops/action-gh-release` 上传 Release 资产并覆盖同名文件。

## v1.0.10 - 2026-05-23

### 修复

- 修复 GitHub Release 页面没有显示详细更新内容的问题。
- 改用 `ncipollo/release-action` 原生的 `generateReleaseNotes: true`，让 GitHub 在创建或更新 Release 时直接生成正文。
- 使用 `${{ secrets.GITHUB_TOKEN }}` 作为 Release 操作 token，避免在 workflow 中手动处理或暴露个人访问令牌。
