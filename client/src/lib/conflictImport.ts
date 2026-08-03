// Pure logic for the conflict importer (CSV/Excel). The dialog is the UI over this.
import type { CastMembership, Conflict, Weekday } from '@/types'
import { WEEKDAYS, toIsoDate } from '@/lib/conflicts'

/** Which file column feeds each conflict field (index into the header row; null = unset). */
export interface ColumnMap {
  name: number | null
  start: number | null
  end: number | null
  reason: number | null
  weekday: number | null
}

/** One editable row in the review table. */
export interface ReviewRow {
  name: string
  performerId: number | null
  startDate: string | null
  endDate: string | null
  weekdays: Weekday[]
  reason: string | null
  include: boolean
}

const norm = (s: string) => (s ?? '').toString().trim().toLowerCase().replace(/\s+/g, ' ')

/** Read the first sheet of a CSV/Excel file into a trimmed grid of strings. */
export async function sheetToRows(file: File): Promise<string[][]> {
  // CSV/text: parse ourselves so date-looking strings are kept verbatim. Letting
  // SheetJS coerce "2026-08-10" into a Date reformats it through the local
  // timezone and shifts it a day — a silent, one-off corruption of every date.
  if (/\.(csv|txt)$/i.test(file.name) || file.type === 'text/csv') {
    return parseDelimited(await file.text())
  }
  // Real Excel: load SheetJS on demand. raw:true keeps date cells as serial
  // numbers (parseDateCell handles them timezone-safely) instead of reformatting.
  const XLSX = await import('xlsx')
  const wb = XLSX.read(await file.arrayBuffer(), { cellDates: false })
  const ws = wb.Sheets[wb.SheetNames[0]]
  if (!ws) return []
  const rows = XLSX.utils.sheet_to_json<unknown[]>(ws, {
    header: 1,
    raw: true,
    blankrows: false,
    defval: '',
  })
  return rows.map((r) => r.map((c) => (c == null ? '' : String(c).trim())))
}

/** A quote-aware delimited parser (comma / tab / semicolon, auto-detected). */
function parseDelimited(text: string): string[][] {
  text = text.replace(/^﻿/, '') // strip BOM
  const firstLine = text.slice(0, text.search(/\r?\n/) === -1 ? text.length : text.search(/\r?\n/))
  const delim = [',', '\t', ';'].reduce((best, d) =>
    firstLine.split(d).length > firstLine.split(best).length ? d : best,
  )
  const rows: string[][] = []
  let row: string[] = []
  let field = ''
  let inQuotes = false
  for (let i = 0; i < text.length; i++) {
    const ch = text[i]
    if (inQuotes) {
      if (ch === '"') {
        if (text[i + 1] === '"') { field += '"'; i++ } else inQuotes = false
      } else field += ch
    } else if (ch === '"') inQuotes = true
    else if (ch === delim) { row.push(field); field = '' }
    else if (ch === '\n') { row.push(field); rows.push(row); row = []; field = '' }
    else if (ch !== '\r') field += ch
  }
  if (field.length > 0 || row.length > 0) { row.push(field); rows.push(row) }
  return rows.map((r) => r.map((c) => c.trim())).filter((r) => r.some((c) => c.length > 0))
}

/** A header row is one whose cells are mostly words (not dates/numbers). */
export function looksLikeHeader(row: string[]): boolean {
  const cells = row.filter((c) => c.trim().length > 0)
  if (cells.length === 0) return false
  const wordy = cells.filter((c) => /[a-z]/i.test(c) && !/^\d{1,4}[/.-]\d/.test(c)).length
  return wordy >= Math.ceil(cells.length / 2)
}

/** Best-guess mapping of header labels to conflict fields. */
export function guessMapping(headers: string[]): ColumnMap {
  const find = (re: RegExp, skip: number[] = []) => {
    const i = headers.findIndex((h, idx) => !skip.includes(idx) && re.test(h.toLowerCase()))
    return i === -1 ? null : i
  }
  const name = find(/name|performer|kid|student|child|actor/)
  const start = find(/start|from|begin|first|unavail|^date$|date/, name != null ? [name] : [])
  const end = find(/end|thru|through|until|\bto\b|last/, [name ?? -1, start ?? -1])
  const reason = find(/reason|note|why|detail|comment|desc/)
  const weekday = find(/weekday|day of|repeat|recur|every|weekly|recurring/)
  return { name, start, end, reason, weekday }
}

/**
 * Resolve a free-text name to a cast member id. Tiers, most→least strict:
 * exact "First Last" / "Last, First"; then a unique last name; a unique first
 * name; finally first-initial + unique last ("E. Johnson"). Ambiguous → null.
 */
