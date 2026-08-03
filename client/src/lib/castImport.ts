// Pure logic for the cast importer (CSV/Excel roster → performers in a show).
// Reuses the conflict importer's file parsing + date parsing.
import type { Gender, Performer } from '@/types'
import { toIsoDate } from '@/lib/conflicts'
import { parseDateCell } from '@/lib/conflictImport'
export { sheetToRows } from '@/lib/conflictImport'

/** Which file column feeds each cast field (index into the header row; null = unset). */
export interface CastColumnMap {
  firstName: number | null
  lastName: number | null
  fullName: number | null
  gender: number | null
  dob: number | null
  age: number | null
  guardianName: number | null
  guardianEmail: number | null
  guardianPhone: number | null
  castGroup: number | null
  notes: number | null
}

/** One editable row in the cast review table. */
export interface CastReviewRow {
  firstName: string
  lastName: string
  gender: Gender | null
  dateOfBirth: string | null
  age: number | null
  notes: string | null
  castGroup: string | null
  guardianName: string | null
  guardianEmail: string | null
  guardianPhone: string | null
  /** Matched roster performer id, or null = create a new performer. */
  existingPerformerId: number | null
  include: boolean
}

const norm = (s: string) => (s ?? '').toString().trim().toLowerCase().replace(/\s+/g, ' ')

/** Best-guess mapping of header labels to cast fields. */
export function guessCastMapping(headers: string[]): CastColumnMap {
  const find = (re: RegExp, skip: number[] = []) => {
    const i = headers.findIndex((h, idx) => !skip.includes(idx) && re.test(h.toLowerCase()))
    return i === -1 ? null : i
  }
  const firstName = find(/first ?name|^first$|given/)
  const lastName = find(/last ?name|^last$|surname|family/)
  // Only look for a single full-name column when we didn't find split name columns.
  const fullName = firstName == null && lastName == null
    ? find(/^name$|full ?name|performer|student|child|actor|^kid$/)
    : null
  const guardianEmail = find(/e-?mail/)
  const guardianPhone = find(/phone|cell|mobile|\btel\b/)
  const guardianName = find(/guardian|parent|mother|father|\bmom\b|\bdad\b|contact/, [guardianEmail ?? -1, guardianPhone ?? -1])
  return {
    firstName,
    lastName,
    fullName,
    gender: find(/gender|\bsex\b|m\/f/),
    dob: find(/birth|d\.?o\.?b|birthday/),
    age: find(/^age$|\bage\b/),
    guardianName,
    guardianEmail,
    guardianPhone,
    castGroup: find(/group|\bcast\b|track|team|ensemble|color/),
    notes: find(/note|comment|remark|allerg/),
  }
}

/** Split "Ava Brown" / "Brown, Ava" into [first, last]. */
export function splitName(full: string): [string, string] {
  const t = (full ?? '').trim()
  if (!t) return ['', '']
  if (t.includes(',')) {
    const [last, first] = t.split(',', 2).map((s) => s.trim())
    return [first ?? '', last ?? '']
  }
  const parts = t.split(/\s+/)
  if (parts.length === 1) return [parts[0], '']
  return [parts.slice(0, -1).join(' '), parts[parts.length - 1]]
}

/** Map a free-text gender cell to the enum, else null. */
export function parseGender(cell: string): Gender | null {
  const t = norm(cell)
  if (!t) return null
  if (/^(m|male|boy|man|b)$/.test(t)) return 'Male'
  if (/^(f|female|girl|woman|w|g)$/.test(t)) return 'Female'
  if (/^(nb|non-?binary|enby|x|other)$/.test(t)) return 'NonBinary'
  return null
}

/** Approximate a DOB from an age so the app's computed age (as of `refIso`) matches. */
export function ageToDob(age: number, refIso: string | null | undefined): string | null {
  if (!Number.isFinite(age) || age <= 0 || age > 120) return null
  const ref = refIso && /^\d{4}-\d{2}-\d{2}$/.test(refIso)
    ? new Date(Number(refIso.slice(0, 4)), Number(refIso.slice(5, 7)) - 1, Number(refIso.slice(8, 10)))
    : new Date()
  return toIsoDate(new Date(ref.getFullYear() - age, ref.getMonth(), ref.getDate()))
}

/**
 * Resolve a name to a roster performer id, most→least strict: exact "First Last" /
 * "Last, First"; then a unique last name; a unique first name; first-initial + last.
 */
export function matchPerformer(name: string, roster: Performer[]): number | null {
  const n = norm(name)
  if (!n) return null
  const people = roster.map((p) => ({ id: p.id, first: norm(p.firstName), last: norm(p.lastName) }))
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

/** Build a review row from one data row using the mapping. */
export function buildRow(cells: string[], map: CastColumnMap, roster: Performer[], refIso: string | null): CastReviewRow {
  const get = (idx: number | null) => (idx != null && idx >= 0 ? cells[idx] ?? '' : '')
  let first = get(map.firstName).trim()
  let last = get(map.lastName).trim()
  if (!first && !last && map.fullName != null) [first, last] = splitName(get(map.fullName))

  const ageRaw = get(map.age).replace(/[^\d]/g, '')
  const age = ageRaw ? Number(ageRaw) : null
  let dob = parseDateCell(get(map.dob))
  if (!dob && age) dob = ageToDob(age, refIso)

  return {
    firstName: first,
    lastName: last,
    gender: parseGender(get(map.gender)),
    dateOfBirth: dob,
    age,
    notes: get(map.notes).trim() || null,
    castGroup: get(map.castGroup).trim() || null,
    guardianName: get(map.guardianName).trim() || null,
    guardianEmail: get(map.guardianEmail).trim() || null,
    guardianPhone: get(map.guardianPhone).trim() || null,
    existingPerformerId: matchPerformer(`${first} ${last}`, roster),
    include: true,
  }
}

export type CastRowStatus = 'new' | 'existing' | 'already-in-cast' | 'needs-name'

export function rowStatus(row: CastReviewRow, castPerformerIds: Set<number>): CastRowStatus {
  if (!row.firstName.trim() && !row.lastName.trim()) return 'needs-name'
  if (row.existingPerformerId != null && castPerformerIds.has(row.existingPerformerId)) return 'already-in-cast'
  return row.existingPerformerId != null ? 'existing' : 'new'
}
