import { useState } from "react";
import MDEditor from "@uiw/react-md-editor";
import type { PendingRequest } from "../types";

interface Props {
  request: PendingRequest;
  onReply: (requestId: string, content: string) => void;
  onReplyWithToolCalls: (requestId: string, toolCallsJson: string) => void;
}

export default function ChatView({ request, onReply, onReplyWithToolCalls }: Props) {
  const [reply, setReply] = useState("");
  const [mode, setMode] = useState<"text" | "tool">("text");
  const [toolJson, setToolJson] = useState(
    JSON.stringify(
      {
        type: "tool_calls",
        tool_calls: [
          {
            id: "call_1",
            type: "function",
            function: { name: "", arguments: "{}" },
          },
        ],
      },
      null,
      2
    )
  );

  const handleSend = () => {
    if (mode === "text") {
      if (!reply.trim()) return;
      onReply(request.id, reply);
    } else {
      onReplyWithToolCalls(request.id, toolJson);
    }
  };

  return (
    <div className="chat-view">
      <div className="messages">
        {request.messages.map((msg, i) => (
          <div key={i} className={`message message-${msg.role}`}>
            <span className="message-role">{msg.role}</span>
            <div className="message-content">{msg.content}</div>
          </div>
        ))}
      </div>

      <div className="reply-area">
        <div className="reply-tabs">
          <button
            className={mode === "text" ? "active" : ""}
            onClick={() => setMode("text")}
          >
            Text Reply
          </button>
          <button
            className={mode === "tool" ? "active" : ""}
            onClick={() => setMode("tool")}
          >
            Tool Call
          </button>
        </div>

        {mode === "text" ? (
          <MDEditor value={reply} onChange={(v) => setReply(v ?? "")} height={200} />
        ) : (
          <textarea
            className="tool-editor"
            value={toolJson}
            onChange={(e) => setToolJson(e.target.value)}
            rows={10}
          />
        )}

        <button className="send-btn" onClick={handleSend}>
          Send
        </button>
      </div>
    </div>
  );
}
