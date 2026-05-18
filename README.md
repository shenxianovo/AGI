# AGI — Actual Guy Inside

用调用 LLM 的方式调用人类。一个兼容 OpenAI + Anthropic API 格式的 HTTP 服务，背后由人类操作员通过 WebUI 响应。

## 工作原理

```
Caller (以为在调用 AI)
    ↓ OpenAI/Anthropic API
AGI Server (SSE 心跳保活)
    ↓ SignalR
Operator WebUI (人类回复)
```

## 功能

- 兼容 OpenAI `/v1/chat/completions` 和 Anthropic `/v1/messages` 格式
- SSE streaming 输出（Operator 写完后模拟逐 token 推送）
- Tool calls 支持（Operator 决策，Caller 执行）
- 异步轮询模式（提交任务 + 查询状态）
- 心跳保活防止连接超时
- JWT 认证（验证外部 AuthService 签发的 token）

## 技术栈

- 后端：ASP.NET Core (.NET 10)
- 前端：React + Vite + TypeScript
- 实时通信：SignalR

## 本地开发

```bash
# 后端
cd src/AGI.Api
dotnet run

# 前端（开发模式，自动代理到后端）
cd web
npm install
npm run dev
```

前端开发服务器运行在 `http://localhost:5173`，API 请求自动代理到 `http://localhost:5096`。

## 部署

GitHub Actions 自动部署：push to main 且 `src/**` 或 `web/**` 有变更时触发。

### 服务器初始化

```bash
# 创建目录
sudo mkdir -p /srv/agi/releases
sudo chown shenxianovo:shenxianovo /srv/agi

# 安装 systemd unit
sudo cp deploy/agi.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable agi

# Nginx 配置
sudo cp deploy/nginx/chat.conf /etc/nginx/sites-enabled/
sudo nginx -t && sudo nginx -s reload
```

### GitHub Secrets

| Secret | 说明 |
|--------|------|
| `SERVER_HOST` | 服务器 IP/域名 |
| `SERVER_USER` | SSH 用户名 |
| `SERVER_SSH_KEY` | SSH 私钥 |