export function matchPerformer(name: string, cast: CastMembership[]): number | null {
  const n = norm(name)
  if (!n) return null
  const people = cast
    .filter((m) => m.performer)
    .map((m) => ({ id: m.performerId, first: norm(m.performer!.firstName), last: norm(m.performer!.lastName) }))

  for (const p of people) {
    if (n === `${p.first} ${p.last}` || n === `${p.last} ${p.first}` || n === norm(`${p.last}, ${p.first}`)) return p.id
  }
  const tokens = n.replace(/[.,]/g, ' ').split(/\s+/).filter(Boolean)
  if (tokens.length === 0) return null
  const first = tokens[0]
  const last = tokens[tokens.length - 1]

  const byLast = people.filter((p) => p.last === last)
  if (byLast.length === 1) return byLast[0].id
  const byFirst = people.filter((p) => p.first === first)
  if (byFirst.length === 1) return byFirst[0].id
  if (first.length <= 2) {
    const cand = people.filter((p) => p.last === last && p.first.startsWith(first[0]))
    if (cand.length === 1) return cand[0].id
  }
  return null
}

/** Parse a cell to yyyy-MM-dd: ISO, US M/D/Y, Excel serial, or a parseable date string. */
export function parseDateCell(cell: string): string | null {
  const t = (cell ?? '').toString().trim()
  if (!t) return null
  if (/^\d{4}-\d{2}-\d{2}$/.test(t)) return t
  const us = /^(\d{1,2})[/.-](\d{1,2})[/.-](\d{2,4})$/.exec(t)
  if (us) {
    const year = us[3].length === 2 ? `20${us[3]}` : us[3]
    const d = new Date(Number(year), Number(us[1]) - 1, Number(us[2]))
    return Number.isNaN(d.getTime()) ? null : toIsoDate(d)
  }
  // Bare Excel serial (only in a plausible modern range, so "2026" the year isn't a date).
  if (/^\d{5}$/.test(t)) {
    const serial = Number(t)
    if (serial >= 20000 && serial <= 60000) {
      const ms = Date.UTC(1899, 11, 30) + serial * 86400000
      const d = new Date(ms)
      return toIsoDate(new Date(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate()))
    }
  }
  const parsed = new Date(t) // "June 12, 2026", "12 Jun 2026", Excel Date.toString(), etc.
  return Number.isNaN(parsed.getTime()) ? null : toIsoDate(parsed)
}

/** Parse "Tue, Thu" / "Mon/Wed" / "tuesday and thursday" into weekday names. */
export function parseWeekdays(cell: string): Weekday[] {
  const t = (cell ?? '').toString().toLowerCase()
  if (!t.trim()) return []
  const out: Weekday[] = []
  for (const part of t.split(/[,/&+]|\band\b/).map((s) => s.trim()).filter(Boolean)) {
    const w = WEEKDAYS.find((d) => d.toLowerCase().startsWith(part.slice(0, 3)))
    if (w && !out.includes(w)) out.push(w)
  }
  return out
}

/** A ready-to-insert conflict (no id/productionId yet). */
export type NewConflict = Omit<Conflict, 'id' | 'productionId'>

/** Expand a review row into one conflict per weekday (Weekly) or a single One-off. */
export function expandRow(row: ReviewRow): NewConflict[] {
  if (row.performerId == null) return []
  if (row.weekdays.length > 0) {
    // Weekly recurrences start from the given date, or today if none was provided.
    const start = row.startDate ?? toIsoDate(new Date())
    return row.weekdays.map((weekday) => ({
      performerId: row.performerId!,
      type: 'Weekly',
      startDate: start,
      endDate: row.endDate ?? null,
      weekday,
      reason: row.reason || null,
    }))
  }
  if (!row.startDate) return []
  return [
    {
      performerId: row.performerId,
      type: 'OneOff',
      startDate: row.startDate,
      endDate: row.endDate ?? null,
      weekday: null,
      reason: row.reason || null,
    },
  ]
}

/** True when an identical conflict already exists (skip on re-import). */
export function isDuplicate(c: NewConflict, existing: Conflict[]): boolean {
  return existing.some(
    (e) =>
      e.performerId === c.performerId &&
      e.type === c.type &&
      (e.startDate ?? null) === (c.startDate ?? null) &&
      (e.endDate ?? null) === (c.endDate ?? null) &&
      (e.weekday ?? null) === (c.weekday ?? null),
  )
}

export type RowStatus = 'ready' | 'needs-performer' | 'needs-date' | 'duplicate'

export function rowStatus(row: ReviewRow, existing: Conflict[]): RowStatus {
  if (row.performerId == null) return 'needs-performer'
  if (row.weekdays.length === 0 && !row.startDate) return 'needs-date'
  const expanded = expandRow(row)
  if (expanded.length > 0 && expanded.every((c) => isDuplicate(c, existing))) return 'duplicate'
  return 'ready'
}
