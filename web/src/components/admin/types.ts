export interface Me {
  userName: string;
  roles: string[];
  mustChangePassword: boolean;
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

export const SUPER_ADMIN = 'super-admin';
