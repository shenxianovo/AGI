export interface ContentBlock {
  type: string;
  text?: string;
}

export interface Message {
  role: string;
  content: string | ContentBlock[];
}

export interface PendingRequest {
  id: string;
  model: string;
  messages: Message[];
}
