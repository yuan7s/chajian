# 更新日志

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
