const MS_PER_DAY = 86_400_000;

/**
 * The building is in France: a day starts at midnight in Paris, not in UTC. A photo taken at 00:30 on the 27th in Paris
 * (23:30 UTC on the 26th) was taken on the 27th.
 */
export const BUILDING_TIME_ZONE = 'Europe/Paris';

const calendarDay = new Intl.DateTimeFormat('en-CA', {
  timeZone: BUILDING_TIME_ZONE,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
});

/** The calendar day of an instant in the building's time zone, counted in days since 1970-01-01. */
function dayNumber(instant: Date): number {
  const parts = calendarDay.formatToParts(instant);
  const part = (type: Intl.DateTimeFormatPartTypes): number => Number(parts.find((p) => p.type === type)?.value);
  return Date.UTC(part('year'), part('month') - 1, part('day')) / MS_PER_DAY;
}

/**
 * Whole calendar days, in the building's time zone, between the first observation of a constat and today
 * (spec §3: "ouvert depuis N jours"). The back end publishes the instant of the first observation; the age is computed in
 * the visitor's browser against its own clock, so it keeps counting while the back end is down.
 */
export function daysOpen(firstObserved: Date, today: Date): number {
  const days = dayNumber(today) - dayNumber(firstObserved);
  if (days < 0) {
    throw new RangeError('The first observation cannot be after today.');
  }
  return days;
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
