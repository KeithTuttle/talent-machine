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

/** Translucent background from a hex color (keeps text readable in both themes). */
export function tint(hex?: string | null, alpha = 0.18): string | undefined {
  if (!hex || !/^#[0-9a-fA-F]{6}$/.test(hex)) return undefined
  const r = parseInt(hex.slice(1, 3), 16)
  const g = parseInt(hex.slice(3, 5), 16)
  const b = parseInt(hex.slice(5, 7), 16)
  return `rgba(${r}, ${g}, ${b}, ${alpha})`
}
