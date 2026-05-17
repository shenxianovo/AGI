# 同步 SSE 心跳 + 异步轮询双模式

人类响应速度远慢于 LLM（秒级 vs 分钟级），标准 SDK 的默认超时会断开连接。我们选择同时支持两种模式：同步请求通过 SSE streaming 发送 heartbeat 保活；同时提供异步 endpoint（返回 202 + task ID，调用方轮询结果）。

同步模式让标准 OpenAI/Anthropic SDK 开箱能用（只需调大超时），异步模式给需要长时间等待的场景兜底。Operator 写完回复后，通过 simulated streaming 逐 token 推送，调用方体验与真实 LLM 一致。

## Considered Options

- **纯同步** — SDK 兼容性最好，但长时间无响应可能被中间件/代理层断开。
- **纯异步** — 最健壮，但调用方必须改代码适配轮询逻辑，违背"零改动替换"目标。
