# InsightCleanerAI

InsightCleanerAI 是一个面向 Windows 的可视化磁盘分析与清理工具。它模仿 SpaceSniffer 的 treemap 展示方式，结合本地/云端 AI 对每个目录或文件生成用途说明，让你在不熟悉软件名的情况下也能快速判断“能不能删”“删了会怎样”。

## 功能介绍

- **多种 AI 模式**：可以只用内置启发式规则、本地 LLM（Ollama、koboldcpp 等）、或者调用 OpenAI/DeepSeek/百度千帆等云端接口，还支持搜索 API 作为补充。
- **缓存策略**：AI 说明会缓存在 `insights.db`，支持“仅按路径匹配缓存”模式，避免文件大小变化导致重复调用。
- **黑/白名单管理**：扫描或 AI 上传都可以单独配置名单（黑名单/白名单二选一），敏感目录会在 UI 中标记 `[禁止]` 并跳过云端。
- **本地化 UI**：默认中文界面，支持离线/禁止徽标、双击打开资源管理器、右键删除等交互。
- **日志&调试**：`%AppData%\InsightCleanerAI\logs\debug.log` 记录扫描与 AI 调用过程，便于诊断。

## 环境需求

- Windows 10/11
- .NET 5.0 SDK（开发/构建）或 .NET 5 Desktop Runtime（运行）

## 构建与运行

```powershell
# 克隆仓库后
dotnet build InsightCleanerAI/InsightCleanerAI.csproj
dotnet run --project InsightCleanerAI/InsightCleanerAI.csproj
```

### 打包发布

```powershell
dotnet publish InsightCleanerAI/InsightCleanerAI.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true
```

发布后的可执行文件位于 `bin/Release/net5.0-windows/win-x64/publish/`。

## 使用说明

1. **选择根路径**：在主界面顶部指定要分析的目录，然后点击“扫描”。
2. **设置面板**：通过菜单 `设置 → 打开设置...` 配置隐私模式、最大节点数、AI 模式、缓存目录等。
3. **名单管理**：`设置 → 编辑名单...` 中可以分别配置“扫描名单”和“识别名单”，支持黑/白名单模式（每行一个绝对路径）。
4. **AI 模式**：
   - *关闭*：仅显示树状结构，不做 AI 说明。
   - *离线规则*：使用内置启发式分类器（最快，不联网）。
   - *本地 LLM 服务*：填入 HTTP Endpoint、模型名，可配合 Ollama、koboldcpp 等。
   - *云端搜索*：配置符合 OpenAI 风格的 Endpoint/Key；还可以选填百度搜索 API 作为搜索来源。
5. **API Key**：默认不写入 `settings.json`。在设置中勾选“保留 API Key”后才会持久化。

### 注意事项

- 扫描过程中可以点击“取消”。若要清空缓存，使用设置界面中的“清空缓存”按钮或删除 `%AppData%\InsightCleanerAI\insights.db`。
- 以管理员身份运行能访问更多系统目录，但也会触发大量 AI 请求，建议搭配黑/白名单限制扫描范围。

## 目录结构

```
InsightCleanerAI/
├── Models/               # 基础数据结构与枚举
├── ViewModels/           # WPF MVVM 逻辑
├── Services/             # 文件扫描、云端/本地 LLM 适配器、缓存等
├── Infrastructure/       # 配置存储、日志、转换器等
├── Resources/            # 本地化字符串、XAML 资源
└── bin/, obj/            # 构建产物
```

## 贡献

欢迎提交 Issue 或 PR，提交前请运行：

```powershell
dotnet build InsightCleanerAI/InsightCleanerAI.csproj -c Release
```

确保编译通过无错误。

---

## 🎯 功能增强与Bug修复（2025版本更新）

### 一、原项目Bug修复

#### 1. 本地LLM超时问题
**问题描述：**
- 所有LocalLLM请求在100秒时超时，文件显示"尚未生成说明"
- 大参数模型（如 gemma3:27b）需要40-60秒响应时间，原默认超时不足

