import { reactive } from 'vue'

export interface ConfirmOptions {
  title: string
  message?: string
  confirmText?: string
  cancelText?: string
  /** Style the confirm button as destructive (red). */
  destructive?: boolean
  /** Require the user to type this exact text before Confirm enables
   * (guard for high-blast-radius deletes, e.g. a studio name). */
  requireText?: string
}

interface ConfirmState extends ConfirmOptions {
  open: boolean
  /** What the user has typed toward requireText (owned by ConfirmDialog). */
  typed: string
  resolve?: (value: boolean) => void
}

const state = reactive<ConfirmState>({ open: false, title: '', typed: '' })

/** Reactive confirm-dialog state (read by ConfirmDialog.vue). */
export function useConfirmState() {
  return state
}

/**
 * Promise-based confirmation. Usage:
 *   if (!(await confirm({ title: 'Delete?', destructive: true }))) return
 */
export function confirm(options: ConfirmOptions): Promise<boolean> {
  return new Promise((resolve) => {
    state.open = true
    state.title = options.title
    state.message = options.message
    state.confirmText = options.confirmText ?? 'Confirm'
    state.cancelText = options.cancelText ?? 'Cancel'
    state.destructive = options.destructive ?? false
    state.requireText = options.requireText
    state.typed = ''
    state.resolve = resolve
  })
}

export function resolveConfirm(result: boolean) {
  state.open = false
  state.resolve?.(result)
  state.resolve = undefined
}
