import { useEffect, useRef, useState, useCallback } from "react";
import * as signalR from "@microsoft/signalr";
import RequestList from "./components/RequestList";
import ChatView from "./components/ChatView";
import type { PendingRequest } from "./types";
import "./index.css";

function App() {
  const [requests, setRequests] = useState<PendingRequest[]>([]);
  const [activeId, setActiveId] = useState<string | null>(null);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/operator")
      .withAutomaticReconnect()
      .build();

    connection.on("NewRequest", (request: PendingRequest) => {
      setRequests((prev) => [...prev, request]);
    });

    connection.start().catch(console.error);
    connectionRef.current = connection;

    return () => {
      connection.stop();
    };
  }, []);

  const handleReply = useCallback(
    async (requestId: string, content: string) => {
      await connectionRef.current?.invoke("Reply", requestId, content);
      setRequests((prev) => prev.filter((r) => r.id !== requestId));
      setActiveId(null);
    },
    []
  );

  const handleReplyWithToolCalls = useCallback(
    async (requestId: string, toolCallsJson: string) => {
      await connectionRef.current?.invoke(
        "ReplyWithToolCalls",
        requestId,
        toolCallsJson
      );
      setRequests((prev) => prev.filter((r) => r.id !== requestId));
      setActiveId(null);
    },
    []
  );

  const activeRequest = requests.find((r) => r.id === activeId) ?? null;

  return (
    <div className="app">
      <aside className="sidebar">
        <h2>Requests</h2>
        <RequestList
          requests={requests}
          activeId={activeId}
          onSelect={setActiveId}
        />
      </aside>
      <main className="main">
        {activeRequest ? (
          <ChatView
            request={activeRequest}
            onReply={handleReply}
            onReplyWithToolCalls={handleReplyWithToolCalls}
          />
        ) : (
          <div className="empty-state">
            <p>Waiting for requests...</p>
          </div>
        )}
      </main>
    </div>
  );
}

export default App;
