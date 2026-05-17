# AGI — Actual Guy Inside

用调用 LLM 的方式调用人类。一个兼容 OpenAI + Anthropic API 格式的 HTTP 服务，背后由人类操作员通过 WebUI 响应。

## 技术栈

- 后端：.NET (ASP.NET Core)
- 前端：React
- 实时通信：SignalR（后端 ↔ WebUI）
- 认证：验证外部 AuthService 签发的 JWT（JWKS 离线验证）
- 部署：本地/裸机运行

## 设计决策摘要

| 决策 | 结论 |
|------|------|
| API 协议 | 同时兼容 OpenAI + Anthropic 格式 |
| 请求生命周期 | 同步 SSE 心跳 + 异步轮询双模式 |
| Tool calls | 调用方执行，Operator 只决策 |
| 状态管理 | 协议无状态，UI 层智能分组展示 |
| 并发模型 | 单 Operator 队列 |
| Streaming | Operator 写完后模拟 streaming 推送 |
| Model 字段 | 默认 `"quq-1.0"`，可配置/透传 |
| Tool call UI | 表单 + JSON 编辑器混合模式 |
| 回复编辑器 | Markdown 编辑器 |

详见 `CONTEXT.md`（领域语言）和 `docs/adr/`（架构决策记录）。

## Agent skills

### Issue tracker

Issues are tracked as local markdown files under `.scratch/`. See `docs/agents/issue-tracker.md`.

### Triage labels

Default label vocabulary. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context layout with `CONTEXT.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.
