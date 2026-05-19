import { useEffect, useState } from "react";

interface StatusData {
  processed_messages: number;
  input_tokens: number;
  output_tokens: number;
  pending_messages: number;
  operator_online: boolean;
  public_endpoint: string;
  example_api_key: string;
}

export default function StatusDashboard() {
  const [status, setStatus] = useState<StatusData | null>(null);

  useEffect(() => {
    const fetchStatus = async () => {
      try {
        const res = await fetch("/api/status");
        const data = await res.json();
        setStatus(data);
      } catch (err) {
        console.error("Failed to fetch status:", err);
      }
    };

    fetchStatus();
    const interval = setInterval(fetchStatus, 3000);
    return () => clearInterval(interval);
  }, []);

  if (!status) {
    return <div className="status-loading">Loading...</div>;
  }

  return (
    <div className="status-dashboard">
      <header className="status-header">
        <h1>AGI — Actual Guy Inside</h1>
        <p className="status-subtitle">Human-powered AI API</p>
      </header>

      <div className="status-grid">
        <div className="status-card">
          <div className="status-label">Operator Status</div>
          <div className={`status-value ${status.operator_online ? "online" : "offline"}`}>
            {status.operator_online ? "🟢 Online" : "🔴 Offline"}
          </div>
        </div>

        <div className="status-card">
          <div className="status-label">Pending Messages</div>
          <div className="status-value">{status.pending_messages}</div>
        </div>

        <div className="status-card">
          <div className="status-label">Processed Messages</div>
          <div className="status-value">{status.processed_messages}</div>
        </div>

        <div className="status-card">
          <div className="status-label">Input Tokens</div>
          <div className="status-value">{status.input_tokens.toLocaleString()}</div>
        </div>

        <div className="status-card">
          <div className="status-label">Output Tokens</div>
          <div className="status-value">{status.output_tokens.toLocaleString()}</div>
        </div>
      </div>

      <div className="status-info">
        <h2>API Endpoint</h2>
        <code className="status-code">{status.public_endpoint}</code>

        <h2>Example API Key</h2>
        <code className="status-code">{status.example_api_key}</code>

        <div className="status-usage">
          <h3>Usage Example (OpenAI format)</h3>
          <pre className="status-pre">{`curl ${status.public_endpoint}/v1/chat/completions \\
  -H "Authorization: Bearer ${status.example_api_key}" \\
  -H "Content-Type: application/json" \\
  -d '{
    "model": "quq-1.0",
    "messages": [{"role": "user", "content": "Hello"}]
  }'`}</pre>

          <h3>Usage Example (Anthropic format)</h3>
          <pre className="status-pre">{`curl ${status.public_endpoint}/v1/messages \\
  -H "x-api-key: ${status.example_api_key}" \\
  -H "Content-Type: application/json" \\
  -d '{
    "model": "quq-1.0",
    "max_tokens": 1024,
    "messages": [{"role": "user", "content": "Hello"}]
  }'`}</pre>
        </div>
      </div>
    </div>
  );
}
