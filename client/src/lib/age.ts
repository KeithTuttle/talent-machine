/**
 * Age in whole years at `asOf` (a production's show date), falling back to
 * today. Returns null when the date of birth is unknown/unparseable, so callers
 * can simply hide the age.
 */
export function ageOn(dateOfBirth?: string | null, asOf?: string | null): number | null {
  if (!dateOfBirth) return null
  const dob = new Date(dateOfBirth)
  if (Number.isNaN(dob.getTime())) return null
  const ref = asOf ? new Date(asOf) : new Date()
  if (Number.isNaN(ref.getTime())) return null
  let age = ref.getFullYear() - dob.getFullYear()
  const beforeBirthday =
    ref.getMonth() < dob.getMonth() ||
    (ref.getMonth() === dob.getMonth() && ref.getDate() < dob.getDate())
  if (beforeBirthday) age--
  return age >= 0 ? age : null
}

/**
 * Best age we can say for a performer: computed from their date of birth when
 * known, else the age someone typed in by hand. Same return shape as `ageOn` so
 * every existing caller (sorting, filtering, display) keeps working unchanged.
 */
export function effectiveAge(
  performer?: { dateOfBirth?: string | null; ageYears?: number | null } | null,
  asOf?: string | null,
): number | null {
  const fromDob = ageOn(performer?.dateOfBirth, asOf)
  if (fromDob !== null) return fromDob
  const typed = performer?.ageYears
  return typed != null && typed >= 0 ? typed : null
}

/**
 * True when an age came from a typed-in guess rather than a real birth date —
 * worth a visual hint (e.g. "~8") since it won't advance on its own like a
 * DOB-derived age does.
 */
export function isApproximateAge(
  performer?: { dateOfBirth?: string | null; ageYears?: number | null } | null,
): boolean {
  return !performer?.dateOfBirth && performer?.ageYears != null
}
