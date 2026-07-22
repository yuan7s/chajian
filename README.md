# chajian —— SolidWorks 桌面伴侣工具栏

基于 WPF 的 SolidWorks 效率工具，通过本地 HTTP/WebSocket 与 SolidWorks 插件通信，实现快捷操作。

## 功能

- **始终置顶工具栏** — 悬浮在 SolidWorks 窗口上方，`Ctrl+F1` 全局热键显示/隐藏
- **属性悬浮框** — 鼠标悬停时浮动显示零件/装配体属性
- **编码整理** — 将文件名同步写入物料编码、零件图号、文件名称等属性
- **属性读写** — 批量读写自定义属性、删除属性、配置属性管理
- **批量重命名** — 零件/装配体/工程图重命名，自动更新引用、复制关联工程图
- **工程图工具** — 另存 DWG/PDF、旋转视图、一键 ISO 标准、替换图纸格式/标准
- **装配体工具** — 基准面自动装配、删除错误配合、组件排序、隐藏配置名称
- **组件树配置** — 开启/关闭特征树显示项（注解、基准面、原点等）
- **工程图标注** — 按视图图层自动分配标注

## 系统要求

- Windows 10/11 x64
- .NET 9.0 Desktop Runtime
- SolidWorks 2022+ (64-bit)

## 构建

```bash
# 还原依赖
dotnet restore ExternalProgram.sln -r win-x64

# 先构建 SwAddin（net48 插件）
dotnet build ExternalProgram.SwAddin/ExternalProgram.SwAddin.csproj -c Release

# 发布单文件 exe
dotnet publish ExternalProgram.csproj -c Release -r win-x64 --self-contained false -o publish/ExternalProgram
```

## 架构

```
ExternalProgram (WPF)                   SolidWorks 进程
    SwAddinClient                       SwAddin (COM 插件)
      ├─ HTTP /health      ────────►   AddinHttpServer (端口 32128+)
      ├─ HTTP POST /command ────────►   命令调度分发
      └─ WebSocket /events  ◄────────  事件广播
```

两个项目通过 `127.0.0.1` 回环通信，编译时无 COM 依赖。SwAddin 由 SolidWorks 加载时自动启动 HTTP 服务，主程序发现 SolidWorks 窗口后自动连接。

## 项目结构

| 项目 | 目标框架 | 说明 |
|------|---------|------|
| `ExternalProgram.csproj` | net9.0-windows | WPF 工具栏主程序 |
| `ExternalProgram.SwAddin` | net48 | COM 注册 SolidWorks 插件 |

## 许可

MIT