**根本原因：**
1. 静态 HttpClient 使用默认100秒超时，无法通过 CancellationToken 覆盖
2. `ReadAsStringAsync()` 使用了错误的 CancellationToken，未能响应配置的超时时间

**修复方案：**
- 将 `HttpClient.Timeout` 设置为 `Timeout.InfiniteTimeSpan`，完全依赖 CancellationTokenSource 控制超时
- 修复 `ReadAsStringAsync(linkedCts.Token)` 使用正确的超时 Token
- 新增 `LocalLlmRequestTimeoutSeconds` 配置项，默认300秒

**修改文件：**
- `Services/LocalLlmInsightProvider.cs:22-25, 75`
- `Models/AiConfiguration.cs:35`
- `Infrastructure/UserConfig.cs:32`

**验证结果：**
- gemma3:27b 成功生成中文分析响应，响应时间约40秒
- 大模型（27B+）可正常工作，建议配置600秒或更长超时

---

### 二、新增功能

#### 功能模块 1：文件删除功能

**1.1 管理员权限检测**
- 新增 `Infrastructure/AdminHelper.cs` 工具类
- 实现 `IsRunningAsAdmin()` 静态方法，基于 Windows API（WindowsIdentity 和 WindowsPrincipal）
- 为删除功能提供安全的权限检查基础

**1.2 节点详情面板删除按钮**
- 在右侧节点详情面板新增红色"删除文件/文件夹"按钮
- 未选中节点时自动禁用按钮
- 非管理员用户点击时弹出权限提示，引导以管理员身份重启程序
- 管理员用户点击时显示二次确认对话框

**1.3 安全保护机制**
- 隐私模式下（`FullPath` 为空）自动禁用删除功能
- 扫描过程中禁止删除操作
- 删除操作需要用户明确确认

**修改文件：**
- `MainWindow.xaml` - 添加删除按钮UI
- `MainWindow.xaml.cs` - 实现删除逻辑和权限控制

---

#### 功能模块 2：AI模型管理系统

**2.1 模型列表自动获取服务**

新增 `Services/ModelListService.cs` 统一服务，支持：

**云端模型获取：**
- OpenAI 标准接口 (`GET /v1/models`)
- 自动解析 `{ "data": [{ "id": "model-name" }] }` 格式
- 智能端点构建（自动从 `/chat/completions` 转换为 `/models`）
- 10秒超时保护，完整的错误日志记录

**本地模型获取：**
- 优先尝试 Ollama API (`GET /api/tags`)
- 解析 `{ "models": [{ "name": "model-name" }] }` 格式
- 失败后自动回退到 OpenAI 兼容接口
- 支持多种响应格式自适应

**2.2 ViewModel 模型列表支持**

修改 `ViewModels/MainViewModel.cs`，新增：

**属性：**
- `CloudModels` - 云端模型列表（ObservableCollection<string>）
- `LocalModels` - 本地模型列表（ObservableCollection<string>）
- `IsLoadingCloudModels` / `IsLoadingLocalModels` - 加载状态标志

**方法：**
- `LoadCloudModelsAsync()` - 异步加载云端可用模型
- `LoadLocalModelsAsync()` - 异步加载本地可用模型

**特性：**
- 防止重复加载（加载中时忽略新请求）
- 完整的异常处理和日志记录
- 加载完成后自动清空旧列表并填充新数据

**2.3 设置界面模型选择器重构**

修改 `SettingsWindow.xaml` 和 `SettingsWindow.xaml.cs`：

**云端模型配置：**
- 文本输入框 → 可编辑的 ComboBox 下拉框
- 新增"获取模型"按钮（位于下拉框右侧）
- 加载中按钮显示"加载中..."并自动禁用
- 成功后弹窗："成功获取 X 个模型"
- 失败时显示详细排查提示（服务地址、API Key、网络连接）

**本地模型配置：**
- 文本输入框 → 可编辑的 ComboBox 下拉框
- 新增"获取模型"按钮
- 仅在 AI 模式为"本地 LLM 服务"时启用
- 失败时提示检查：本地 LLM 服务状态、服务地址、接口支持

