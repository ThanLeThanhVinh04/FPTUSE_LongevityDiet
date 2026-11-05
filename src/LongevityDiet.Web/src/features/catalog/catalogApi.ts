import { api } from '../../lib/api'

export type PagedResult<T> = {
  items: T[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export type Food = {
  id: string
  name: string
  category: string
  tags?: string | null
  cuisine?: string | null
  defaultServingAmount: number
  servingUnit: string
  calories: number
  proteinG: number
  carbohydrateG: number
  fatG: number
  saturatedFatG?: number | null
  sugarG?: number | null
  fiberG?: number | null
  isPlantBased: boolean
  isLegume: boolean
  isWholeGrain: boolean
  isFish: boolean
  isActive: boolean
}

export type RecipeSummary = {
  id: string
  name: string
  category: string
  cuisine?: string | null
  tags?: string | null
  servingCount: number
  prepMinutes: number
  caloriesPerServing: number
  isPlantForward: boolean
  hasLegume: boolean
  hasWholeGrain: boolean
  hasFish: boolean
  isActive: boolean
}

export type Allergen = {
  id: string
  code: string
  name: string
}

export type RecipeDetail = RecipeSummary & {
  description: string
  proteinGPerServing: number
  carbohydrateGPerServing: number
  fatGPerServing: number
  saturatedFatGPerServing?: number | null
  sugarGPerServing?: number | null
  fiberGPerServing?: number | null
  ingredients: {
    foodId: string
    foodName: string
    amount: number
    unit: string
    isOptional: boolean
  }[]
  allergens: Allergen[]
}

export type RuleVersion = {
  id: string
  dietRuleId: string
  versionNumber: number
  parametersJson: string
  sourceUrl: string
  sourceType: number
  citation: string
  evidenceLevel: number
  state: number
  effectiveAt?: string | null
  publishedAt?: string | null
}

export type DietRule = {
  id: string
  ruleKey: string
  title: string
  description: string
  category: string
  isMedicalSensitive: boolean
  isActive: boolean
  versions: RuleVersion[]
}

export type FoodQuery = {
  page?: number
  pageSize?: number
  search?: string
  category?: string
  isPlantBased?: boolean
  isLegume?: boolean
  isWholeGrain?: boolean
  isFish?: boolean
  sortBy?: string
  sortDirection?: string
}

export type RecipeQuery = {
  page?: number
  pageSize?: number
  search?: string
  category?: string
  cuisine?: string
  isPlantForward?: boolean
  hasLegume?: boolean
  hasWholeGrain?: boolean
  hasFish?: boolean
  sortBy?: string
  sortDirection?: string
}

export type SaveFoodInput = Omit<Food, 'id' | 'isActive'>

export type SaveRecipeInput = {
  name: string
  description: string
  category: string
  cuisine?: string | null
  tags?: string | null
  servingCount: number
  prepMinutes: number
  caloriesPerServing: number
  proteinGPerServing: number
  carbohydrateGPerServing: number
  fatGPerServing: number
  saturatedFatGPerServing?: number | null
  sugarGPerServing?: number | null
  fiberGPerServing?: number | null
  isPlantForward: boolean
  hasLegume: boolean
  hasWholeGrain: boolean
  hasFish: boolean
  ingredients: { foodId: string; amount: number; unit: string; isOptional: boolean }[]
  allergenIds: string[]
}

export async function getFoods(query: FoodQuery = {}, admin = false) {
  const response = await api.get<PagedResult<Food>>(admin ? '/admin/foods' : '/foods', { params: query })
  return response.data
}

export async function getFood(id: string) {
  const response = await api.get<Food>(`/foods/${id}`)
  return response.data
}

export async function getRecipes(query: RecipeQuery = {}, admin = false) {
  const response = await api.get<PagedResult<RecipeSummary>>(admin ? '/admin/recipes' : '/recipes', {
    params: query,
  })
  return response.data
}

export async function getRecipe(id: string) {
  const response = await api.get<RecipeDetail>(`/recipes/${id}`)
  return response.data
}

export async function getAdminRecipe(id: string) {
  const response = await api.get<RecipeDetail>(`/admin/recipes/${id}`)
  return response.data
}

export async function getAllergens() {
  const response = await api.get<Allergen[]>('/allergens')
  return response.data
}

export async function getRules(admin = false) {
  const response = await api.get<DietRule[]>(admin ? '/admin/diet-rules' : '/diet-rules')
  return response.data
}

export async function createFood(input: SaveFoodInput) {
  const response = await api.post<Food>('/admin/foods', input)
  return response.data
}

export async function updateFood(id: string, input: SaveFoodInput) {
  const response = await api.put<Food>(`/admin/foods/${id}`, input)
  return response.data
}

export async function deactivateFood(id: string) {
  await api.delete(`/admin/foods/${id}`)
}

export async function createRecipe(input: SaveRecipeInput) {
  const response = await api.post<RecipeDetail>('/admin/recipes', input)
  return response.data
}

export async function updateRecipe(id: string, input: SaveRecipeInput) {
  const response = await api.put<RecipeDetail>(`/admin/recipes/${id}`, input)
  return response.data
}

export async function deactivateRecipe(id: string) {
  await api.delete(`/admin/recipes/${id}`)
}

export async function createRule(input: {
  ruleKey: string
  title: string
  description: string
  category: string
  isMedicalSensitive: boolean
}) {
  const response = await api.post<DietRule>('/admin/diet-rules', input)
  return response.data
}

export async function createRuleVersion(ruleId: string, input: {
  parametersJson: string
  sourceUrl: string
  sourceType: number
  citation: string
  evidenceLevel: number
  effectiveAt?: string | null
}) {
  const response = await api.post<RuleVersion>(`/admin/diet-rules/${ruleId}/versions`, input)
  return response.data
}

export async function publishRuleVersion(versionId: string) {
  const response = await api.post<RuleVersion>(`/admin/rule-versions/${versionId}/publication`)
  return response.data
}
