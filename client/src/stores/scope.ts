import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { api } from '@/lib/api'
import type { Season, Production } from '@/types'

/**
 * Global season + production selection. The selected production scopes the
 * planner (numbers, cast, groups, roles); the selected season filters which
 * productions are offered. Both persist to localStorage across reloads.
 */
export const useScopeStore = defineStore('scope', () => {
  const seasons = ref<Season[]>([])
  const productions = ref<Production[]>([])
  const selectedSeasonId = ref<number | null>(
    Number(localStorage.getItem('selectedSeasonId')) || null,
  )
  const selectedProductionId = ref<number | null>(
    Number(localStorage.getItem('selectedProductionId')) || null,
  )
  const loading = ref(false)

  const selectedSeason = computed(
    () => seasons.value.find((s) => s.id === selectedSeasonId.value) ?? null,
  )
  const selectedProduction = computed(
    () => productions.value.find((p) => p.id === selectedProductionId.value) ?? null,
  )
  const seasonProductions = computed(() =>
    productions.value.filter((p) => p.seasonId === selectedSeasonId.value),
  )

  async function fetchAll() {
    loading.value = true
    try {
      const [{ data: seasonData }, { data: productionData }] = await Promise.all([
        api.get<Season[]>('/seasons'),
        api.get<Production[]>('/productions'),
      ])
      seasons.value = seasonData
      productions.value = productionData

      // Seasons arrive newest-year first; default to the newest with a valid selection.
      if (!seasons.value.some((s) => s.id === selectedSeasonId.value)) {
        selectedSeasonId.value = seasonData[0]?.id ?? null
      }
      ensureProductionSelection()
    } finally {
      loading.value = false
    }
  }

  function selectSeason(id: number) {
    selectedSeasonId.value = id
    localStorage.setItem('selectedSeasonId', String(id))
    ensureProductionSelection()
  }

  function selectProduction(id: number) {
    selectedProductionId.value = id
    localStorage.setItem('selectedProductionId', String(id))
  }

  /** Clear all scoped state + persisted selections — used when switching companies. */
  function reset() {
    seasons.value = []
    productions.value = []
    selectedSeasonId.value = null
    selectedProductionId.value = null
    localStorage.removeItem('selectedSeasonId')
    localStorage.removeItem('selectedProductionId')
  }

  /** Keep the production selection inside the selected season. */
  function ensureProductionSelection() {
    const inSeason = seasonProductions.value
    if (!inSeason.some((p) => p.id === selectedProductionId.value)) {
      selectedProductionId.value = inSeason[0]?.id ?? null
      if (selectedProductionId.value !== null)
        localStorage.setItem('selectedProductionId', String(selectedProductionId.value))
    }
  }

  return {
    seasons,
    productions,
    selectedSeasonId,
    selectedProductionId,
    selectedSeason,
    selectedProduction,
    seasonProductions,
    loading,
    fetchAll,
    selectSeason,
    selectProduction,
    reset,
  }
})
