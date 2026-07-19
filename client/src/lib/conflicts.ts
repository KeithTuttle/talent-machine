import type { Conflict, Weekday } from '@/types'

export const WEEKDAYS: Weekday[] = [
  'Sunday',
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
]

/** Parse yyyy-MM-dd as a LOCAL date (new Date('2026-06-12') would be UTC and
 * shift a day in western timezones). Returns null on junk. */
export function parseDate(value?: string | null): Date | null {
  if (!value) return null
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(value.trim())
  if (!m) return null
  const d = new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]))
  return Number.isNaN(d.getTime()) ? null : d
}

/** Format a Date back to yyyy-MM-dd (local). */
export function toIsoDate(d: Date): string {
  const mm = String(d.getMonth() + 1).padStart(2, '0')
  const dd = String(d.getDate()).padStart(2, '0')
  return `${d.getFullYear()}-${mm}-${dd}`
}

function stripTime(d: Date): number {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime()
}

/** Whether the conflict makes the performer unavailable on `date`. */
export function occursOn(conflict: Conflict, date: Date): boolean {
  const day = stripTime(date)
  const start = parseDate(conflict.startDate)
  if (!start) return false
  const end = parseDate(conflict.endDate)

  if (conflict.type === 'Weekly') {
    if (!conflict.weekday) return false
    if (WEEKDAYS[date.getDay()] !== conflict.weekday) return false
    if (day < stripTime(start)) return false
    return end === null || day <= stripTime(end)
  }

  // OneOff: start..(end ?? start)
  const rangeEnd = end ?? start
  return day >= stripTime(start) && day <= stripTime(rangeEnd)
}

const shortDate = (value: string) => {
  const d = parseDate(value)
  return d ? d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' }) : value
}

/** Human label: "Jun 12", "Jun 12 – Jun 14", "every Tue", "every Tue until Jul 30". */
export function conflictLabel(c: Conflict): string {
  if (c.type === 'Weekly') {
    const day = c.weekday?.slice(0, 3) ?? '?'
    return c.endDate ? `every ${day} until ${shortDate(c.endDate)}` : `every ${day}`
  }
  if (c.endDate && c.endDate !== c.startDate)
    return `${shortDate(c.startDate)} – ${shortDate(c.endDate)}`
  return shortDate(c.startDate)
}
