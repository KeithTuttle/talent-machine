import { Check, CheckCheck, Eye } from 'lucide-vue-next'
import type { TeachStatus } from '@/types'

export const TEACH_STATUSES: TeachStatus[] = ['Taught', 'NeedsReview', 'Complete']

export const TEACH_LABELS: Record<TeachStatus, string> = {
  Taught: 'Taught',
  NeedsReview: 'Needs review',
  Complete: 'Complete',
}

/** Icon + color for a teach status (null = not taught → no icon). */
export function teachIcon(status?: TeachStatus | null) {
  switch (status) {
    case 'Complete':
      return { icon: CheckCheck, class: 'text-emerald-600 dark:text-emerald-400', label: 'Complete' }
    case 'Taught':
      return { icon: Check, class: 'text-blue-600 dark:text-blue-400', label: 'Taught' }
    case 'NeedsReview':
      return { icon: Eye, class: 'text-amber-600 dark:text-amber-400', label: 'Needs review' }
    default:
      return null
  }
}
