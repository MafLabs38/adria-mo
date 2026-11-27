export interface Me {
  userName: string;
  roles: string[];
  mustChangePassword: boolean;
  /** The building's time zone, in which dates are shown. */
  timeZone: string;
}

export interface Category {
  key: string;
  label: string;
}

export interface ConstatSummary {
  number: number;
  title: string;
  category: string;
  location: string | null;
  status: string;
  isPublic: boolean;
  firstObservedAt: string;
  updatedAt: string;
  pieceCount: number;
  thumbnail: string | null;
}

export interface PieceDetail {
  id: number;
  text: string | null;
  takenAt: string | null;
  addedAt: string;
  observedAt: string;
  photo: string | null;
  thumbnail: string | null;
  originalSha256: string | null;
}

export interface ConstatDetail extends Omit<ConstatSummary, 'pieceCount' | 'thumbnail'> {
  pieces: PieceDetail[];
}

export interface UserSummary {
  id: string;
  userName: string;
  roles: string[];
  mustChangePassword: boolean;
  lockedOutUntil: string | null;
}

export interface PublicationState {
  lastAttemptAt: string | null;
  lastSuccessAt: string | null;
  lastError: string | null;
}

export const SUPER_ADMIN = 'super-admin';

export interface DeviceSummary {
  id: string;
  name: string;
  createdAt: string;
  pairedAt: string | null;
  isCurrent: boolean;
}

export interface PairingCode {
  id: string;
  code: string;
  expiresAt: string;
}

export interface InboxItem {
  id: number;
  photo: string;
  thumbnail: string;
  takenAt: string | null;
  addedAt: string;
  author: string;
}

export interface InboxSeries {
  startedAt: string;
  endedAt: string;
  photos: InboxItem[];
}

export interface InboxListing {
  series: InboxSeries[];
  undated: InboxItem[];
}

export interface AttachResult {
  constat: number;
  attached: number;
  alreadyThere: number;
  missing: number;
}
