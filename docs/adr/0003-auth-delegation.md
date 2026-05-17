# 认证委托给外部 AuthService

本服务不实现 API Key 管理和签发，只验证由外部 AuthService（D:\Code\Personal\AuthService）签发的 JWT。调用方先从 AuthService 用 API Key 换取短期 JWT，再用 JWT 访问本服务。

这样做的原因是避免重复造轮子——AuthService 已经有完整的 key 管理、吊销、JWKS 端点。本服务只需从 AuthService 的 `/.well-known/jwks.json` 拉取公钥做离线验证。代价是本服务依赖 AuthService 可用（至少 JWKS 端点可达），但公钥可以缓存，实际上是弱依赖。