**用户体验提升：**
- 下拉框支持手动输入（兼容未列出的模型）
- 智能启用/禁用逻辑，避免误操作
- 中文友好提示，错误信息包含明确的解决方向

**2.4 启动时模型验证与自动恢复**

实现位置：`MainViewModel.cs:86-151`

**启动流程：**
```
加载配置 → 保存模型名 → 清空显示 → 异步获取模型 → 验证并恢复
```

**验证逻辑：**
1. 启动时清空模型名称显示（保持UI干净）
2. 根据AI模式自动获取可用模型列表
3. 验证保存的模型是否在可用列表中
4. 如果保存的模型（如 gemma3:27b）在列表中，自动恢复显示
5. 如果不在列表中，保持为空

**2.5 扫描前模型可用性检查**

实现位置：`MainViewModel.cs:475-493`

**拦截逻辑：**
- LocalLlm 模式：检查 `LocalModels.Count > 0`，否则拦截并提示
- KeyOnline 模式：检查 `CloudModels.Count > 0`，否则拦截并提示
- 离线规则和关闭模式：不检查，正常运行

**新增状态消息：**
- `StatusNoLocalModels`: 未获取到可用的本地模型（Strings.resx:131-136）
- `StatusNoCloudModels`: 未获取到可用的云端模型（Strings.resx:131-136）

---

#### 功能模块 3：本地LLM响应解析增强

修改 `Services/LocalLlmInsightProvider.cs`

**3.1 多格式响应解析**
- Ollama 格式：`{ "response": "..." }`
- OpenAI 格式：`{ "choices": [{ "message": { "content": "..." } }] }`
- 简化格式：`{ "content": "..." }`, `{ "text": "..." }`, `{ "output": "..." }`
- 特殊格式：`choices[].text` 字段（某些实现）

**3.2 容错机制**
- JSON 解析失败时，使用原始响应（截断至300字符）
- 确保即使格式不标准，也能显示模型输出
- 彻底消除"暂无说明"的情况（除非模型真的无返回内容）

**3.3 调试日志增强**
- 记录每次本地 LLM 请求：`本地LLM请求：{文件名}（超时={timeoutSeconds}秒）`
- 记录响应前200字符：`本地LLM响应：{前200字符}...`
- 记录解析失败警告：`本地LLM响应解析失败，使用原始响应`
- 记录成功和异常详情

**3.4 文本处理优化**
- 自动 `Trim()` 去除首尾空白
- 智能截断超长响应（避免UI卡顿）
- 保留完整语义信息

**日志位置：**
`%AppData%\InsightCleanerAI\logs\debug.log`

---

#### 功能模块 4：设置界面帮助系统

修改 `SettingsWindow.xaml` 和 `SettingsWindow.xaml.cs`

**4.1 界面布局调整**

- 窗口宽度扩展：720px → 750px
- 新增第三列（28px）专门放置帮助按钮
- 调整控件宽度以适应新布局：
  - ComboBox：340px → 310px
  - TextBox（缓存目录/数据库路径）：360px → 330px
- 保持原有二列布局不变，第三列独立显示

**4.2 帮助按钮设计**

**UI规格：**
- 图标：❓（Unicode 问号字符）
- 尺寸：24x24px（紧凑设计）
- 边距：左侧4px（与控件保持间距）
- 字体大小：12px
- 工具提示：「查看帮助」

**覆盖范围（23个配置项）：**
1. 隐私模式 - 公开/脱敏模式说明
2. AI模式 - 四种模式对比（关闭/离线规则/本地LLM/云端）
3. 询问范围 - 三种扫描深度对比
4. 云端API Key - 密钥获取和保存策略
5. 云端服务地址 - 常见服务端点示例
6. 云端模型 - 模型选择和手动输入
7. 云端请求超时 - 超时时间建议
8. 云端并发限制 - 并发数与限流平衡
9. AI批量大小 - 分批处理机制
10. AI总结点数 - 成本控制说明
11. 搜索API地址 - 可选搜索功能
12. 搜索API Key - 搜索密钥配置
13. 本地LLM服务地址 - Ollama/koboldcpp端点
14. 本地模型名称 - 常见模型推荐
15. 本地LLM API Key - 可选认证
16. 最大扫描深度 - 深度限制原因
17. 最大节点数 - 防止程序卡死
18. 扫描延迟 - SSD/HDD不同建议
19. 缓存目录 - 临时文件存储
20. 数据库路径 - AI结果持久化
21. 缓存匹配模式 - 严格/宽松匹配
22. 保留API Key - 安全性权衡
23. 包含隐藏文件 - 性能影响说明

