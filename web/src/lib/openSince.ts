const MS_PER_DAY = 86_400_000;

/**
 * Whole calendar days between the first observation of a constat and today (spec §3: "ouvert depuis N jours").
 * Both dates are read as calendar days in UTC, so the hour of the photo does not change the count.
 */
export function daysOpen(firstObserved: Date, today: Date): number {
  const start = Date.UTC(firstObserved.getUTCFullYear(), firstObserved.getUTCMonth(), firstObserved.getUTCDate());
  const end = Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), today.getUTCDate());
  if (end < start) {
    throw new RangeError('The first observation cannot be after today.');
  }
  return Math.round((end - start) / MS_PER_DAY);
}

/** The label shown on a constat card, in French: "ouvert aujourd'hui", "ouvert depuis 1 jour", "ouvert depuis 94 jours". */
export function openSinceLabel(days: number): string {
  if (!Number.isInteger(days) || days < 0) {
    throw new RangeError('The number of days must be a whole number, zero or more.');
  }
  if (days === 0) {
    return "ouvert aujourd'hui";
  }
  return days === 1 ? 'ouvert depuis 1 jour' : `ouvert depuis ${days} jours`;
}
