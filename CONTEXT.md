# AGI — Actual Guy Inside

一个将人类包装为标准 LLM API 接口的服务。调用方以为在调用 AI agent，实际上背后是人类在操作。

## Language

**Human Agent**:
通过标准 LLM API 协议对外提供服务的人类操作员。
_Avoid_: AI, bot, model

**Operator**:
在 WebUI 中接收请求并回复的人类。系统中唯一的"智能体"。
_Avoid_: user（容易与调用方混淆）

**Caller**:
通过 API 发送请求的外部应用程序或系统。它以为自己在调用 LLM。
_Avoid_: client（太泛）, user

**Request**:
Caller 发来的一次完整 API 调用，包含 messages 数组和可选的 tools 定义。
_Avoid_: prompt, query

**Request Queue**:
待处理请求的有序列表。Operator 从队列中逐个（或挑选）处理。
_Avoid_: inbox, task list

**Tool Call**:
Operator 决定调用 Caller 提供的某个 tool，构造 function name + arguments 返回给 Caller 执行。
_Avoid_: function call（OpenAI 旧术语）

**Simulated Streaming**:
Operator 写完回复后，服务端按一定速率逐 token 推送给 Caller，模拟 LLM 的 streaming 输出。
_Avoid_: real-time typing

**Heartbeat**:
Operator 思考期间，服务端定期向 Caller 发送的空 SSE chunk，防止连接超时。
_Avoid_: keep-alive（太底层）

## Relationships

- 一个 **Caller** 发送一个 **Request**
- 一个 **Request** 进入 **Request Queue** 等待 **Operator** 处理
- **Operator** 可以回复文本，也可以发起 **Tool Call**
- **Tool Call** 由 **Caller** 执行，结果返回后 **Operator** 继续处理
- 最终回复通过 **Simulated Streaming** 推送给 **Caller**

## Example dialogue

> **Dev:** "当一个 **Caller** 发来 **Request**，**Operator** 不在线怎么办？"
> **Domain expert:** "**Request** 留在 **Request Queue** 里，**Heartbeat** 保持连接。如果超过配置的最大等待时间，返回超时错误。"

## Flagged ambiguities

- "user" 在本项目中有歧义 — 可能指 Operator（人类操作员）或 Caller（调用方）。已解决：分别使用 **Operator** 和 **Caller**。
