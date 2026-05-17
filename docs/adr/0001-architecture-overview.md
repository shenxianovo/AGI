# 人类作为 LLM API 后端的整体架构

我们选择构建一个兼容 OpenAI 和 Anthropic 两种 API 格式的 HTTP 服务，背后由人类操作员通过 WebUI 响应请求。技术栈为 .NET 后端 + React 前端 + SignalR 实时通信。

这个决策的核心权衡是：兼容两种协议增加了适配层复杂度，但换来了"任何已对接 LLM 的系统都能零改动切换到人类"的通用性。选择 .NET 是因为与现有 AuthService 同生态，JWT 验证开箱即用。

## Considered Options

- **只兼容 OpenAI 格式** — 更简单，但排除了 Anthropic SDK 用户。
- **自定义协议** — 最灵活，但调用方需要专门适配，违背"无缝替换"的核心目标。
- **TypeScript 全栈** — 开发速度快，但与现有 AuthService（.NET）生态不一致。

## Consequences

- 需要维护两套协议的请求/响应适配器（OpenAI ↔ 内部模型 ↔ Anthropic）。
- 调用方只需修改 base_url 即可切换，无需改代码逻辑。
- 认证依赖外部 AuthService，本服务不管 key 签发，只验证 JWT。
