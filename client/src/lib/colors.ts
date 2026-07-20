/** Preset group colors — "gold group", "blue group"… — plus helpers for tinting. */
export const GROUP_COLORS = [
  { name: 'Gold', hex: '#EAB308' },
  { name: 'Blue', hex: '#3B82F6' },
  { name: 'Red', hex: '#EF4444' },
  { name: 'Green', hex: '#22C55E' },
  { name: 'Purple', hex: '#A855F7' },
  { name: 'Teal', hex: '#14B8A6' },
  { name: 'Orange', hex: '#F97316' },
  { name: 'Pink', hex: '#EC4899' },
  { name: 'Slate', hex: '#64748B' },
  { name: 'Brown', hex: '#926B4B' },
]

/** Stable palette for costume labels (dance-manager's COSTUME_PALETTE). */
export const COSTUME_PALETTE = [
  '#2563eb', '#db2777', '#16a34a', '#f59e0b', '#8b5cf6',
  '#0d9488', '#dc2626', '#0ea5e9', '#ca8a04', '#7c3aed',
]

/** Assigns each distinct (lowercased) costume label a stable palette color. */
export function costumeColorMap(labels: (string | null | undefined)[]): Map<string, string> {
  const map = new Map<string, string>()
  let i = 0
  for (const raw of labels) {
    const key = raw?.trim().toLowerCase()
    if (!key || map.has(key)) continue
    map.set(key, COSTUME_PALETTE[i % COSTUME_PALETTE.length])
    i++
  }
  return map
}

/** Dot colors by performer gender (dance-manager's overview convention). */
export const GENDER_COLORS: Record<string, string> = {
  Male: '#2563EB',
  Female: '#DB2777',
  NonBinary: '#A855F7',
}
export const GENDER_FALLBACK = '#94A3B8'

/** Translucent background from a hex color (keeps text readable in both themes). */
export function tint(hex?: string | null, alpha = 0.18): string | undefined {
  if (!hex || !/^#[0-9a-fA-F]{6}$/.test(hex)) return undefined
  const r = parseInt(hex.slice(1, 3), 16)
  const g = parseInt(hex.slice(3, 5), 16)
  const b = parseInt(hex.slice(5, 7), 16)
  return `rgba(${r}, ${g}, ${b}, ${alpha})`
}
