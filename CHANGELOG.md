# 更新日志

## v1.0.10 - 2026-05-23

### 修复

- 修复 GitHub Release 页面没有显示详细更新内容的问题。
- 改用 `ncipollo/release-action` 原生的 `generateReleaseNotes: true`，让 GitHub 在创建或更新 Release 时直接生成正文。
- 使用 `${{ secrets.GITHUB_TOKEN }}` 作为 Release 操作 token，避免在 workflow 中手动处理或暴露个人访问令牌。
