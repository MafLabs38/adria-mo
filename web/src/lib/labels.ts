/** What the interface says, in French, for the keys the back end sends. */

export interface StatusLabel {
  label: string;
  /** Tailwind classes of the badge. */
  tone: string;
}

const STATUSES: Record<string, StatusLabel> = {
  open: { label: 'Ouvert', tone: 'bg-accent-400 text-slate-900' },
  'reported-to-landlord': { label: 'Signalé au bailleur', tone: 'bg-amber-200 text-amber-900' },
  'intervention-planned': { label: 'Intervention prévue', tone: 'bg-sky-200 text-sky-900' },
  'in-progress': { label: 'En cours', tone: 'bg-sky-300 text-sky-950' },
  resolved: { label: 'Résolu (vérifié)', tone: 'bg-brand-200 text-brand-900' },
  closed: { label: 'Clos', tone: 'bg-slate-200 text-slate-700' },
  reopened: { label: 'Rouvert', tone: 'bg-red-200 text-red-900' },
};

export function statusLabel(status: string): StatusLabel {
  return STATUSES[status] ?? { label: status, tone: 'bg-slate-200 text-slate-700' };
}

const MESSAGES: Record<string, string> = {
  TooShort: 'Au moins 12 caractères.',
  TooLong: 'Au plus 128 caractères.',
  OneRepeatedCharacter: 'Pas un seul caractère répété.',
  ContainsUserName: "Le mot de passe ne doit pas contenir l'identifiant.",
  TooCommon: 'Ce mot de passe est trop courant : choisissez une phrase à vous.',
  PasswordMismatch: "Le mot de passe actuel n'est pas le bon.",
  SameAsCurrent: "Le nouveau mot de passe doit être différent de l'actuel.",
  InvalidUserName: 'Identifiant : lettres sans accent, chiffres, point, tiret ou soulignement, de 3 à 64 caractères.',
  DuplicateUserName: 'Cet identifiant existe déjà.',
  Required: 'Obligatoire.',
  Unknown: 'Valeur inconnue.',
  Empty: 'Ajoutez une photo ou un commentaire.',
  NotAnImage: "Ce fichier n'est pas une photo lisible.",
  TooLarge: 'Photo trop lourde (25 Mo au plus).',
  TooManyPixels: 'Photo trop grande (40 millions de pixels au plus).',
  EmptyFile: 'Ce fichier est vide.',
  AlreadyAdded: 'Cette photo est déjà dans le constat.',
  TooMany: 'Trop de photos à la fois (200 au plus).',
  ServerError: "Le serveur n'a pas pu enregistrer cette photo : réessayez plus tard.",
};

/** The French message of an error code of the API; the code itself when it is unknown, so nothing is hidden. */
export function message(code: string): string {
  return MESSAGES[code] ?? code;
}

/** "27 novembre 2026 à 14:05", in the building's time zone. */
export function formatDateTime(instant: string, timeZone: string): string {
  return new Intl.DateTimeFormat('fr-FR', { dateStyle: 'long', timeStyle: 'short', timeZone }).format(
    new Date(instant),
  );
}

/** "27 novembre 2026", in the building's time zone. */
export function formatDate(instant: string, timeZone: string): string {
  return new Intl.DateTimeFormat('fr-FR', { dateStyle: 'long', timeZone }).format(new Date(instant));
}

/**
 * The title of a series of photos: "23 novembre 2026, de 09:15 à 09:20", "23 novembre 2026 à 09:15" for one instant, both dates
 * when it spans midnight; in the building's time zone.
 */
export function seriesTitle(startedAt: string, endedAt: string, timeZone: string): string {
  const day = new Intl.DateTimeFormat('fr-FR', { dateStyle: 'long', timeZone });
  const time = new Intl.DateTimeFormat('fr-FR', { timeStyle: 'short', timeZone });
  const [start, end] = [new Date(startedAt), new Date(endedAt)];
  if (day.format(start) !== day.format(end)) {
    return `Du ${day.format(start)} à ${time.format(start)} au ${day.format(end)} à ${time.format(end)}`;
  }
  if (time.format(start) === time.format(end)) {
    return `${day.format(start)} à ${time.format(start)}`;
  }
  return `${day.format(start)}, de ${time.format(start)} à ${time.format(end)}`;
}