**4.3 帮助内容设计**

**实现方式：**
- 代码位置：`SettingsWindow.xaml.cs:248-320`
- 使用 `ShowHelp(string title, string message)` 统一方法
- MessageBox 弹窗显示，`MessageBoxImage.Information` 图标
- 标题格式：「帮助 - {配置项名称}」

**内容特点：**
- 中文友好，避免技术术语
- 包含默认值和推荐值
- 提供常见服务/模型示例
- 说明配置项对性能/成本的影响
- 给出明确的使用建议

**示例内容：**
```
隐私模式：
• 公开模式：向AI发送真实完整路径
  适用：本地AI（Ollama），数据不会上传互联网

• 脱敏模式：隐藏真实路径，只显示相对结构
  适用：云端AI，保护隐私信息
```

**4.4 用户体验优化**

- 帮助按钮与配置项垂直对齐（`VerticalAlignment="Center"`）
- CheckBox行的按钮与CheckBox顶部对齐（`Margin="4,12,0,0"`）
- 所有帮助文本使用 `\n` 换行，MessageBox自动处理
- 引号使用转义（`\"`）确保C#字符串正确解析

---

### 三、技术架构更新

**新增依赖：** 无（所有功能使用 .NET 5.0 标准库实现）

**核心类图：**
```
Infrastructure/
  ├── AdminHelper.cs              # 管理员权限检测（新增）
  └── （原有文件...）

Services/
  ├── ModelListService.cs         # 模型列表获取（新增）
  ├── LocalLlmInsightProvider.cs  # 本地LLM适配器（增强）
  └── （原有文件...）

ViewModels/
  └── MainViewModel.cs            # 增加模型列表管理（增强）

Views/
  ├── SettingsWindow.xaml         # 设置界面UI（增强：帮助按钮）
  └── SettingsWindow.xaml.cs      # 设置界面逻辑（增强：帮助系统）
```

**API 兼容性：**
- **Ollama**: 完全支持 `/api/tags` 和 `/api/generate`
- **OpenAI**: 支持标准 `/v1/models` 和 `/v1/chat/completions`
- **DeepSeek**: 支持（OpenAI 兼容）
- **其他兼容服务**: 自动适配

---

### 四、使用建议

**设置界面：**
- 所有配置项右侧都有❓帮助按钮
- 点击帮助按钮可快速查看配置说明
- 帮助文本包含默认值、推荐值和使用建议
- 适合新用户快速了解各配置项的作用

**删除功能：**
- 建议以管理员身份运行程序以获得完整功能
- 删除前请仔细确认，已删除文件无法恢复
- 建议先在测试目录测试删除功能

**模型选择：**
- 云端模型：点击"获取模型"后，从下拉框选择性能最优的模型
- 本地模型：确保 Ollama 或其他 LLM 服务已启动，然后获取模型列表
- 如果列表中没有你想要的模型，可以直接手动输入模型名称

**本地LLM调试：**
- 如果发现 AI 说明不准确，查看 `debug.log` 中的原始响应
- 检查模型是否正确理解了中文提示词
- 尝试更换更强大的模型（如 qwen、deepseek-coder 等）

---

## 版本历史

**v1.1.8 (2025-11-18)** - 设置界面帮助系统
- 🆘 **全面的帮助按钮系统**：
  - 为所有23个配置项添加❓帮助按钮
  - 窗口宽度扩展至750px以容纳帮助按钮（原720px）
  - 新增28px第三列专门放置帮助按钮
  - 调整控件宽度保持界面紧凑（ComboBox: 310px, TextBox: 330px）
