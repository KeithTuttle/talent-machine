import { occursOn, parseDate, toIsoDate } from '@/lib/conflicts'
import type { Conflict, NumberCast, Rehearsal, RehearsalAttendee, RehearsalType } from '@/types'

/** Resolved attendees: (number cast ∪ added) − excluded; number-less slots use
 * only explicitly added rows. Mirrors the server's RehearsalResolver. */
export function resolveAttendees(
  slot: Pick<Rehearsal, 'id' | 'musicalNumberId'>,
  numberCasts: NumberCast[],
  overrides: RehearsalAttendee[],
): Set<number> {
  const result = new Set<number>(
    slot.musicalNumberId != null
      ? numberCasts.filter((c) => c.musicalNumberId === slot.musicalNumberId).map((c) => c.performerId)
      : [],
  )
  for (const o of overrides) {
    if (o.rehearsalId !== slot.id) continue
    if (o.isExcluded) result.delete(o.performerId)
    else result.add(o.performerId)
  }
  return result
}

/** Attendee ids whose conflicts hit the given date (yyyy-MM-dd). */
export function conflictedAttendees(
  attendees: Iterable<number>,
  date: string,
  conflicts: Conflict[],
): number[] {
  const d = parseDate(date)
  if (!d) return []
  const out: number[] = []
  for (const pid of attendees) {
    if (conflicts.some((c) => c.performerId === pid && occursOn(c, d))) out.push(pid)
  }
  return out
}

/** "14:30[:00]" → "2:30 PM". Falls back to the raw string on junk. */
export function formatTime(time: string): string {
  const m = /^(\d{1,2}):(\d{2})/.exec(time)
  if (!m) return time
  const h = Number(m[1])
  const suffix = h >= 12 ? 'PM' : 'AM'
  const h12 = h % 12 === 0 ? 12 : h % 12
  return `${h12}:${m[2]} ${suffix}`
}

/** Monday of the week containing `d`. */
export function startOfWeek(d: Date): Date {
  const out = new Date(d.getFullYear(), d.getMonth(), d.getDate())
  out.setDate(out.getDate() - ((out.getDay() + 6) % 7))
  return out
}

export function addDays(d: Date, days: number): Date {
  const out = new Date(d)
  out.setDate(out.getDate() + days)
  return out
}

/** "Jun 8 – Jun 14, 2026" for the week starting at `monday`. */
export function weekLabel(monday: Date): string {
  const sunday = addDays(monday, 6)
  const fmt = (x: Date, year: boolean) =>
    x.toLocaleDateString(undefined, { month: 'short', day: 'numeric', ...(year ? { year: 'numeric' } : {}) })
  return `${fmt(monday, false)} – ${fmt(sunday, true)}`
}

export const weekRange = (monday: Date) => ({
  from: toIsoDate(monday),
  to: toIsoDate(addDays(monday, 6)),
})

/** Badge styling per rehearsal type (Tailwind-free inline tints for portability). */
export const REHEARSAL_TYPE_COLORS: Record<RehearsalType, string> = {
  Music: '#3B82F6',
  Dance: '#EC4899',
  Blocking: '#F97316',
  Runthrough: '#22C55E',
  Other: '#64748B',
}

export const REHEARSAL_TYPES: RehearsalType[] = ['Music', 'Dance', 'Blocking', 'Runthrough', 'Other']
