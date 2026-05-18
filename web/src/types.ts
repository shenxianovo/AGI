export interface Message {
  role: string;
  content: string;
}

export interface PendingRequest {
  id: string;
  model: string;
  messages: Message[];
}
