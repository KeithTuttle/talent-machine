import { reactive } from 'vue'

export type ToastKind = 'success' | 'error' | 'info'

export interface ToastItem {
  id: number
  kind: ToastKind
  message: string
}

const state = reactive<{ items: ToastItem[] }>({ items: [] })
let seq = 0

/** Reactive list of active toasts (read by Toaster.vue). */
export function useToasts() {
  return state
}

export function pushToast(kind: ToastKind, message: string, timeout = 3500) {
  const id = ++seq
  state.items.push({ id, kind, message })
  if (timeout > 0) window.setTimeout(() => dismissToast(id), timeout)
  return id
}

export function dismissToast(id: number) {
  const i = state.items.findIndex((t) => t.id === id)
  if (i !== -1) state.items.splice(i, 1)
}

/** Convenience helpers: `toast.success('Saved')`, `toast.error('…')`. */
export const toast = {
  success: (message: string) => pushToast('success', message),
  error: (message: string) => pushToast('error', message),
  info: (message: string) => pushToast('info', message),
}
