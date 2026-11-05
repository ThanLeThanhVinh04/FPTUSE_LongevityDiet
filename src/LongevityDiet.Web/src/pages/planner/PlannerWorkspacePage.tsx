import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  AlertTriangle,
  CalendarDays,
  CheckCircle2,
  Clock3,
  Edit3,
  History,
  Info,
  Leaf,
  Plus,
  RefreshCw,
  ShieldAlert,
  Sparkles,
  ThumbsDown,
  ThumbsUp,
  Trash2,
  UserRound,
  Utensils,
} from 'lucide-react'
import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../auth/authState'
import { api } from '../../lib/api'
import { getApiErrorMessage } from '../../lib/apiError'
import { FmdWorkspaceSection } from './FmdWorkspaceSection'

export type PlannedMealDto = {
  id: string
  mealPlanDayId: string
  slotOrder: number
  slotType: string
  plannedTime: string
  candidateCode: string
  title: string
  description: string
  cuisine: string
  servingSize: number
  servingUnit: string
  caloriesKcal: number
  proteinGrams: number
  carbsGrams: number
  fatGrams: number
  fiberGrams: number
  isPlantBased: boolean
  hasLegumes: boolean
  hasWholeGrains: boolean
  containsFish: boolean
  allergens: string[]
  ingredients: string[]
  deterministicScore: number
  reasonCodes: string[]
  explanationSummary: string
  regenerationCount: number
}

export type MealPlanDayDto = {
  id: string
  dayNumber: number
  planDate: string
  targetCaloriesKcal: number
  totalPlannedCaloriesKcal: number
  dailyFocusCode: string
  dailyFocusTitle: string
  meals: PlannedMealDto[]
}

export type MealPlanDto = {
  id: string
  userId: string
  ruleSetVersion: string
  revisionNumber: number
  previousPlanId: string | null
  status: string
  startDate: string
  endDate: string
  mealsPerDay: number
  timeZone: string
  dietaryPattern: string
  appliedAllergies: string[]
  appliedExclusions: string[]
  reasonCodes: string[]
  planExplanation: string
  generatedAt: string
  updatedAt: string
  days: MealPlanDayDto[]
}

export type DietaryPreferenceDto = {
  userId: string
  dietaryPattern: string
  allergies: string[]
  excludedFoods: string[]
  preferredCuisine: string | null
  updatedAt: string
}

export type CandidateMealDto = {
  candidateCode: string
  title: string
  description: string
  category: string
  cuisine: string
  supportedSlots: string[]
  servingSize: number
  servingUnit: string
  caloriesKcal: number
  proteinGrams: number
  carbsGrams: number
  fatGrams: number
  fiberGrams: number
  isPlantBased: boolean
  hasLegumes: boolean
  hasWholeGrains: boolean
  containsFish: boolean
  allergens: string[]
  ingredients: string[]
}

export type RecommendationScoreComponentDto = {
  componentCode: string
  label: string
  normalizedValue: number
  weight: number
  weightedDelta: number
  isPenalty: boolean
}

export type RecommendationReasonDetailDto = {
  reasonCode: string
  title: string
  explanation: string
}

export type RankedRecommendationCandidateDto = {
  rank: number
  candidateCode: string
  title: string
  description: string
  cuisine: string
  servingSize: number
  servingUnit: string
  caloriesKcal: number
  proteinGrams: number
  carbsGrams: number
  fatGrams: number
  fiberGrams: number
  isPlantBased: boolean
  hasLegumes: boolean
  hasWholeGrains: boolean
  containsFish: boolean
  allergens: string[]
  ingredients: string[]
  finalScore: number
  isDisliked: boolean
  isPreferred: boolean
  scoreComponents: RecommendationScoreComponentDto[]
  reasonCodes: string[]
  reasonDetails: RecommendationReasonDetailDto[]
  deterministicExplanation: string
  explanationText: string
  explanationSource: string
}

export type RejectedRecommendationCandidateDto = {
  candidateCode: string
  title: string
  rejectionReasonCodes: string[]
  rejectionExplanation: string
}

export type RecommendationCatalogResultDto = {
  contractVersion: string
  ruleSetVersion: string
  weightProfileVersion: string
  targetSlot: string
  dietaryPattern: string
  appliedAllergies: string[]
  appliedExclusions: string[]
  dislikedTokens: string[]
  preferredTokens: string[]
  acceptedCandidates: RankedRecommendationCandidateDto[]
  rejectedCandidates: RejectedRecommendationCandidateDto[]
}

export type UserFoodPreferenceDto = {
  id: string
  userId: string
  foodToken: string
  displayLabel: string
  preferenceKind: 'Preferred' | 'Disliked' | 'ExplicitExclusion' | string
  source: string
  notes: string | null
  updatedAt: string
}

export type MealReplacementHistoryDto = {
  id: string
  userId: string
  mealPlanId: string
  mealPlanDayId: string
  plannedMealId: string
  dayNumber: number
  slotType: string
  previousCandidateCode: string
  previousTitle: string
  previousScore: number
  replacementCandidateCode: string
  replacementTitle: string
  replacementScore: number
  replacementReasonCode: string
  reasonSummary: string
  appliedAllergies: string[]
  appliedExclusions: string[]
  ruleSetVersion: string
  weightProfileVersion: string
  replacedAt: string
}

export type ReplacePlannedMealResultDto = {
  updatedPlan: MealPlanDto
  replacedMeal: PlannedMealDto
  replacementAudit: MealReplacementHistoryDto
}

export type MealLogItemDto = {
  id: string
  foodCode: string | null
  foodName: string
  servingAmount: number
  servingUnit: string
  caloriesKcal: number
  proteinGrams: number
  carbsGrams: number
  fatGrams: number
  fiberGrams: number
  isPlantBased: boolean
}

export type MealLogDto = {
  id: string
  userId: string
  plannedMealId: string | null
  eatenAt: string
  localDate: string
  localTime: string
  timeZone: string
  mealType: string
  title: string
  notes: string | null
  totalCaloriesKcal: number
  totalProteinGrams: number
  totalCarbsGrams: number
  totalFatGrams: number
  totalFiberGrams: number
  createdAt: string
  updatedAt: string
  items: MealLogItemDto[]
}

export type EatingWindowSummaryDto = {
  userId: string
  localDate: string
  timeZone: string
  loggedMealCount: number
  caloricMealCount: number
  totalCaloriesKcal: number
  firstMealAt: string | null
  lastMealAt: string | null
  firstMealLocalTime: string | null
  lastMealLocalTime: string | null
  eatingWindowMinutes: number | null
  targetWindowMinutes: number
  isWithinTargetWindow: boolean
  configuredSleepTime: string | null
  minutesBeforeBedtime: number | null
  isLateEatingFlagged: boolean
  statusCode: string
  summaryMessage: string
  calculatedAt: string
}

const COMMON_ALLERGENS = [
  { code: 'peanut', label: 'Đậu phộng (Peanut)' },
  { code: 'shellfish', label: 'Hải sản có vỏ (Shellfish)' },
  { code: 'fish', label: 'Cá (Fish)' },
  { code: 'soy', label: 'Đậu nành (Soy)' },
  { code: 'gluten', label: 'Gluten' },
  { code: 'sesame', label: 'Mè / Vừng (Sesame)' },
  { code: 'tree-nut', label: 'Hạt cây (Tree nut)' },
]

const SLOT_LABELS: Record<string, string> = {
  Breakfast: 'Bữa sáng',
  Lunch: 'Bữa trưa',
  Dinner: 'Bữa tối',
  Snack: 'Bữa phụ',
}

const PREFERENCE_KIND_LABELS: Record<string, string> = {
  Preferred: 'Yêu thích (+Điểm ưu tiên)',
  Disliked: 'Không thích (-Điểm mềm, không cấm)',
  ExplicitExclusion: 'Loại trừ cứng (Cấm tuyệt đối)',
}

