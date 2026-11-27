const MS_PER_DAY = 86_400_000;

/**
 * The building is in France: a day starts at midnight in Paris, not in UTC. A photo taken at 00:30 on the 27th in Paris
 * (23:30 UTC on the 26th) was taken on the 27th. The published snapshot says the building's time zone; this one is the default.
 */
export const BUILDING_TIME_ZONE = 'Europe/Paris';

const calendars = new Map<string, Intl.DateTimeFormat>();

function calendarOf(timeZone: string): Intl.DateTimeFormat {
  let calendar = calendars.get(timeZone);
  if (calendar === undefined) {
    calendar = new Intl.DateTimeFormat('en-CA', { timeZone, year: 'numeric', month: '2-digit', day: '2-digit' });
    calendars.set(timeZone, calendar);
  }
  return calendar;
}

/** The calendar day of an instant in a time zone, counted in days since 1970-01-01. */
function dayNumber(instant: Date, timeZone: string): number {
  if (Number.isNaN(instant.getTime())) {
    throw new RangeError('The date is not a valid date.');
  }
  const parts = calendarOf(timeZone).formatToParts(instant);
  const part = (type: Intl.DateTimeFormatPartTypes): number => {
    const value = parts.find((p) => p.type === type)?.value;
    if (value === undefined) {
      throw new RangeError(`The calendar gave no ${type}.`);
    }
    return Number(value);
  };
  return Date.UTC(part('year'), part('month') - 1, part('day')) / MS_PER_DAY;
}

/**
 * Whole calendar days, in the building's time zone, between the first observation of a constat and today
 * (spec §3: "ouvert depuis N jours"). The back end publishes the instant of the first observation; the age is computed in
 * the visitor's browser against its own clock, so it keeps counting while the back end is down. A visitor's clock that is
 * behind the first observation counts zero: one wrong clock must not break the list.
 */
export function daysOpen(firstObserved: Date, today: Date, timeZone: string = BUILDING_TIME_ZONE): number {
  return Math.max(0, dayNumber(today, timeZone) - dayNumber(firstObserved, timeZone));
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
