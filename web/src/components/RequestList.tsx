import type { PendingRequest } from "../types";

interface Props {
  requests: PendingRequest[];
  activeId: string | null;
  onSelect: (id: string) => void;
}

export default function RequestList({ requests, activeId, onSelect }: Props) {
  if (requests.length === 0) {
    return <p className="no-requests">No pending requests</p>;
  }

  return (
    <ul className="request-list">
      {requests.map((req) => {
        const lastMessage = req.messages[req.messages.length - 1];
        const preview = lastMessage?.content?.slice(0, 50) ?? "";
        return (
          <li
            key={req.id}
            className={`request-item ${req.id === activeId ? "active" : ""}`}
            onClick={() => onSelect(req.id)}
          >
            <span className="request-model">{req.model}</span>
            <span className="request-preview">{preview}</span>
          </li>
        );
      })}
    </ul>
  );
}
