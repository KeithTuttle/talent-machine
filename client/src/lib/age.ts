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
