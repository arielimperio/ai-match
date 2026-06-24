export interface ChatMessage {
  sender: 'me' | 'them';
  text: string;
}

export interface Feedback {
  rating: number;
  comment: string;
  submitted: boolean;
}

export interface MeetingFeedback {
  collecting: boolean;
  rating: number;
  comment: string;
  submitted: boolean;
}

export interface Match {
  id: string;
  name: string;
  role: string;
  score: number;
  reason: string;
  initials: string;
  photo?: string;
  status: 'none' | 'requested' | 'mutual';
  chat: ChatMessage[];
  feedback: Feedback;
  meetingFeedback?: MeetingFeedback; // Added based on usage in main.js
}

export interface Answers {
  firstName: string;
  lastName: string;
  email: string;
  company: string;
  title: string;
  photo: string | null;
  // Dynamic answers: QuestionId -> Value
  dynamic: Record<string, any>;
  // Dynamic "Other" values: QuestionId -> Text
  dynamicOther: Record<string, string>;
  hasAcceptedTerms: boolean;

  // Legacy fields (kept for compatibility in UI if needed)
  superpower?: string | null;
  challenge?: string | null;
  topics?: string[];
  bio?: string;
}

export type Step = 'welcome' | 'success' | 'results' | 'admin' | string;
export type ViewMode = 'user' | 'admin';

export interface QuestionOption {
  id: string;
  icon: string;
  title: string;
  desc: string;
  order?: number;
}

export interface QuestionContent {
  id: string; // added id
  title: string;
  description: string;
  type: string; // Choice, MultipleChoice, Text, Profile
  options?: QuestionOption[];
  placeholder?: string;
  button?: string;
  maxLength?: number;
  order?: number;
  targetRole?: string; // All, Student, Company
}
