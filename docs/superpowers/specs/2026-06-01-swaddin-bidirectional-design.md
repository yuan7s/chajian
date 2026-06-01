# 外部程序.SwAddin —— 双向通信 SolidWorks 插件设计

## 概述

在 `外部程序.sln` 中新建 `外部程序.SwAddin`（C# .NET Framework 4.8）项目，参照 `ReadBom.SwAddin` 的 COM 注册和 HTTP 桥接模式，增加 WebSocket 双向推送通道。外部程序移除对所有 SolidWorks COM 互操作程序集的直接依赖，改为通过 HTTP + WebSocket 与插件通信。

## 架构

```
外部程序 (WPF UI, VB.NET)
    │
    ├── HTTP POST /command ──→ 外部程序.SwAddin (C#, in SW process)
    │                          │
    └── WebSocket ←──────────── 事件推送
               ws://127.0.0.1:32128/events
```

- **端口**：`32128`（避开 readbom 的 `32127`）
- **HTTP 端点**：`POST http://127.0.0.1:32128/command`，JSON 请求/响应
- **WebSocket 端点**：`ws://127.0.0.1:32128/events`

## 项目结构

```
外部程序.sln
├── 外部程序.vbproj              (现有，改为纯 HTTP/WS 客户端)
└── 外部程序.SwAddin.csproj      (新建)
    ├── SwAddin.cs               COM 入口、注册、生命周期
    ├── AddinHttpServer.cs       HttpListener、命令路由、JSON
    ├── AddinWebSocket.cs        WebSocket 管理、事件广播
    ├── AddinHttpServer.Commands.cs  SW 操作实现
    └── CommandRequest.cs        DTO
```

### SwAddin.cs

- `[SwAddin]` 特性，`LoadAtStartup = true`
- `ConnectToSW`：创建隐藏 WinForms Control 用于线程调度，启动 HTTP + WebSocket 服务
- `DisconnectFromSW`：关闭 WebSocket 连接，停止 HTTP，释放资源
- COM 注册函数写 `HKLM` 和 `HKCU` 注册表项

### AddinHttpServer.cs

- `HttpListener` 监听 `http://127.0.0.1:32128/`
- `POST /command` 路由，JSON 反序列化为 `CommandRequest`
- `ExecuteCommand` switch 分发到具体操作
- 响应格式：`{ ok: bool, data: object }` 或 `{ ok: false, error: string }`
- 通过 `Control.BeginInvoke` 将所有 SW 操作调度到主线程

### AddinWebSocket.cs

- 基于 `System.Net.WebSockets.HttpListenerWebSocketContext`
- 接受 WebSocket 连接后维护客户端列表
- 事件广播方法：`BroadcastEvent(type, payload)`
- 心跳：每 30 秒 ping/pong
- 推送通过 `Control.BeginInvoke` 调度到 SW 主线程后执行

### AddinHttpServer.Commands.cs

全量迁移当前 SW 操作：

| 命令 | 功能 | 来源 |
|------|------|------|
| `ping` | 健康检查 | - |
| `active-document` | 活动文档路径/标题/类型 | UpdateStatusBar |
| `save-dwg` | 工程图另存为 DWG | SaveActiveDrawingAsDwg |
| `save-pdf` | 工程图另存为 PDF | SaveActiveDrawingAsPdf |
| `open-file-location` | 获取选中/活动模型的完整路径 | GetActiveOrSelectedModelPath |
| `rotate-drawing-view` | 旋转选中工程图视图 | RotateSelectedDrawingView |
| `get-component-tree` | 装配体子件树 | SubAsmsjs |
| `sort-components` | 装配体特征树排序 | SortComponentsInFolder |
| `read-properties` | 读取模型自定义属性 | PropertyOverlayWindow |
| `write-properties` | 写入模型自定义属性 | PropertyOverlayWindow |
| `rename-component` | 重命名组件并另存 | RenameWindow |
| `coding-cleanup` | 编码清理 | CodingCleanupWindow |
| `drawing-settings` | 工程图设置 | DrawingSettingsWindow |
| `get-bounding-box` | 包围盒计算 | PropertyOverlayWindow |
| `rebuild` | 重建模型 | - |
| `save` | 保存文档 | - |
| `open-document` | 打开文档 | - |

### CommandRequest.cs

```csharp
public class CommandRequest
{
    public string command { get; set; }
    public Dictionary<string, object> args { get; set; }
}
```

## WebSocket 推送事件

| 事件 | 触发时机 | 替代 |
|------|----------|------|
| `doc-changed` | SW 活动文档切换 | StatusTimer |
| `selection-changed` | 选中对象变化 | StatusTimer |
| `doc-closed` | 文档关闭 | - |
| `sw-shutdown` | SolidWorks 进程退出 | - |

事件格式：`{ type: string, timestamp: string, data: object }`

## 外部程序改动

### 移除
- `Interop.SldWorks` 和 `Interop.SwConst` 引用
- 所有 `SldWorks.*` 类型的直接使用
- `StatusTimer` 轮询
- 进程选择下拉框（插件在 SW 内部运行，无需选实例）
- `Marshal.GetActiveObject` / `CreateObject` 连接逻辑

### 新增
- `SwAddinClient` 类（HTTP POST + ClientWebSocket）
- 连接管理（WebSocket 断线重连：3 次指数退避，失败后降级到 5 秒 HTTP ping 轮询）
- 事件处理回调注册机制

### 保留不变
- 所有 WPF 窗口 XAML 布局
- WinForms 托盘图标和热键
- 业务逻辑（仅替换数据获取方式）

## 构建和部署

- 插件 DLL 输出到 `外部程序.SwAddin\bin\Debug\net48\`
- 首次安装需手动注册：`RegAsm.exe 外部程序.SwAddin.dll /codebase`
- 外部程序的 PostBuild 将插件输出复制到发布目录
- 解决 DLL 锁定：构建前检测并提示关闭 SolidWorks（同 ReadBom.SwAddin 的 MSBuild 目标）

## 依赖

**插件引用**：
- `Interop.SldWorks` (lib/)
- `Interop.SwConst` (lib/)
- `SolidWorks.Interop.swpublished` (SW 安装目录)
- `SolidWorksTools` (SW 安装目录)
- `System.Net.Http`、`System.Windows.Forms`、`System.Web.Extensions`（Framework）

**外部程序移除引用**：
- `Interop.SldWorks`
- `Interop.SwConst`

## 风险

- WebSocket 在 .NET Framework 4.8 中需要 `System.Net.WebSockets` 支持，`HttpListener` 本身不直接提供 WebSocket 升级——需在 HTTP 请求处理中检测 `IsWebSocketRequest` 并调用 `AcceptWebSocketAsync`
- 双向通信增加连接管理复杂度，调试需确保 SW 主线程不阻塞