- 📖 **详细的中文帮助内容**：
  - 每个配置项都有清晰的说明文档
  - 包含默认值、推荐值和使用建议
  - 提供常见服务端点和模型示例
  - 说明配置对性能、成本的影响
- 🎨 **用户体验优化**：
  - 24x24px紧凑按钮设计，不占用过多空间
  - 所有按钮与配置项垂直对齐
  - 工具提示「查看帮助」
  - MessageBox弹窗显示，标题格式统一为「帮助 - {配置项名称}」
- 📝 **覆盖配置项**（完整列表）：
  - 基础配置：隐私模式、AI模式、询问范围
  - 云端配置：API Key、服务地址、模型、超时、并发限制
  - AI控制：批量大小、总结点数
  - 搜索配置：搜索API地址、搜索API Key
  - 本地LLM：服务地址、模型名称、API Key
  - 扫描限制：最大深度、最大节点数、扫描延迟
  - 存储配置：缓存目录、数据库路径、缓存匹配模式
  - 高级选项：保留API Key、包含隐藏文件
- 💡 **适用场景**：
  - 新用户快速了解各配置项作用
  - 避免配置错误导致扫描失败
  - 提供最佳实践建议
- 🔧 **修改文件**：
  - `SettingsWindow.xaml` - UI布局和帮助按钮
  - `SettingsWindow.xaml.cs:248-320` - 帮助文本和事件处理

**v1.1.7 (2025-11-18)** - 模型验证与扫描拦截优化
- 🎯 **启动时模型验证逻辑**：
  - 启动时清空模型名称显示，保持UI干净
  - 自动获取可用模型列表后，验证保存的模型是否在列表中
  - 如果保存的模型（如gemma3:27b）不在可用列表中，保持为空
  - 如果在列表中，自动恢复显示
  - 代码位置：`MainViewModel.cs:86-151`
- 🛡️ **扫描前模型可用性检查**：
  - 根据AI模式智能检查：LocalLlm检查本地模型，KeyOnline检查云端模型
  - 如果选择LocalLlm但`LocalModels.Count == 0`，拦截扫描并提示
  - 如果选择KeyOnline但`CloudModels.Count == 0`，拦截扫描并提示
  - 离线规则和关闭模式不检查，正常运行
  - 代码位置：`MainViewModel.cs:475-493`
- 📝 **新增状态消息**：
  - `StatusNoLocalModels`: 未获取到可用的本地模型的提示
  - `StatusNoCloudModels`: 未获取到可用的云端模型的提示
  - 代码位置：`Strings.resx:131-136`
- 🔄 **启动流程优化**：
  ```
  加载配置 → 保存模型名 → 清空显示 → 异步获取模型 → 验证并恢复
  ```
- ✅ 用户体验改进：
  - 启动后模型名称框始终为空（无论配置中保存了什么）
  - 1-2秒后自动获取并填充下拉列表
  - 如果保存的模型在列表中，自动恢复显示；否则保持为空
  - 扫描前精准拦截，避免用户困惑

**v1.1.6 (2025-11-18)** - HttpClient超时修复与启动优化
- 🔧 **根本性修复**：解决LocalLLM请求真正的超时问题
  - 问题诊断：通过测试发现LocalLLM调用本身正常（gemma3:27b响应约41秒）
  - 根本原因1：静态HttpClient默认100秒超时无法通过CancellationToken修改
  - 根本原因2：`ReadAsStringAsync()`使用了错误的CancellationToken
  - 解决方案：
    - 将HttpClient.Timeout设置为`Timeout.InfiniteTimeSpan`（完全依赖CancellationTokenSource控制）
    - 修复`ReadAsStringAsync(linkedCts.Token)`使用正确的超时Token
  - 代码位置：`LocalLlmInsightProvider.cs:22-25, 72`