function getTodayIsoDate() {
  const now = new Date()
  const year = now.getFullYear()
  const month = String(now.getMonth() + 1).padStart(2, '0')
  const day = String(now.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function formatShortTime(value: string | null | undefined) {
  if (!value) {
    return '--:--'
  }
  return value.slice(0, 5)
}

function formatDurationMinutes(minutes: number | null | undefined) {
  if (minutes === null || minutes === undefined) {
    return '--:--'
  }
  const h = Math.floor(minutes / 60)
  const m = minutes % 60
  return `${h}h ${String(m).padStart(2, '0')}m`
}

function formatBedtimeGap(
  minutes: number | null | undefined,
  configuredSleepTime?: string | null,
) {
  if (minutes === null || minutes === undefined) {
    return configuredSleepTime ? '--:--' : 'Chưa đặt giờ ngủ'
  }
  if (minutes < 0) {
    return `Trễ ${Math.abs(minutes)} phút`
  }
  const h = Math.floor(minutes / 60)
  const m = minutes % 60
  return `${h}h ${String(m).padStart(2, '0')}m`
}

function toLocalDateTimeInput(dateStr: string, timeStr: string) {
  return `${dateStr}T${timeStr.slice(0, 5)}`
}

export function PlannerWorkspacePage() {
  const { user } = useAuth()
  const queryClient = useQueryClient()

  const todayIso = useMemo(() => getTodayIsoDate(), [])
  const [selectedDayIndex, setSelectedDayIndex] = useState(0)
  const [trackingDate, setTrackingDate] = useState(todayIso)
  const [logFromDate, setLogFromDate] = useState(() => {
    const d = new Date()
    d.setDate(d.getDate() - 6)
    return d.toISOString().slice(0, 10)
  })
  const [logToDate, setLogToDate] = useState(todayIso)

  const [dietaryPatternDraft, setDietaryPatternDraft] = useState<string | null>(null)
  const [preferredCuisineDraft, setPreferredCuisineDraft] = useState<string | null>(null)
  const [selectedAllergiesDraft, setSelectedAllergiesDraft] = useState<string[] | null>(null)
  const [excludedFoodsTextDraft, setExcludedFoodsTextDraft] = useState<string | null>(null)
  const [plannerFeedback, setPlannerFeedback] = useState<string | null>(null)
  const [plannerError, setPlannerError] = useState<string | null>(null)

  // Week 2 - Task 3 state: Recommendation v2 drawer + Food Preferences + Feedback
  const [inspectedMealId, setInspectedMealId] = useState<string | null>(null)
  const [prefTokenInput, setPrefTokenInput] = useState('')
  const [prefKindInput, setPrefKindInput] = useState('Disliked')
  const [prefNotesInput, setPrefNotesInput] = useState('')
  const [recFeedbackMessage, setRecFeedbackMessage] = useState<string | null>(null)
  const [recErrorMessage, setRecErrorMessage] = useState<string | null>(null)

  const [editingLogId, setEditingLogId] = useState<string | null>(null)
  const [logMealType, setLogMealType] = useState('Breakfast')
  const [logEatenAt, setLogEatenAt] = useState(`${todayIso}T07:30`)
  const [logTitle, setLogTitle] = useState('')
  const [logFoodCode, setLogFoodCode] = useState('')
  const [logFoodName, setLogFoodName] = useState('')
  const [logServingAmount, setLogServingAmount] = useState('1')
  const [logServingUnit, setLogServingUnit] = useState('khẩu phần')
  const [logCalories, setLogCalories] = useState('350')
  const [logProtein, setLogProtein] = useState('12')
  const [logCarbs, setLogCarbs] = useState('50')
  const [logFat, setLogFat] = useState('10')
  const [logFiber, setLogFiber] = useState('8')
  const [logNotes, setLogNotes] = useState('')
  const [logFeedback, setLogFeedback] = useState<string | null>(null)
  const [logError, setLogError] = useState<string | null>(null)

  const preferencesQuery = useQuery({
    queryKey: ['meal-plan-preferences'],
    queryFn: async () => (await api.get<DietaryPreferenceDto>('/meal-plans/preferences')).data,
  })

  const candidatesQuery = useQuery({
    queryKey: ['meal-plan-candidates'],
    queryFn: async () => (await api.get<CandidateMealDto[]>('/meal-plans/candidates')).data,
  })

  const currentPlanQuery = useQuery({
    queryKey: ['meal-plan-current'],
    queryFn: async () => {
      try {
        const res = await api.get<MealPlanDto>('/meal-plans/current')
        return res.data
      } catch (err: unknown) {
        if (
          typeof err === 'object' &&
          err !== null &&
          'response' in err &&
          (err as { response?: { status?: number } }).response?.status === 404
        ) {
          return null
        }
        throw err
      }
    },
  })

  const plan = currentPlanQuery.data

  const inspectedRecommendationsQuery = useQuery({
    queryKey: ['recommendations-for-meal', plan?.id, inspectedMealId],
    enabled: Boolean(plan?.id && inspectedMealId),
    queryFn: async () =>
      (
        await api.get<RecommendationCatalogResultDto>('/recommendations', {
          params: {
            mealPlanId: plan?.id,
            plannedMealId: inspectedMealId,
          },
        })
      ).data,
  })

  const foodPreferencesQuery = useQuery({
    queryKey: ['user-food-preferences'],
    queryFn: async () =>
      (await api.get<UserFoodPreferenceDto[]>('/recommendations/preferences')).data,
  })

  const replacementHistoryQuery = useQuery({
    queryKey: ['meal-replacement-history', plan?.id],
    queryFn: async () =>
      (
        await api.get<MealReplacementHistoryDto[]>('/recommendations/replacements', {
          params: { mealPlanId: plan?.id, limit: 20 },
        })
      ).data,
  })

  const eatingWindowQuery = useQuery({
    queryKey: ['eating-window-summary', trackingDate],
    queryFn: async () =>
      (
        await api.get<EatingWindowSummaryDto>('/eating-window/summary', {
          params: { date: trackingDate },
        })
      ).data,
  })

  const mealLogsQuery = useQuery({
    queryKey: ['meal-logs', logFromDate, logToDate],
    queryFn: async () =>
      (
        await api.get<MealLogDto[]>('/meal-logs', {
          params: { fromDate: logFromDate, toDate: logToDate },
        })
      ).data,
  })

  const dietaryPattern =
    dietaryPatternDraft ?? preferencesQuery.data?.dietaryPattern ?? 'PlantForward'
  const preferredCuisine =
    preferredCuisineDraft ?? preferencesQuery.data?.preferredCuisine ?? ''
  const selectedAllergies =
    selectedAllergiesDraft ?? preferencesQuery.data?.allergies ?? []
  const excludedFoodsText =
    excludedFoodsTextDraft ?? (preferencesQuery.data?.excludedFoods ?? []).join(', ')

  const setDietaryPattern = (value: string) => setDietaryPatternDraft(value)
  const setPreferredCuisine = (value: string) => setPreferredCuisineDraft(value)
  const setExcludedFoodsText = (value: string) => setExcludedFoodsTextDraft(value)

  const parseExcludedList = () =>
    excludedFoodsText
      .split(/[,;|]/)
      .map((item) => item.trim().toLowerCase())
      .filter(Boolean)

  const generatePlanMutation = useMutation({
    mutationFn: async () => {
      const response = await api.post<MealPlanDto>('/meal-plans/generate', {
        days: 7,
        dietaryPattern,
        allergies: selectedAllergies,
        excludedFoods: parseExcludedList(),
        preferredCuisine: preferredCuisine || null,
      })
      return response.data
    },
    onSuccess: (data) => {
      setPlannerError(null)
      setPlannerFeedback(
        `Đã tạo kế hoạch 7 ngày (Phiên bản #${data.revisionNumber} • ${data.ruleSetVersion}).`,
      )
      setSelectedDayIndex(0)
      setInspectedMealId(null)
      queryClient.setQueryData(['meal-plan-current'], data)
      void queryClient.invalidateQueries({ queryKey: ['meal-plan-preferences'] })
      void queryClient.invalidateQueries({ queryKey: ['meal-replacement-history'] })
    },
    onError: (err) => {
      setPlannerFeedback(null)
      setPlannerError(getApiErrorMessage(err, 'Không thể tạo thực đơn lúc này.'))
    },
  })

  const regeneratePlanMutation = useMutation({
    mutationFn: async (planId: string) => {
      const response = await api.post<MealPlanDto>(`/meal-plans/${planId}/regenerate`, {})
      return response.data
    },
    onSuccess: (data) => {
      setPlannerError(null)
      setPlannerFeedback(
        `Đã làm mới kế hoạch 7 ngày sang phiên bản #${data.revisionNumber}.`,
      )
      setInspectedMealId(null)
      queryClient.setQueryData(['meal-plan-current'], data)
    },
    onError: (err) => {
      setPlannerFeedback(null)
      setPlannerError(getApiErrorMessage(err, 'Không thể làm mới kế hoạch lúc này.'))
    },
  })

  const replaceMealMutation = useMutation({
    mutationFn: async ({
      planId,
      mealId,
      targetCandidateCode,
      replacementReasonCode,
    }: {
      planId: string
      mealId: string
      targetCandidateCode?: string | null
      replacementReasonCode?: string
    }) => {
      const response = await api.post<ReplacePlannedMealResultDto>(
        `/meal-plans/${planId}/meals/${mealId}/replace`,
        {
          targetCandidateCode: targetCandidateCode ?? null,
          replacementReasonCode: replacementReasonCode ?? 'USER_REQUESTED_VARIETY',
        },
      )
      return response.data
    },
    onSuccess: (data) => {
      setPlannerError(null)
      setPlannerFeedback(
        `Đã đổi món thành công: "${data.replacementAudit.previousTitle}" → "${data.replacedMeal.title}" (Điểm ${data.replacedMeal.deterministicScore}).`,
      )
      queryClient.setQueryData(['meal-plan-current'], data.updatedPlan)
      void queryClient.invalidateQueries({ queryKey: ['recommendations-for-meal'] })
      void queryClient.invalidateQueries({ queryKey: ['meal-replacement-history'] })
    },
    onError: (err) => {
      setPlannerFeedback(null)
      setPlannerError(getApiErrorMessage(err, 'Không thể đổi món ăn lúc này.'))
    },
  })

  const submitFeedbackMutation = useMutation({
    mutationFn: async ({
      candidateCode,
      feedbackType,
      plannedMealId,
    }: {
      candidateCode: string
      feedbackType: 'Like' | 'Dislike'
      plannedMealId?: string
    }) => {
      const response = await api.post('/recommendations/feedback', {
        candidateCode,
        feedbackType,
        reasonCode: feedbackType === 'Like' ? 'TASTE_MATCH' : 'DISLIKE_FLAVOR',
        plannedMealId: plannedMealId ?? null,
      })
      return response.data
    },
    onSuccess: (_data, variables) => {
      setRecErrorMessage(null)
      const actionLabel =
        variables.feedbackType === 'Like'
          ? 'Đã ghi nhận Yêu thích (+ưu tiên mềm)'
          : 'Đã ghi nhận Không thích (giảm bậc ưu tiên mềm, không xóa cứng)'
      setPlannerFeedback(`${actionLabel} cho mã món ${variables.candidateCode}.`)
      setRecFeedbackMessage(`${actionLabel} cho mã món ${variables.candidateCode}.`)
      void queryClient.invalidateQueries({ queryKey: ['user-food-preferences'] })
      void queryClient.invalidateQueries({ queryKey: ['recommendations-for-meal'] })
    },
    onError: (err) => {
      setRecErrorMessage(getApiErrorMessage(err, 'Không thể gửi phản hồi gợi ý.'))
    },
  })

  const upsertFoodPreferenceMutation = useMutation({
    mutationFn: async () => {
      const response = await api.post<UserFoodPreferenceDto>('/recommendations/preferences', {
        foodToken: prefTokenInput.trim(),
        preferenceKind: prefKindInput,
        displayLabel: prefTokenInput.trim(),
        notes: prefNotesInput.trim() || null,
      })
      return response.data
    },
    onSuccess: (saved) => {
      setRecErrorMessage(null)
      setRecFeedbackMessage(
        `Đã lưu "${saved.displayLabel}" vào nhóm ${PREFERENCE_KIND_LABELS[saved.preferenceKind] ?? saved.preferenceKind}.`,
      )
      setPrefTokenInput('')
      setPrefNotesInput('')
      void queryClient.invalidateQueries({ queryKey: ['user-food-preferences'] })
      void queryClient.invalidateQueries({ queryKey: ['meal-plan-preferences'] })
      void queryClient.invalidateQueries({ queryKey: ['recommendations-for-meal'] })
    },
    onError: (err) => {
      setRecFeedbackMessage(null)
      setRecErrorMessage(getApiErrorMessage(err, 'Không thể lưu tùy chỉnh thực phẩm.'))
    },
  })

  const removeFoodPreferenceMutation = useMutation({
    mutationFn: async (foodToken: string) => {
      await api.delete(`/recommendations/preferences/${encodeURIComponent(foodToken)}`)
    },
    onSuccess: () => {
      setRecErrorMessage(null)
      setRecFeedbackMessage('Đã xóa mục tùy chỉnh sở thích/loại trừ.')
      void queryClient.invalidateQueries({ queryKey: ['user-food-preferences'] })
      void queryClient.invalidateQueries({ queryKey: ['recommendations-for-meal'] })
    },
    onError: (err) => {
      setRecErrorMessage(getApiErrorMessage(err, 'Không thể xóa mục tùy chỉnh.'))
    },
  })

  const saveMealLogMutation = useMutation({
    mutationFn: async () => {
      const eatenAtIso = new Date(logEatenAt).toISOString()
      const payload = {
        eatenAt: eatenAtIso,
        mealType: logMealType,
        title: logTitle.trim() || logFoodName.trim(),
        plannedMealId: null,
        notes: logNotes.trim() || null,
        items: [
          {
            foodCode: logFoodCode.trim() || null,
            foodName: logFoodName.trim() || logTitle.trim(),
            servingAmount: Number(logServingAmount) || 1,
            servingUnit: logServingUnit.trim() || 'khẩu phần',
            caloriesKcal: Number(logCalories) || 0,
            proteinGrams: Number(logProtein) || 0,
            carbsGrams: Number(logCarbs) || 0,
            fatGrams: Number(logFat) || 0,
            fiberGrams: Number(logFiber) || 0,
            isPlantBased: true,
          },
        ],
      }

      if (editingLogId) {
        return (await api.put<MealLogDto>(`/meal-logs/${editingLogId}`, payload)).data
      }

      return (await api.post<MealLogDto>('/meal-logs', payload)).data
    },
    onSuccess: (savedLog) => {
      setLogError(null)
      setLogFeedback(
        editingLogId
          ? 'Đã cập nhật nhật ký bữa ăn và tính lại khung giờ ăn.'
          : 'Đã ghi nhận bữa ăn và cập nhật khung giờ ăn.',
      )
      setEditingLogId(null)
      setLogTitle('')
      setLogFoodCode('')
      setLogFoodName('')
      setLogNotes('')
      if (savedLog.localDate) {
        setTrackingDate(savedLog.localDate)
        if (savedLog.localDate < logFromDate) {
          setLogFromDate(savedLog.localDate)
        }
        if (savedLog.localDate > logToDate) {
          setLogToDate(savedLog.localDate)
        }
      }
      void queryClient.invalidateQueries({ queryKey: ['meal-logs'] })
      void queryClient.invalidateQueries({ queryKey: ['eating-window-summary'] })
    },
    onError: (err) => {
      setLogFeedback(null)
      setLogError(getApiErrorMessage(err, 'Không thể lưu nhật ký bữa ăn.'))
    },
  })

  const deleteMealLogMutation = useMutation({
    mutationFn: async (logId: string) => {
      await api.delete(`/meal-logs/${logId}`)
    },
    onSuccess: () => {
      setLogError(null)
      setLogFeedback('Đã xóa bữa ăn và tính toán lại khung giờ ăn.')
      if (editingLogId) {
        setEditingLogId(null)
      }
      void queryClient.invalidateQueries({ queryKey: ['meal-logs'] })
      void queryClient.invalidateQueries({ queryKey: ['eating-window-summary'] })
    },
    onError: (err) => {
      setLogFeedback(null)
      setLogError(getApiErrorMessage(err, 'Không thể xóa bữa ăn lúc này.'))
    },
  })

  const quickLogPlannedMealMutation = useMutation({
    mutationFn: async ({ dayDate, meal }: { dayDate: string; meal: PlannedMealDto }) => {
      const localDateTime = toLocalDateTimeInput(dayDate, meal.plannedTime)
      const eatenAtIso = new Date(localDateTime).toISOString()
      const response = await api.post<MealLogDto>('/meal-logs', {
        eatenAt: eatenAtIso,
        mealType: meal.slotType,
        title: meal.title,
        plannedMealId: meal.id,
        notes: `Ghi nhận từ thực đơn 7 ngày (${meal.candidateCode})`,
        items: [
          {
            foodCode: meal.candidateCode,
            foodName: meal.title,
            servingAmount: meal.servingSize,
            servingUnit: meal.servingUnit,
            caloriesKcal: meal.caloriesKcal,
            proteinGrams: meal.proteinGrams,
            carbsGrams: meal.carbsGrams,
            fatGrams: meal.fatGrams,
            fiberGrams: meal.fiberGrams,
            isPlantBased: meal.isPlantBased,
          },
        ],
      })
      return response.data
    },
    onSuccess: (savedLog) => {
      setLogError(null)
      setLogFeedback(`Đã ghi nhận "${savedLog.title}" vào nhật ký ngày ${savedLog.localDate}.`)
      setTrackingDate(savedLog.localDate)
      if (savedLog.localDate < logFromDate) {
        setLogFromDate(savedLog.localDate)
      }
      if (savedLog.localDate > logToDate) {
        setLogToDate(savedLog.localDate)
      }
      void queryClient.invalidateQueries({ queryKey: ['meal-logs'] })
      void queryClient.invalidateQueries({ queryKey: ['eating-window-summary'] })
    },
    onError: (err) => {
      setLogFeedback(null)
      setLogError(getApiErrorMessage(err, 'Không thể ghi nhận nhanh bữa ăn.'))
    },
  })

  const toggleAllergy = (code: string) => {
    const next = selectedAllergies.includes(code)
      ? selectedAllergies.filter((item) => item !== code)
      : [...selectedAllergies, code]
    setSelectedAllergiesDraft(next)
  }

  const handleCandidateSelect = (code: string) => {
    setLogFoodCode(code)
    const found = candidatesQuery.data?.find((c) => c.candidateCode === code)
    if (!found) {
      return
    }
    setLogTitle(found.title)
    setLogFoodName(found.title)
    setLogServingAmount(String(found.servingSize))
    setLogServingUnit(found.servingUnit)
    setLogCalories(String(found.caloriesKcal))
    setLogProtein(String(found.proteinGrams))
    setLogCarbs(String(found.carbsGrams))
    setLogFat(String(found.fatGrams))
    setLogFiber(String(found.fiberGrams))
  }

  const handleEditMealLog = (log: MealLogDto) => {
    setEditingLogId(log.id)
    setLogMealType(log.mealType)
    setLogEatenAt(toLocalDateTimeInput(log.localDate, log.localTime))
    setLogTitle(log.title)
    const primaryItem = log.items[0]
    setLogFoodCode(primaryItem?.foodCode ?? '')
    setLogFoodName(primaryItem?.foodName ?? log.title)
    setLogServingAmount(String(primaryItem?.servingAmount ?? 1))
    setLogServingUnit(primaryItem?.servingUnit ?? 'khẩu phần')
    setLogCalories(String(log.totalCaloriesKcal))
    setLogProtein(String(log.totalProteinGrams))
    setLogCarbs(String(log.totalCarbsGrams))
    setLogFat(String(log.totalFatGrams))
    setLogFiber(String(log.totalFiberGrams))
    setLogNotes(log.notes ?? '')
    setLogFeedback(null)
    setLogError(null)
  }

  const activeDay =
    plan && plan.days.length > 0
      ? plan.days[Math.min(selectedDayIndex, plan.days.length - 1)]
      : null

  const windowSummary = eatingWindowQuery.data

  return (
    <div className="site-shell">
      <header className="topbar">
        <Link className="brand" to="/" aria-label="Longevity home">
          <span className="brand-monogram">L</span>
          <span className="brand-copy">
            <strong>Longevity</strong>
            <small>Daily wellness companion</small>
          </span>
        </Link>

        <nav className="nav-links" aria-label="Điều hướng ứng dụng">
          <a className="active" href="#eating-window">Khung giờ ăn</a>
          <a href="#meal-planner">Thực đơn 7 ngày</a>
          <a href="#recommendation-v2">Gợi ý & Đổi món v2</a>
          <a href="#meal-logger">Nhật ký bữa ăn</a>
          <a href="#fmd-tracker">Chu kỳ FMD 5 ngày</a>
          <Link to="/">Trang giới thiệu</Link>
        </nav>

        <div className="header-actions">
          <Link className="account-button" to={user ? '/profile' : '/login'}>
            <UserRound size={16} strokeWidth={1.7} />
            <span>{user ? user.displayName : 'Đăng nhập'}</span>
          </Link>
        </div>
      </header>

      <main className="workspace-main">
        {/* EATING WINDOW SUMMARY SECTION */}
        <section className="workspace-section" id="eating-window">
          <div className="workspace-header-row">
            <div>
              <span className="eyebrow">NHỊP SINH HỌC & KHUNG GIỜ ĂN</span>
              <h1>Theo dõi kế hoạch ăn uống và khung giờ 12 tiếng.</h1>
            </div>
            <label className="date-picker-pill">
              <CalendarDays size={15} />
              <span>Ngày theo dõi:</span>
              <input
                aria-label="Ngày theo dõi khung giờ ăn"
                type="date"
                value={trackingDate}
                onChange={(e) => setTrackingDate(e.target.value)}
              />
            </label>
          </div>

          {eatingWindowQuery.isLoading ? (
            <div className="surface-card status-banner" role="status">
              Đang tính toán khung giờ ăn...
            </div>
          ) : eatingWindowQuery.isError ? (
            <div className="surface-card status-banner error-banner" role="alert">
              {getApiErrorMessage(eatingWindowQuery.error, 'Không thể tải khung giờ ăn.')}
            </div>
          ) : windowSummary ? (
            <div className="window-summary-grid">
              <article className="surface-card window-main-card">
                <div className="window-card-top">
                  <div>
                    <span className="micro-label">TỔNG HỢP NGÀY {windowSummary.localDate}</span>
                    <h2>{windowSummary.summaryMessage}</h2>
                  </div>
                  <span
                    className={`status-pill ${
                      windowSummary.isWithinTargetWindow && !windowSummary.isLateEatingFlagged
                        ? 'status-ok'
                        : 'status-warn'
                    }`}
                  >
                    {windowSummary.isWithinTargetWindow && !windowSummary.isLateEatingFlagged ? (
                      <>
                        <CheckCircle2 size={14} /> Đạt khuyến nghị
                      </>
                    ) : (
                      <>
                        <AlertTriangle size={14} /> Cần lưu ý
                      </>
                    )}
                  </span>
                </div>

                <div className="window-metrics-row">
                  <div className="window-metric-box">
                    <span>Bữa đầu tiên</span>
                    <strong>{formatShortTime(windowSummary.firstMealLocalTime)}</strong>
                    <small>Múi giờ {windowSummary.timeZone}</small>
                  </div>
                  <div className="window-metric-box">
                    <span>Bữa cuối cùng</span>
                    <strong>{formatShortTime(windowSummary.lastMealLocalTime)}</strong>
                    <small>{windowSummary.caloricMealCount} bữa sinh năng lượng</small>
                  </div>
                  <div className="window-metric-box">
                    <span>Độ dài khung giờ ăn</span>
                    <strong data-testid="eating-window-duration">
                      {formatDurationMinutes(windowSummary.eatingWindowMinutes)}
                    </strong>
                    <small>Mục tiêu ≤ {Math.floor(windowSummary.targetWindowMinutes / 60)} giờ</small>
                  </div>
                  <div className="window-metric-box">
                    <span>Khoảng nghỉ trước giờ ngủ</span>
                    <strong data-testid="bedtime-gap-value">
                      {formatBedtimeGap(
                        windowSummary.minutesBeforeBedtime,
                        windowSummary.configuredSleepTime,
                      )}
                    </strong>
                    <small>
                      Giờ ngủ: {formatShortTime(windowSummary.configuredSleepTime)} • Tổng{' '}
                      {windowSummary.totalCaloriesKcal} kcal
                    </small>
                  </div>
                </div>
              </article>
            </div>
          ) : null}
        </section>

        {/* 7-DAY DETERMINISTIC MEAL PLANNER + RECOMMENDATION V2 REPLACEMENT */}
        <section className="workspace-section" id="meal-planner">
          <div className="section-heading">
            <div>
              <span className="eyebrow">KẾ HOẠCH BỮA ĂN & GỢI Ý THAY THẾ (PLANNER + GRPC V2)</span>
              <h2>Thực đơn 7 ngày cá nhân hóa theo nguyên tắc trường thọ.</h2>
            </div>
          </div>

          <div className="planner-controls-card surface-card">
            <div className="card-heading">
              <span className="icon-box">
                <ShieldAlert size={18} strokeWidth={1.6} />
              </span>
              <div>
                <span className="micro-label">RÀNG BUỘC AN TOÀN & SỞ THÍCH</span>
                <h3>Dị ứng (loại trừ cứng), thực phẩm loại trừ và chế độ ăn</h3>
              </div>
            </div>

            <div className="planner-config-grid">
              <label className="field">
                <span>Chế độ ăn chủ đạo</span>
                <select
                  aria-label="Chế độ ăn chủ đạo"
                  className="plain-input"
                  value={dietaryPattern}
                  onChange={(e) => setDietaryPattern(e.target.value)}
                >
                  <option value="PlantForward">Chủ đạo thực vật (Plant-Forward)</option>
                  <option value="Pescatarian">Thực vật kết hợp cá (Pescatarian)</option>
                  <option value="StrictlyPlantBased">Thuần thực vật (Strictly Plant-Based)</option>
                </select>
              </label>

              <label className="field">
                <span>Ẩm thực ưu tiên</span>
                <select
                  aria-label="Ẩm thực ưu tiên"
                  className="plain-input"
                  value={preferredCuisine}
                  onChange={(e) => setPreferredCuisine(e.target.value)}
                >
                  <option value="">Kết hợp hài hòa</option>
                  <option value="Vietnamese">Ẩm thực Việt Nam</option>
                  <option value="Mediterranean">Ẩm thực Địa Trung Hải</option>
                </select>
              </label>

              <label className="field">
                <span>Thực phẩm / nguyên liệu muốn loại trừ (phân cách bằng dấu phẩy)</span>
                <input
                  aria-label="Thực phẩm loại trừ"
                  className="plain-input"
                  placeholder="Ví dụ: salmon, tofu, mushroom..."
                  value={excludedFoodsText}
                  onChange={(e) => setExcludedFoodsText(e.target.value)}
                />
              </label>
            </div>

            <div className="allergy-chips-block">
              <span className="field-label">Dị ứng bắt buộc loại trừ (Hard Constraint):</span>
              <div className="chip-group">
                {COMMON_ALLERGENS.map((allergen) => {
                  const active = selectedAllergies.includes(allergen.code)
                  return (
                    <button
                      key={allergen.code}
                      type="button"
                      className={`filter-chip ${active ? 'active' : ''}`}
                      onClick={() => toggleAllergy(allergen.code)}
                    >
                      {allergen.label}
                    </button>
                  )
                })}
              </div>
            </div>

            {plannerError && (
              <p className="form-error" role="alert">
                {plannerError}
              </p>
            )}
            {plannerFeedback && (
              <p className="form-success" role="status">
                {plannerFeedback}
              </p>
            )}

            <div className="planner-action-bar">
              <button
                type="button"
                className="primary-button"
                disabled={generatePlanMutation.isPending}
                onClick={() => generatePlanMutation.mutate()}
              >
                <Sparkles size={16} />
                {generatePlanMutation.isPending ? 'Đang tạo kế hoạch...' : 'Tạo kế hoạch 7 ngày'}
              </button>

              {plan && (
                <button
                  type="button"
                  className="secondary-button"
                  disabled={regeneratePlanMutation.isPending}
                  onClick={() => regeneratePlanMutation.mutate(plan.id)}
                >
                  <RefreshCw size={15} />
                  {regeneratePlanMutation.isPending
                    ? 'Đang làm mới...'
                    : 'Tạo lại toàn bộ kế hoạch'}
                </button>
              )}
            </div>
          </div>

          {currentPlanQuery.isLoading ? (
            <div className="surface-card status-banner" role="status">
              Đang tải kế hoạch bữa ăn hiện tại...
            </div>
          ) : !plan ? (
            <div className="surface-card empty-state-card">
              <Utensils size={28} />
              <h3>Bạn chưa có kế hoạch bữa ăn 7 ngày đang hoạt động</h3>
              <p>
                Chọn các ràng buộc dị ứng hoặc nguyên liệu muốn tránh ở trên, sau đó nhấn{' '}
                <strong>Tạo kế hoạch 7 ngày</strong> để hệ thống xây dựng thực đơn tất định từ danh
                mục thực phẩm trường thọ.
              </p>
            </div>
          ) : (
            <div className="plan-display-shell">
              <div className="plan-meta-banner surface-card">
                <div>
                  <span className="micro-label">
                    PHIÊN BẢN #{plan.revisionNumber} • {plan.ruleSetVersion} • {plan.startDate} →{' '}
                    {plan.endDate}
                  </span>
                  <p className="plan-explanation-text">{plan.planExplanation}</p>
                </div>
                <div className="plan-meta-tags">
                  <span className="meta-badge">{plan.dietaryPattern}</span>
                  <span className="meta-badge">{plan.mealsPerDay} bữa/ngày</span>
                  {plan.appliedAllergies.length > 0 && (
                    <span className="meta-badge warn-badge">
                      Loại dị ứng: {plan.appliedAllergies.join(', ')}
                    </span>
                  )}
                  {plan.appliedExclusions.length > 0 && (
                    <span className="meta-badge">
                      Loại trừ: {plan.appliedExclusions.join(', ')}
                    </span>
                  )}
                </div>
              </div>

              {/* DAY NAVIGATION */}
              <div className="day-tabs" role="tablist" aria-label="Chọn ngày trong kế hoạch 7 ngày">
                {plan.days.map((day, idx) => (
                  <button
                    key={day.id}
                    type="button"
                    role="tab"
                    aria-selected={idx === selectedDayIndex}
                    className={`day-tab ${idx === selectedDayIndex ? 'active' : ''}`}
                    onClick={() => {
                      setSelectedDayIndex(idx)
                      setInspectedMealId(null)
                    }}
                  >
                    <span>Ngày {day.dayNumber}</span>
                    <strong>{day.planDate.slice(5)}</strong>
                    <small>{day.totalPlannedCaloriesKcal} kcal</small>
                  </button>
                ))}
              </div>

              {activeDay && (
                <div className="active-day-panel">
                  <div className="day-focus-bar">
                    <div>
                      <span className="micro-label">TRỌNG TÂM NGÀY {activeDay.dayNumber}</span>
                      <h3>{activeDay.dailyFocusTitle}</h3>
                    </div>
                    <span className="day-calorie-pill">
                      Tổng năng lượng dự kiến: <strong>{activeDay.totalPlannedCaloriesKcal} kcal</strong>
                    </span>
                  </div>

                  <div className="planned-meals-grid">
                    {activeDay.meals.map((meal) => {
                      const isInspected = inspectedMealId === meal.id
                      return (
                        <article
                          key={meal.id}
                          className="surface-card planned-meal-card"
                          data-testid={`planned-meal-card-${meal.slotType.toLowerCase()}`}
                        >
                          <div className="planned-meal-top">
                            <span className="slot-badge">
                              <Clock3 size={13} /> {SLOT_LABELS[meal.slotType] ?? meal.slotType} •{' '}
                              {formatShortTime(meal.plannedTime)}
                            </span>
                            <span className="score-badge" title="Điểm quy tắc tất định">
                              Điểm: {meal.deterministicScore}
                            </span>
                          </div>

                          <h4 className="planned-meal-title">{meal.title}</h4>
                          <p className="planned-meal-desc">{meal.description}</p>

                          <div className="macro-strip">
                            <span>
                              <strong>{meal.caloriesKcal}</strong> kcal
                            </span>
                            <span>
                              Đạm: <strong>{meal.proteinGrams}g</strong>
                            </span>
                            <span>
                              Tinh bột: <strong>{meal.carbsGrams}g</strong>
                            </span>
                            <span>
                              Béo: <strong>{meal.fatGrams}g</strong>
                            </span>
                            <span>
                              Xơ: <strong>{meal.fiberGrams}g</strong>
                            </span>
                          </div>

                          <div className="reason-tags">
                            {meal.isPlantBased && (
                              <span className="reason-chip">
                                <Leaf size={12} /> Thực vật
                              </span>
                            )}
                            {meal.hasLegumes && <span className="reason-chip">Đậu & Hạt</span>}
                            {meal.hasWholeGrains && (
                              <span className="reason-chip">Ngũ cốc nguyên hạt</span>
                            )}
                            {meal.containsFish && <span className="reason-chip">Cá Omega-3</span>}
                            {meal.reasonCodes.map((code) => (
                              <span key={code} className="reason-code-chip">
                                {code}
                              </span>
                            ))}
                          </div>

                          <p className="meal-explanation">{meal.explanationSummary}</p>

                          {/* FEEDBACK & WHY THIS RECOMMENDATION ROW */}
                          <div className="meal-feedback-strip">
                            <button
                              type="button"
                              className="feedback-pill-btn"
                              disabled={submitFeedbackMutation.isPending}
                              onClick={() =>
                                submitFeedbackMutation.mutate({
                                  candidateCode: meal.candidateCode,
                                  feedbackType: 'Like',
                                  plannedMealId: meal.id,
                                })
                              }
                            >
                              <ThumbsUp size={13} /> Thích
                            </button>
                            <button
                              type="button"
                              className="feedback-pill-btn"
                              disabled={submitFeedbackMutation.isPending}
                              onClick={() =>
                                submitFeedbackMutation.mutate({
                                  candidateCode: meal.candidateCode,
                                  feedbackType: 'Dislike',
                                  plannedMealId: meal.id,
                                })
                              }
                            >
                              <ThumbsDown size={13} /> Không thích
                            </button>
                            <button
                              type="button"
                              className={`feedback-pill-btn ${isInspected ? 'active' : ''}`}
                              onClick={() =>
                                setInspectedMealId(isInspected ? null : meal.id)
                              }
                            >
                              <Info size={13} /> Vì sao gợi ý này? / Chọn món đổi
                            </button>
                          </div>

                          <div className="planned-meal-actions">
                            <button
                              type="button"
                              className="secondary-button small-button"
                              disabled={replaceMealMutation.isPending}
                              onClick={() =>
                                replaceMealMutation.mutate({
                                  planId: plan.id,
                                  mealId: meal.id,
                                })
                              }
                            >
                              <RefreshCw size={14} />
                              Đổi món khác
                              {meal.regenerationCount > 0 ? ` (${meal.regenerationCount})` : ''}
                            </button>

                            <button
                              type="button"
                              className="primary-button small-button"
                              disabled={quickLogPlannedMealMutation.isPending}
                              onClick={() =>
                                quickLogPlannedMealMutation.mutate({
                                  dayDate: activeDay.planDate,
                                  meal,
                                })
                              }
                            >
                              <Plus size={14} />
                              Ghi nhận đã ăn
                            </button>
                          </div>
                        </article>
                      )
                    })}
                  </div>

                  {/* EXPANDED RECOMMENDATION V2 EXPLANATION & REPLACEMENT DRAWER */}
                  {inspectedMealId && (
                    <div
                      className="surface-card recommendation-inspector-card"
                      data-testid="recommendation-inspector"
                    >
                      <div className="inspector-header">
                        <div>
                          <span className="micro-label">
                            GỢI Ý THAY THẾ & GIẢI THÍCH TẤT ĐỊNH (GRPC RECOMMENDATION V2)
                          </span>
                          <h3>
                            Vì sao gợi ý món này & Danh sách món thay thế an toàn
                          </h3>
                        </div>
                        <button
                          type="button"
                          className="secondary-button small-button"
                          onClick={() => setInspectedMealId(null)}
                        >
                          Đóng bảng gợi ý
                        </button>
                      </div>

                      {inspectedRecommendationsQuery.isLoading ? (
                        <p className="status-text" role="status">
                          Đang xếp hạng món ăn qua engine Recommendation v2...
                        </p>
                      ) : inspectedRecommendationsQuery.isError ? (
                        <p className="form-error" role="alert">
                          {getApiErrorMessage(
                            inspectedRecommendationsQuery.error,
                            'Không thể tải danh sách gợi ý thay thế.',
                          )}
                        </p>
                      ) : inspectedRecommendationsQuery.data ? (
                        <div className="inspector-body">
                          <div className="inspector-meta-strip">
                            <span className="meta-badge">
                              Bộ quy tắc: {inspectedRecommendationsQuery.data.ruleSetVersion}
                            </span>
                            <span className="meta-badge">
                              Trọng số: {inspectedRecommendationsQuery.data.weightProfileVersion}
                            </span>
                            <span className="meta-badge">
                              Khung bữa:{' '}
                              {SLOT_LABELS[inspectedRecommendationsQuery.data.targetSlot] ??
                                inspectedRecommendationsQuery.data.targetSlot}
                            </span>
                          </div>

                          <div className="inspector-candidates-list">
                            {inspectedRecommendationsQuery.data.acceptedCandidates.map(
                              (candidate) => (
                                <div
                                  key={candidate.candidateCode}
                                  className="inspector-candidate-row"
                                >
                                  <div className="inspector-candidate-main">
                                    <div className="inspector-candidate-title-row">
                                      <span className="rank-pill">Hạng #{candidate.rank}</span>
                                      <strong>{candidate.title}</strong>
                                      <span className="score-badge">
                                        {candidate.finalScore} điểm
                                      </span>
                                      {candidate.isPreferred && (
                                        <span className="meta-badge">Yêu thích</span>
                                      )}
                                      {candidate.isDisliked && (
                                        <span className="meta-badge warn-badge">
                                          Không thích (Đã trừ điểm mềm)
                                        </span>
                                      )}
                                    </div>
                                    <p className="inspector-explanation">
                                      {candidate.explanationText}
                                    </p>
                                    <div className="score-breakdown-chips">
                                      {candidate.scoreComponents.map((comp) => (
                                        <span
                                          key={comp.componentCode}
                                          className={`score-comp-chip ${comp.isPenalty ? 'penalty' : ''}`}
                                        >
                                          {comp.label}:{' '}
                                          <strong>
                                            {comp.weightedDelta >= 0
                                              ? `+${comp.weightedDelta}`
                                              : comp.weightedDelta}
                                          </strong>
                                        </span>
                                      ))}
                                    </div>
                                  </div>

                                  <div className="inspector-candidate-actions">
                                    <button
                                      type="button"
                                      className="primary-button small-button"
                                      disabled={replaceMealMutation.isPending}
                                      onClick={() =>
                                        replaceMealMutation.mutate({
                                          planId: plan.id,
                                          mealId: inspectedMealId,
                                          targetCandidateCode: candidate.candidateCode,
                                          replacementReasonCode: 'USER_SELECTED_CANDIDATE',
                                        })
                                      }
                                    >
                                      <RefreshCw size={13} /> Chọn đổi sang món này
                                    </button>
                                  </div>
                                </div>
                              ),
                            )}
                          </div>

                          {inspectedRecommendationsQuery.data.rejectedCandidates.length > 0 && (
                            <div className="rejected-candidates-box">
                              <span className="micro-label">
                                CÁC MÓN ĐÃ BỊ LOẠI BỞI RÀNG BUỘC CỨNG (HARD CONSTRAINTS)
                              </span>
                              <div className="rejected-list">
                                {inspectedRecommendationsQuery.data.rejectedCandidates.map(
                                  (rej) => (
                                    <div key={rej.candidateCode} className="rejected-item">
                                      <strong>{rej.title}</strong>
                                      <span>{rej.rejectionExplanation}</span>
                                    </div>
                                  ),
                                )}
                              </div>
                            </div>
                          )}
                        </div>
                      ) : null}
                    </div>
                  )}
                </div>
              )}
            </div>
          )}
        </section>

        {/* WEEK 2 TASK 3: PREFERENCES / DISLIKE VS EXCLUSION & AUDIT REPLACEMENT HISTORY */}
        <section className="workspace-section" id="recommendation-v2">
          <div className="section-heading">
            <div>
              <span className="eyebrow">CÁ NHÂN HÓA GỢI Ý V2 & KIỂM TOÁN ĐỔI MÓN</span>
              <h2>Tùy chỉnh Yêu thích / Không thích / Loại trừ cứng và lịch sử đổi món.</h2>
            </div>
          </div>

          <div className="logger-layout">
            {/* FOOD PREFERENCES VS EXPLICIT EXCLUSION */}
            <div className="surface-card logger-form-card">
              <div className="card-heading">
                <span className="icon-box">
                  <Sparkles size={18} strokeWidth={1.6} />
                </span>
                <div>
                  <span className="micro-label">PHÂN TÁCH DISLIKE MỀM & LOẠI TRỪ CỨNG</span>
                  <h3>Cài đặt Sở thích / Không thích / Loại trừ</h3>
                </div>
              </div>

              <form
                onSubmit={(e) => {
                  e.preventDefault()
                  if (prefTokenInput.trim()) {
                    upsertFoodPreferenceMutation.mutate()
                  }
                }}
              >
                <div className="profile-grid">
                  <label className="field">
                    <span>Mã món hoặc nguyên liệu</span>
                    <input
                      aria-label="Mã món hoặc nguyên liệu"
                      className="plain-input"
                      placeholder="Ví dụ: salmon, tofu, mushroom..."
                      required
                      value={prefTokenInput}
                      onChange={(e) => setPrefTokenInput(e.target.value)}
                    />
                  </label>

                  <label className="field">
                    <span>Mức độ tác động</span>
                    <select
                      aria-label="Mức độ tác động"
                      className="plain-input"
                      value={prefKindInput}
                      onChange={(e) => setPrefKindInput(e.target.value)}
                    >
                      <option value="Preferred">Yêu thích (+8 điểm ưu tiên mềm)</option>
                      <option value="Disliked">Không thích (-18 điểm mềm, không cấm cứng)</option>
                      <option value="ExplicitExclusion">Loại trừ cứng (Cấm tuyệt đối)</option>
                    </select>
                  </label>

                  <label className="field span-2">
                    <span>Ghi chú lý do (tùy chọn)</span>
                    <input
                      aria-label="Ghi chú lý do"
                      className="plain-input"
                      placeholder="Ví dụ: Không hợp vị hoặc muốn tránh hoàn toàn..."
                      value={prefNotesInput}
                      onChange={(e) => setPrefNotesInput(e.target.value)}
                    />
                  </label>
                </div>

                {recErrorMessage && (
                  <p className="form-error" role="alert">
                    {recErrorMessage}
                  </p>
                )}
                {recFeedbackMessage && (
                  <p className="form-success" role="status">
                    {recFeedbackMessage}
                  </p>
                )}

                <div className="logger-form-actions">
                  <button
                    type="submit"
                    className="primary-button"
                    disabled={upsertFoodPreferenceMutation.isPending}
                  >
                    <Plus size={16} /> Lưu tùy chỉnh gợi ý
                  </button>
                </div>
              </form>

              <div className="preference-items-list">
                {foodPreferencesQuery.data && foodPreferencesQuery.data.length > 0 ? (
                  foodPreferencesQuery.data.map((pref) => (
                    <div key={pref.id} className="meal-log-item-row">
                      <div className="meal-log-info-col">
                        <strong>{pref.displayLabel}</strong>
                        <p>
                          {PREFERENCE_KIND_LABELS[pref.preferenceKind] ?? pref.preferenceKind} •
                          Nguồn: {pref.source}
                        </p>
                        {pref.notes && <small className="log-note">{pref.notes}</small>}
                      </div>
                      <div className="meal-log-actions-col">
                        <button
                          type="button"
                          className="icon-action-btn danger"
                          aria-label={`Xóa ${pref.foodToken}`}
                          onClick={() => removeFoodPreferenceMutation.mutate(pref.foodToken)}
                        >
                          <Trash2 size={15} />
                        </button>
                      </div>
                    </div>
                  ))
                ) : (
                  <p className="status-text">
                    Chưa có mục Yêu thích / Không thích / Loại trừ riêng nào.
                  </p>
                )}
              </div>
            </div>

            {/* MEAL REPLACEMENT AUDIT HISTORY */}
            <div className="surface-card logger-history-card">
              <div className="card-heading">
                <span className="icon-box">
                  <History size={18} strokeWidth={1.6} />
                </span>
                <div>
                  <span className="micro-label">KIỂM TOÁN LỊCH SỬ ĐỔI MÓN (AUDIT TRAIL)</span>
                  <h3>Lịch sử thay thế bữa ăn trong thực đơn</h3>
                </div>
              </div>

              {!replacementHistoryQuery.data || replacementHistoryQuery.data.length === 0 ? (
                <div className="empty-history">
                  <p>
                    Chưa có lịch sử đổi món nào. Hãy nhấn <strong>Đổi món khác</strong> trên thực
                    đơn 7 ngày để xem bản ghi kiểm toán tại đây.
                  </p>
                </div>
              ) : (
                <div className="meal-log-list" data-testid="replacement-history-list">
                  {replacementHistoryQuery.data.map((item) => (
                    <div key={item.id} className="meal-log-item-row">
                      <div className="meal-log-time-col">
                        <strong>Ngày {item.dayNumber}</strong>
                        <span>{SLOT_LABELS[item.slotType] ?? item.slotType}</span>
                        <small>{item.ruleSetVersion}</small>
                      </div>
                      <div className="meal-log-info-col">
                        <strong>
                          {item.previousTitle} → {item.replacementTitle}
                        </strong>
                        <p>
                          Điểm: {item.previousScore} → {item.replacementScore} • Mã lý do:{' '}
                          {item.replacementReasonCode}
                        </p>
                        <small className="log-note">{item.reasonSummary}</small>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </section>

        {/* MEAL LOG FORM & HISTORY SECTION */}
        <section className="workspace-section" id="meal-logger">
          <div className="section-heading">
            <div>
              <span className="eyebrow">NHẬT KÝ ĂN UỐNG & LỊCH SỬ</span>
              <h2>Ghi nhận bữa ăn thực tế và tự động tính lại khung giờ ăn.</h2>
            </div>
          </div>

          <div className="logger-layout">
            <form
              className="surface-card logger-form-card"
              onSubmit={(e) => {
                e.preventDefault()
                saveMealLogMutation.mutate()
              }}
            >
              <div className="card-heading">
                <span className="icon-box">
                  <Utensils size={18} strokeWidth={1.6} />
                </span>
                <div>
                  <span className="micro-label">
                    {editingLogId ? 'CẬP NHẬT BỮA ĂN' : 'THÊM BỮA ĂN MỚI'}
                  </span>
                  <h3>{editingLogId ? 'Chỉnh sửa nhật ký bữa ăn' : 'Ghi nhận bữa ăn'}</h3>
                </div>
              </div>

              <label className="field">
                <span>Chọn nhanh từ danh mục món ăn (tùy chọn)</span>
                <select
                  aria-label="Chọn món từ danh mục"
                  className="plain-input"
                  value={logFoodCode}
                  onChange={(e) => handleCandidateSelect(e.target.value)}
                >
                  <option value="">-- Nhập thủ công hoặc chọn món có sẵn --</option>
                  {candidatesQuery.data?.map((c) => (
                    <option key={c.candidateCode} value={c.candidateCode}>
                      {c.title} ({c.caloriesKcal} kcal)
                    </option>
                  ))}
                </select>
              </label>

              <div className="profile-grid">
                <label className="field">
                  <span>Bữa ăn</span>
                  <select
                    aria-label="Loại bữa ăn"
                    className="plain-input"
                    value={logMealType}
                    onChange={(e) => setLogMealType(e.target.value)}
                  >
                    <option value="Breakfast">Bữa sáng (Breakfast)</option>
                    <option value="Lunch">Bữa trưa (Lunch)</option>
                    <option value="Dinner">Bữa tối (Dinner)</option>
                    <option value="Snack">Bữa phụ (Snack)</option>
                  </select>
                </label>

                <label className="field">
                  <span>Thời điểm ăn</span>
                  <input
                    aria-label="Thời điểm ăn"
                    className="plain-input"
                    type="datetime-local"
                    required
                    value={logEatenAt}
                    onChange={(e) => setLogEatenAt(e.target.value)}
                  />
                </label>

                <label className="field span-2">
                  <span>Tên bữa ăn / Món ăn chính</span>
                  <input
                    aria-label="Tên bữa ăn"
                    className="plain-input"
                    placeholder="Ví dụ: Cơm gạo lứt đậu lăng và bông cải xanh"
                    required
                    value={logTitle}
                    onChange={(e) => {
                      setLogTitle(e.target.value)
                      if (!logFoodName) {
                        setLogFoodName(e.target.value)
                      }
                    }}
                  />
                </label>

                <label className="field">
                  <span>Định lượng</span>
                  <input
                    aria-label="Định lượng"
                    className="plain-input"
                    type="number"
                    step="0.1"
                    min="0.1"
                    required
                    value={logServingAmount}
                    onChange={(e) => setLogServingAmount(e.target.value)}
                  />
                </label>

                <label className="field">
                  <span>Đơn vị</span>
                  <input
                    aria-label="Đơn vị khẩu phần"
                    className="plain-input"
                    required
                    value={logServingUnit}
                    onChange={(e) => setLogServingUnit(e.target.value)}
                  />
                </label>

                <label className="field">
                  <span>Năng lượng (kcal)</span>
                  <input
                    aria-label="Năng lượng kcal"
                    className="plain-input"
                    type="number"
                    min="0"
                    max="5000"
                    required
                    value={logCalories}
                    onChange={(e) => setLogCalories(e.target.value)}
                  />
                </label>

                <label className="field">
                  <span>Đạm (g)</span>
                  <input
                    aria-label="Đạm (g)"
                    className="plain-input"
                    type="number"
                    step="0.1"
                    min="0"
                    value={logProtein}
                    onChange={(e) => setLogProtein(e.target.value)}
                  />
                </label>

                <label className="field span-2">
                  <span>Ghi chú bữa ăn</span>
                  <input
                    aria-label="Ghi chú bữa ăn"
                    className="plain-input"
                    placeholder="Cảm nhận sau bữa ăn, nguyên liệu thêm..."
                    value={logNotes}
                    onChange={(e) => setLogNotes(e.target.value)}
                  />
                </label>
              </div>

              {logError && (
                <p className="form-error" role="alert">
                  {logError}
                </p>
              )}
              {logFeedback && (
                <p className="form-success" role="status">
                  {logFeedback}
                </p>
              )}

              <div className="logger-form-actions">
                <button
                  type="submit"
                  className="primary-button"
                  disabled={saveMealLogMutation.isPending}
                >
                  <Plus size={16} />
                  {saveMealLogMutation.isPending
                    ? 'Đang lưu...'
                    : editingLogId
                      ? 'Lưu cập nhật bữa ăn'
                      : 'Ghi vào nhật ký'}
                </button>

                {editingLogId && (
                  <button
                    type="button"
                    className="secondary-button"
                    onClick={() => {
                      setEditingLogId(null)
                      setLogTitle('')
                      setLogFoodName('')
                      setLogNotes('')
                    }}
                  >
                    Hủy sửa
                  </button>
                )}
              </div>
            </form>

            {/* MEAL LOG HISTORY */}
            <div className="surface-card logger-history-card">
              <div className="history-card-head">
                <div>
                  <span className="micro-label">LỊCH SỬ BỮA ĂN ĐÃ GHI NHẬN</span>
                  <h3>Danh sách theo khoảng ngày</h3>
                </div>

                <div className="date-range-filters">
                  <label>
                    <span>Từ</span>
                    <input
                      aria-label="Từ ngày"
                      type="date"
                      value={logFromDate}
                      onChange={(e) => setLogFromDate(e.target.value)}
                    />
                  </label>
                  <label>
                    <span>Đến</span>
                    <input
                      aria-label="Đến ngày"
                      type="date"
                      value={logToDate}
                      onChange={(e) => setLogToDate(e.target.value)}
                    />
                  </label>
                </div>
              </div>

              {mealLogsQuery.isLoading ? (
                <p className="status-text" role="status">
                  Đang tải lịch sử bữa ăn...
                </p>
              ) : mealLogsQuery.isError ? (
                <p className="form-error" role="alert">
                  {getApiErrorMessage(mealLogsQuery.error, 'Không thể tải lịch sử bữa ăn.')}
                </p>
              ) : !mealLogsQuery.data || mealLogsQuery.data.length === 0 ? (
                <div className="empty-history">
                  <p>Chưa có bữa ăn nào được ghi nhận trong khoảng thời gian này.</p>
                </div>
              ) : (
                <div className="meal-log-list" data-testid="meal-log-list">
                  {mealLogsQuery.data.map((log) => (
                    <div key={log.id} className="meal-log-item-row" data-testid="meal-log-row">
                      <div className="meal-log-time-col">
                        <strong>{formatShortTime(log.localTime)}</strong>
                        <span>{log.localDate}</span>
                        <small>{SLOT_LABELS[log.mealType] ?? log.mealType}</small>
                      </div>

                      <div className="meal-log-info-col">
                        <strong>{log.title}</strong>
                        <p>
                          {log.totalCaloriesKcal} kcal • Đạm {log.totalProteinGrams}g • Tinh bột{' '}
                          {log.totalCarbsGrams}g • Xơ {log.totalFiberGrams}g
                        </p>
                        {log.notes && <small className="log-note">{log.notes}</small>}
                      </div>

                      <div className="meal-log-actions-col">
                        <button
                          type="button"
                          className="icon-action-btn"
                          aria-label={`Sửa ${log.title}`}
                          onClick={() => handleEditMealLog(log)}
                        >
                          <Edit3 size={15} />
                        </button>
                        <button
                          type="button"
                          className="icon-action-btn danger"
                          aria-label={`Xóa ${log.title}`}
                          disabled={deleteMealLogMutation.isPending}
                          onClick={() => deleteMealLogMutation.mutate(log.id)}
                        >
                          <Trash2 size={15} />
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        </section>

        {/* FMD FASTING MIMICKING DIET & SAFETY GATE SECTION */}
        <FmdWorkspaceSection />
      </main>
    </div>
  )
}