- 🚀 **启动自动获取模型列表**：
  - 程序启动时根据AI模式自动获取相应的模型列表
  - LocalLlm模式自动调用`LoadLocalModelsAsync()`
  - KeyOnline模式自动调用`LoadCloudModelsAsync()`
  - 防止"显示模型名但下拉框为空"的用户体验问题
  - 代码位置：`MainViewModel.cs:86-106`
- 📝 技术细节：
  ```csharp
  // 修复1：HttpClient默认100秒超时（无法修改）
  private static readonly HttpClient HttpClient = new()
  {
      Timeout = System.Threading.Timeout.InfiniteTimeSpan  // 新增
  };

  // 修复2：响应读取使用正确的Token
  var payload = await response.Content.ReadAsStringAsync(linkedCts.Token);  // 修改前：cancellationToken
  ```
- ✅ 验证结果：gemma3:27b成功生成中文分析响应，响应时间约40秒
- 💡 说明：v1.1.5的配置方案正确但不充分，本版本彻底解决了两个层面的超时限制

**v1.1.5 (2025-11-18)** - 超时问题修复（已被v1.1.6替代）
- 🐛 修复LocalLLM请求超时导致显示"尚未生成说明"的问题
- ⏱️ 新增`LocalLlmRequestTimeoutSeconds`配置项，默认300秒（原默认100秒不够大模型使用）
- 📝 详细改进：
  - 在`AiConfiguration`中添加`LocalLlmRequestTimeoutSeconds`属性
  - `LocalLlmInsightProvider`使用`CancellationTokenSource`实现可配置超时
  - 配置文件中自动包含超时设置
  - 日志显示实际使用的超时时间
- 🔍 问题原因：gemma3:27b等大模型响应时间超过100秒，导致HttpClient超时，返回Empty结果
- ✅ 解决方案：增加超时配置，让用户可以根据模型大小调整等待时间
- 💡 建议：小模型(7B以下)可使用默认值，大模型(27B+)建议设置600秒或更长
- ⚠️ 限制：该版本未修复HttpClient静态超时问题，完整解决见v1.1.6

**v1.1.4 (2025-11-18)** - 日志增强与问题验证
- 📊 添加全面的调试日志系统，覆盖配置加载、AI模式选择、模型设置等关键流程
- ✅ 验证确认v1.1.3已成功修复模型名丢失问题
- 🔍 新增日志位置：`%AppData%\InsightCleanerAI\logs\debug.log`
- 📝 详细记录：
  - MainViewModel构造函数：配置加载前后的AI模式和模型名
  - ApplyConfig：配置应用的开始和完成状态
  - AI协调器：选择的Provider和配置参数
  - LocalLlmInsightProvider：请求/响应/错误详情
  - 设置保存：用户点击保存时的配置快照
- 💡 用途：无需人工点击UI即可通过日志完成自动化测试和问题诊断

**v1.1.3 (2025)** - 紧急Bug修复
- ⚠️ 修复v1.1.2引入的严重bug：清空模型名导致AI功能完全失效
- 🐛 调整逻辑：只清空模型下拉列表，保留配置中的模型名
- ℹ️ 说明：下拉框会显示历史模型名是正常行为（需要保留以让AI工作）
- ✅ 日志验证：`AiConfiguration.LocalLlmModel=gemma3:27b` 在清空列表后正确保留

**v1.1.2 (2025)** - Bug修复版v2（已被v1.1.3替代）
- ⚠️ 引入了严重bug：清空模型名导致AI无法工作（已在v1.1.3修复）
- 🐛 修复AI模式选择后配置项未立即启用的问题（改用 Mode=TwoWay 绑定）

**v1.1.1 (2025)** - Bug修复版v1（已被v1.1.2替代）
- 🐛 初步尝试修复AI模式选择和模型下拉框问题

**v1.1.0 (2025)** - 功能增强版
- ✅ 管理员权限检测
- ✅ 节点详情删除按钮
- ✅ 云端/本地模型自动获取
- ✅ 可编辑下拉框（模型选择）
- ✅ 本地LLM响应解析增强
- ✅ 完善的错误提示和调试日志

**v1.0.0** - 初始版本
- 基础文件扫描和 AI 分析功能
