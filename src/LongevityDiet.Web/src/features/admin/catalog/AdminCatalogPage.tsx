import { useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { BookOpen, Leaf, Plus, Save, Trash2, Utensils } from 'lucide-react'
import { Link } from 'react-router-dom'
import { getApiErrorMessage } from '../../../lib/apiError'
import {
  createFood,
  createRecipe,
  createRule,
  createRuleVersion,
  deactivateFood,
  deactivateRecipe,
  getAdminRecipe,
  getAllergens,
  getFoods,
  getRecipes,
  getRules,
  publishRuleVersion,
  updateFood,
  updateRecipe,
  type Food,
  type RecipeDetail,
  type SaveFoodInput,
  type SaveRecipeInput,
} from '../../catalog/catalogApi'

type AdminTab = 'foods' | 'recipes' | 'rules'

const emptyFood: SaveFoodInput = {
  name: '', category: '', tags: '', cuisine: '',
  defaultServingAmount: 100, servingUnit: 'g',
  calories: 0, proteinG: 0, carbohydrateG: 0, fatG: 0,
  saturatedFatG: 0, sugarG: 0, fiberG: 0,
  isPlantBased: false, isLegume: false, isWholeGrain: false, isFish: false,
}

const emptyRecipe: SaveRecipeInput = {
  name: '',
  description: '',
  category: '',
  cuisine: '',
  tags: '',
  servingCount: 1,
  prepMinutes: 0,
  caloriesPerServing: 0,
  proteinGPerServing: 0,
  carbohydrateGPerServing: 0,
  fatGPerServing: 0,
  saturatedFatGPerServing: 0,
  sugarGPerServing: 0,
  fiberGPerServing: 0,
  isPlantForward: false,
  hasLegume: false,
  hasWholeGrain: false,
  hasFish: false,
  ingredients: [],
  allergenIds: [],
}

export function AdminCatalogPage() {
  const [tab, setTab] = useState<AdminTab>('foods')
  return (
    <div className="catalog-page admin-catalog-page">
      <header className="catalog-topbar">
        <Link className="brand" to="/catalog">
          <span className="brand-monogram">L</span>
          <span className="brand-copy"><strong>Longevity</strong><small>Catalog administration</small></span>
        </Link>
        <nav className="catalog-nav">
          <Link to="/app">Hôm nay</Link>
          <Link to="/catalog">Danh mục</Link>
          <Link className="active" to="/admin/catalog">Quản trị</Link>
          <Link to="/profile">Hồ sơ</Link>
        </nav>
      </header>
      <main className="catalog-main">
        <section className="catalog-hero admin-hero">
          <div>
            <span className="eyebrow">ADMIN · CATALOG & RULES</span>
            <h1>Quản lý dữ liệu thật, có phiên bản và có nguồn.</h1>
            <p>Deactivate giữ lịch sử; rule version đã publish là immutable. Mọi mutation đi qua API → Services → Repository.</p>
          </div>
        </section>
        <section className="catalog-workspace">
          <div className="catalog-tabs admin-tabs">
            <button className={tab === 'foods' ? 'active' : ''} onClick={() => setTab('foods')}><Leaf size={15} /> Foods</button>
            <button className={tab === 'recipes' ? 'active' : ''} onClick={() => setTab('recipes')}><Utensils size={15} /> Recipes</button>
            <button className={tab === 'rules' ? 'active' : ''} onClick={() => setTab('rules')}><BookOpen size={15} /> Rules</button>
          </div>
          {tab === 'foods' && <FoodAdmin />}
          {tab === 'recipes' && <RecipeAdmin />}
          {tab === 'rules' && <RuleAdmin />}
        </section>
      </main>
    </div>
  )
}

function FoodAdmin() {
  const queryClient = useQueryClient()
  const [editingId, setEditingId] = useState<string | null>(null)
  const [form, setForm] = useState<SaveFoodInput>(emptyFood)
  const [message, setMessage] = useState<string | null>(null)
  const foods = useQuery({
    queryKey: ['admin', 'foods'],
    queryFn: () => getFoods({ page: 1, pageSize: 100, sortBy: 'name' }, true),
  })
  const save = useMutation({
    mutationFn: () => editingId ? updateFood(editingId, form) : createFood(form),
    onSuccess: () => {
      setMessage(editingId ? 'Đã cập nhật food.' : 'Đã tạo food.')
      setEditingId(null)
      setForm(emptyFood)
      void queryClient.invalidateQueries({ queryKey: ['admin', 'foods'] })
      void queryClient.invalidateQueries({ queryKey: ['catalog', 'foods'] })
    },
    onError: (error) => setMessage(getApiErrorMessage(error)),
  })
  const deactivate = useMutation({
    mutationFn: deactivateFood,
    onSuccess: () => {
      setMessage('Đã deactivate food; lịch sử được giữ lại.')
      void queryClient.invalidateQueries({ queryKey: ['admin', 'foods'] })
      void queryClient.invalidateQueries({ queryKey: ['catalog', 'foods'] })
    },
    onError: (error) => setMessage(getApiErrorMessage(error)),
  })
  const edit = (food: Food) => {
    setEditingId(food.id)
    setForm({
      name: food.name, category: food.category, tags: food.tags ?? '', cuisine: food.cuisine ?? '',
      defaultServingAmount: food.defaultServingAmount, servingUnit: food.servingUnit,
      calories: food.calories, proteinG: food.proteinG, carbohydrateG: food.carbohydrateG, fatG: food.fatG,
      saturatedFatG: food.saturatedFatG ?? 0, sugarG: food.sugarG ?? 0, fiberG: food.fiberG ?? 0,
      isPlantBased: food.isPlantBased, isLegume: food.isLegume, isWholeGrain: food.isWholeGrain, isFish: food.isFish,
    })
    setMessage(null)
  }

  return (
    <div className="admin-grid">
      <section className="admin-list-panel">
        <PanelHeading title="Foods" note={String(foods.data?.totalItems ?? 0) + ' mục, gồm cả inactive'} />
        {foods.isLoading && <AdminState>Đang tải foods...</AdminState>}
        {foods.isError && <AdminState error>Không thể tải foods.</AdminState>}
        <div className="admin-item-list">
          {foods.data?.items.map((food) => (
            <div className={food.isActive ? 'admin-item' : 'admin-item inactive'} key={food.id}>
              <div><strong>{food.name}</strong><span>{food.category} · {food.calories} kcal · {food.isActive ? 'Active' : 'Inactive'}</span></div>
              <div className="admin-item-actions">
                <button onClick={() => edit(food)}>Sửa</button>
                {food.isActive && <button className="danger" onClick={() => deactivate.mutate(food.id)}><Trash2 size={13} /> Deactivate</button>}
              </div>
            </div>
          ))}
        </div>
      </section>
      <section className="admin-form-panel">
        <PanelHeading title={editingId ? 'Cập nhật food' : 'Tạo food mới'} note="Nutrition theo serving mặc định" />
        <div className="admin-form-grid">
          <TextField label="Tên" value={form.name} onChange={(name) => setForm({ ...form, name })} />
          <TextField label="Danh mục" value={form.category} onChange={(category) => setForm({ ...form, category })} />
          <TextField label="Tags" value={form.tags ?? ''} onChange={(tags) => setForm({ ...form, tags })} />
          <TextField label="Cuisine" value={form.cuisine ?? ''} onChange={(cuisine) => setForm({ ...form, cuisine })} />
          <NumberField label="Serving" value={form.defaultServingAmount} onChange={(defaultServingAmount) => setForm({ ...form, defaultServingAmount })} />
          <TextField label="Đơn vị" value={form.servingUnit} onChange={(servingUnit) => setForm({ ...form, servingUnit })} />
          <NumberField label="Calories" value={form.calories} onChange={(calories) => setForm({ ...form, calories })} />
          <NumberField label="Protein g" value={form.proteinG} onChange={(proteinG) => setForm({ ...form, proteinG })} />
          <NumberField label="Carb g" value={form.carbohydrateG} onChange={(carbohydrateG) => setForm({ ...form, carbohydrateG })} />
          <NumberField label="Fat g" value={form.fatG} onChange={(fatG) => setForm({ ...form, fatG })} />
          <NumberField label="Saturated fat g" value={form.saturatedFatG ?? 0} onChange={(saturatedFatG) => setForm({ ...form, saturatedFatG })} />
          <NumberField label="Sugar g" value={form.sugarG ?? 0} onChange={(sugarG) => setForm({ ...form, sugarG })} />
          <NumberField label="Fiber g" value={form.fiberG ?? 0} onChange={(fiberG) => setForm({ ...form, fiberG })} />
        </div>
        <div className="admin-checks">
          {([
            ['isPlantBased', 'Plant-based'], ['isLegume', 'Legume'],
            ['isWholeGrain', 'Whole grain'], ['isFish', 'Fish'],
          ] as const).map(([key, label]) => (
            <label key={key}><input type="checkbox" checked={form[key]} onChange={(event) => setForm({ ...form, [key]: event.target.checked })} />{label}</label>
          ))}
        </div>
        {message && <p className="admin-message">{message}</p>}
        <div className="admin-form-actions">
          {editingId && <button onClick={() => { setEditingId(null); setForm(emptyFood); setMessage(null) }}>Hủy sửa</button>}
          <button className="primary-admin-action" disabled={save.isPending} onClick={() => save.mutate()}><Save size={14} /> {editingId ? 'Lưu thay đổi' : 'Tạo food'}</button>
        </div>
      </section>
    </div>
  )
}

function RecipeAdmin() {
  const queryClient = useQueryClient()
  const [form, setForm] = useState<SaveRecipeInput>(emptyRecipe)
  const [editingRecipe, setEditingRecipe] = useState<RecipeDetail | null>(null)
  const [message, setMessage] = useState<string | null>(null)

  const recipes = useQuery({
    queryKey: ['admin', 'recipes'],
    queryFn: () => getRecipes({ page: 1, pageSize: 100, sortBy: 'name' }, true),
  })
  const foods = useQuery({
    queryKey: ['admin', 'recipe-food-options'],
    queryFn: () => getFoods({ page: 1, pageSize: 100, sortBy: 'name' }, false),
  })
  const allergens = useQuery({
    queryKey: ['admin', 'recipe-allergens'],
    queryFn: getAllergens,
  })

  const activeFoods = foods.data?.items ?? []

  const save = useMutation({
    mutationFn: () => editingRecipe ? updateRecipe(editingRecipe.id, form) : createRecipe(form),
    onSuccess: () => {
      setForm(emptyRecipe)
      setEditingRecipe(null)
      setMessage(editingRecipe ? 'Đã cập nhật recipe.' : 'Đã tạo recipe.')
      void queryClient.invalidateQueries({ queryKey: ['admin', 'recipes'] })
      void queryClient.invalidateQueries({ queryKey: ['catalog', 'recipes'] })
    },
    onError: (error) => setMessage(getApiErrorMessage(error)),
  })

  const deactivate = useMutation({
    mutationFn: deactivateRecipe,
    onSuccess: () => {
      setMessage('Đã deactivate recipe.')
      void queryClient.invalidateQueries({ queryKey: ['admin', 'recipes'] })
      void queryClient.invalidateQueries({ queryKey: ['catalog', 'recipes'] })
    },
    onError: (error) => setMessage(getApiErrorMessage(error)),
  })

  const beginEdit = async (id: string) => {
    try {
      const detail = await getAdminRecipe(id)
      setEditingRecipe(detail)
      setForm({
        name: detail.name,
        description: detail.description,
        category: detail.category,
        cuisine: detail.cuisine ?? '',
        tags: detail.tags ?? '',
        servingCount: detail.servingCount,
        prepMinutes: detail.prepMinutes,
        caloriesPerServing: detail.caloriesPerServing,
        proteinGPerServing: detail.proteinGPerServing,
        carbohydrateGPerServing: detail.carbohydrateGPerServing,
        fatGPerServing: detail.fatGPerServing,
        saturatedFatGPerServing: detail.saturatedFatGPerServing ?? 0,
        sugarGPerServing: detail.sugarGPerServing ?? 0,
        fiberGPerServing: detail.fiberGPerServing ?? 0,
        isPlantForward: detail.isPlantForward,
        hasLegume: detail.hasLegume,
        hasWholeGrain: detail.hasWholeGrain,
        hasFish: detail.hasFish,
        ingredients: detail.ingredients.map((item) => ({
          foodId: item.foodId,
          amount: item.amount,
          unit: item.unit,
          isOptional: item.isOptional,
        })),
        allergenIds: detail.allergens.map((item) => item.id),
      })
      setMessage(null)
    } catch (error) {
      setMessage(getApiErrorMessage(error))
    }
  }

  const addIngredient = () => {
    const firstUnused = activeFoods.find(
      (food) => !form.ingredients.some((ingredient) => ingredient.foodId === food.id),
    )
    if (!firstUnused) {
      setMessage('Không còn food active nào để thêm vào recipe.')
      return
    }

    setForm({
      ...form,
      ingredients: [
        ...form.ingredients,
        { foodId: firstUnused.id, amount: 100, unit: 'g', isOptional: false },
      ],
    })
  }

  const updateIngredient = (
    index: number,
    patch: Partial<SaveRecipeInput['ingredients'][number]>,
  ) => {
    setForm({
      ...form,
      ingredients: form.ingredients.map((ingredient, currentIndex) =>
        currentIndex === index ? { ...ingredient, ...patch } : ingredient,
      ),
    })
  }

  const removeIngredient = (index: number) => {
    setForm({
      ...form,
      ingredients: form.ingredients.filter((_, currentIndex) => currentIndex !== index),
    })
  }

  const toggleAllergen = (id: string) => {
    setForm({
      ...form,
      allergenIds: form.allergenIds.includes(id)
        ? form.allergenIds.filter((current) => current !== id)
        : [...form.allergenIds, id],
    })
  }

  return (
    <div className="admin-grid">
      <section className="admin-list-panel">
        <PanelHeading title="Recipes" note={String(recipes.data?.totalItems ?? 0) + ' mục'} />
        {recipes.isLoading && <AdminState>Đang tải recipes...</AdminState>}
        {recipes.isError && <AdminState error>Không thể tải recipes.</AdminState>}
        <div className="admin-item-list">
          {recipes.data?.items.map((recipe) => (
            <div className={recipe.isActive ? 'admin-item' : 'admin-item inactive'} key={recipe.id}>
              <div>
                <strong>{recipe.name}</strong>
                <span>{recipe.category} · {recipe.prepMinutes} phút · {recipe.isActive ? 'Active' : 'Inactive'}</span>
              </div>
              <div className="admin-item-actions">
                <Link to={'/catalog/recipes/' + recipe.id}>Xem</Link>
                <button onClick={() => void beginEdit(recipe.id)}>Sửa</button>
                {recipe.isActive && (
                  <button className="danger" onClick={() => deactivate.mutate(recipe.id)}>
                    <Trash2 size={13} /> Deactivate
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      </section>

      <section className="admin-form-panel">
        <PanelHeading
          title={editingRecipe ? 'Cập nhật recipe' : 'Tạo recipe'}
          note="Nhập đầy đủ dữ liệu catalog; không dùng nutrition hoặc flags mặc định."
        />
        <div className="admin-form-grid">
          <TextField label="Tên" value={form.name} onChange={(name) => setForm({ ...form, name })} />
          <TextField label="Danh mục" value={form.category} onChange={(category) => setForm({ ...form, category })} />
          <TextField label="Cuisine" value={form.cuisine ?? ''} onChange={(cuisine) => setForm({ ...form, cuisine })} />
          <TextField label="Tags" value={form.tags ?? ''} onChange={(tags) => setForm({ ...form, tags })} />
          <label className="admin-field span-2">
            <span>Mô tả</span>
            <textarea value={form.description} onChange={(event) => setForm({ ...form, description: event.target.value })} />
          </label>
          <NumberField label="Khẩu phần" value={form.servingCount} onChange={(servingCount) => setForm({ ...form, servingCount })} />
          <NumberField label="Prep minutes" value={form.prepMinutes} onChange={(prepMinutes) => setForm({ ...form, prepMinutes })} />
          <NumberField label="Calories / serving" value={form.caloriesPerServing} onChange={(caloriesPerServing) => setForm({ ...form, caloriesPerServing })} />
          <NumberField label="Protein g / serving" value={form.proteinGPerServing} onChange={(proteinGPerServing) => setForm({ ...form, proteinGPerServing })} />
          <NumberField label="Carb g / serving" value={form.carbohydrateGPerServing} onChange={(carbohydrateGPerServing) => setForm({ ...form, carbohydrateGPerServing })} />
          <NumberField label="Fat g / serving" value={form.fatGPerServing} onChange={(fatGPerServing) => setForm({ ...form, fatGPerServing })} />
          <NumberField label="Saturated fat g" value={form.saturatedFatGPerServing ?? 0} onChange={(saturatedFatGPerServing) => setForm({ ...form, saturatedFatGPerServing })} />
          <NumberField label="Sugar g" value={form.sugarGPerServing ?? 0} onChange={(sugarGPerServing) => setForm({ ...form, sugarGPerServing })} />
          <NumberField label="Fiber g" value={form.fiberGPerServing ?? 0} onChange={(fiberGPerServing) => setForm({ ...form, fiberGPerServing })} />
        </div>

        <div className="admin-checks">
          {([
            ['isPlantForward', 'Plant-forward'],
            ['hasLegume', 'Có legume'],
            ['hasWholeGrain', 'Có whole grain'],
            ['hasFish', 'Có fish'],
          ] as const).map(([key, label]) => (
            <label key={key}>
              <input
                type="checkbox"
                checked={form[key]}
                onChange={(event) => setForm({ ...form, [key]: event.target.checked })}
              />
              {label}
            </label>
          ))}
        </div>

        <div className="admin-form-divider" />
        <PanelHeading title="Ingredients" note="Có thể thêm nhiều food active; mỗi food chỉ xuất hiện một lần." />
        <div className="ingredient-admin-list">
          {form.ingredients.map((ingredient, index) => (
            <div className="ingredient-admin-row" key={String(index) + ingredient.foodId}>
              <label className="admin-field">
                <span>Food</span>
                <select
                  value={ingredient.foodId}
                  onChange={(event) => updateIngredient(index, { foodId: event.target.value })}
                >
                  {activeFoods.map((food) => (
                    <option
                      value={food.id}
                      key={food.id}
                      disabled={form.ingredients.some(
                        (current, currentIndex) =>
                          currentIndex !== index && current.foodId === food.id,
                      )}
                    >
                      {food.name}
                    </option>
                  ))}
                </select>
              </label>
              <NumberField label="Amount" value={ingredient.amount} onChange={(amount) => updateIngredient(index, { amount })} />
              <TextField label="Unit" value={ingredient.unit} onChange={(unit) => updateIngredient(index, { unit })} />
              <label className="ingredient-optional">
                <input
                  type="checkbox"
                  checked={ingredient.isOptional}
                  onChange={(event) => updateIngredient(index, { isOptional: event.target.checked })}
                />
                Optional
              </label>
              <button className="ingredient-remove" onClick={() => removeIngredient(index)} type="button">
                <Trash2 size={13} /> Xóa
              </button>
            </div>
          ))}
        </div>
        <button className="inline-admin-action" type="button" onClick={addIngredient} disabled={activeFoods.length === 0}>
          <Plus size={13} /> Thêm ingredient
        </button>

        <div className="admin-form-divider" />
        <PanelHeading title="Allergens" note="Chọn từ allergen catalog active." />
        {allergens.isLoading && <AdminState>Đang tải allergens...</AdminState>}
        {allergens.isError && <AdminState error>Không thể tải allergens.</AdminState>}
        <div className="admin-checks">
          {allergens.data?.map((allergen) => (
            <label key={allergen.id}>
              <input
                type="checkbox"
                checked={form.allergenIds.includes(allergen.id)}
                onChange={() => toggleAllergen(allergen.id)}
              />
              {allergen.code} · {allergen.name}
            </label>
          ))}
        </div>

        {message && <p className="admin-message">{message}</p>}
        <div className="admin-form-actions">
          {editingRecipe && (
            <button
              onClick={() => {
                setEditingRecipe(null)
                setForm(emptyRecipe)
                setMessage(null)
              }}
            >
              Hủy sửa
            </button>
          )}
          <button
            className="primary-admin-action"
            disabled={
              save.isPending ||
              !form.name.trim() ||
              !form.description.trim() ||
              !form.category.trim() ||
              form.servingCount < 1 ||
              form.ingredients.length === 0
            }
            onClick={() => save.mutate()}
          >
            <Save size={14} /> {editingRecipe ? 'Lưu recipe' : 'Tạo recipe'}
          </button>
        </div>
      </section>
    </div>
  )
}

function RuleAdmin() {
  const queryClient = useQueryClient()
  const [ruleKey, setRuleKey] = useState('')
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [category, setCategory] = useState('Nutrition')
  const [selectedRuleId, setSelectedRuleId] = useState('')
  const [sourceUrl, setSourceUrl] = useState('')
  const [citation, setCitation] = useState('')
  const [parametersJson, setParametersJson] = useState('{}')
  const [isMedicalSensitive, setIsMedicalSensitive] = useState(false)
  const [sourceType, setSourceType] = useState(2)
  const [evidenceLevel, setEvidenceLevel] = useState(2)
  const [message, setMessage] = useState<string | null>(null)
  const rules = useQuery({ queryKey: ['admin', 'rules'], queryFn: () => getRules(true) })
  const selectedRule = useMemo(() => rules.data?.find((rule) => rule.id === selectedRuleId) ?? rules.data?.[0], [rules.data, selectedRuleId])
  const addRule = useMutation({
    mutationFn: () => createRule({ ruleKey, title, description, category, isMedicalSensitive }),
    onSuccess: (rule) => {
      setRuleKey(''); setTitle(''); setDescription(''); setIsMedicalSensitive(false); setSelectedRuleId(rule.id)
      setMessage('Đã tạo rule. Hãy tạo version có provenance trước khi publish.')
      void queryClient.invalidateQueries({ queryKey: ['admin', 'rules'] })
    },
    onError: (error) => setMessage(getApiErrorMessage(error)),
  })
  const addVersion = useMutation({
    mutationFn: () => createRuleVersion(selectedRule!.id, {
      parametersJson, sourceUrl, sourceType, citation, evidenceLevel, effectiveAt: null,
    }),
    onSuccess: () => {
      setMessage('Đã tạo draft version.')
      void queryClient.invalidateQueries({ queryKey: ['admin', 'rules'] })
    },
    onError: (error) => setMessage(getApiErrorMessage(error)),
  })
  const publish = useMutation({
    mutationFn: publishRuleVersion,
    onSuccess: () => {
      setMessage('Đã publish version; version này từ giờ là immutable.')
      void queryClient.invalidateQueries({ queryKey: ['admin', 'rules'] })
      void queryClient.invalidateQueries({ queryKey: ['catalog', 'rules'] })
    },
    onError: (error) => setMessage(getApiErrorMessage(error)),
  })

  return (
    <div className="admin-grid rules-admin-grid">
      <section className="admin-list-panel">
        <PanelHeading title="Rules & versions" note={String(rules.data?.length ?? 0) + ' rules'} />
        {rules.isLoading && <AdminState>Đang tải rules...</AdminState>}
        <div className="admin-item-list">
          {rules.data?.map((rule) => (
            <button className={'admin-rule-select ' + (selectedRule?.id === rule.id ? 'selected' : '')} onClick={() => setSelectedRuleId(rule.id)} key={rule.id}>
              <strong>{rule.title}</strong><span>{rule.ruleKey} · {rule.versions.length} versions</span>
            </button>
          ))}
        </div>
        {selectedRule && (
          <div className="rule-version-admin-list">
            <h3>{selectedRule.title}</h3>
            {selectedRule.versions.map((version) => (
              <div key={version.id}>
                <span>v{version.versionNumber} · {version.state === 2 ? 'Published' : 'Draft'}</span>
                <strong>{version.citation || 'Chưa có citation'}</strong>
                {version.state !== 2 && <button onClick={() => publish.mutate(version.id)}>Publish</button>}
              </div>
            ))}
          </div>
        )}
      </section>
      <section className="admin-form-panel">
        <PanelHeading title="Tạo rule" note="Stable RuleKey, mô tả và category bắt buộc" />
        <div className="admin-form-grid">
          <TextField label="RuleKey" value={ruleKey} onChange={setRuleKey} />
          <TextField label="Category" value={category} onChange={setCategory} />
          <TextField label="Tiêu đề" value={title} onChange={setTitle} className="span-2" />
          <label className="admin-field span-2"><span>Mô tả</span><textarea value={description} onChange={(event) => setDescription(event.target.value)} /></label>
          <label className="ingredient-optional span-2">
            <input type="checkbox" checked={isMedicalSensitive} onChange={(event) => setIsMedicalSensitive(event.target.checked)} />
            Rule này nhạy cảm về y khoa
          </label>
        </div>
        <div className="admin-form-actions"><button className="primary-admin-action" disabled={addRule.isPending || !ruleKey.trim() || !title.trim() || !description.trim()} onClick={() => addRule.mutate()}><Plus size={14} /> Tạo rule</button></div>
        <div className="admin-form-divider" />
        <PanelHeading title="Tạo draft version" note={selectedRule ? 'Cho ' + selectedRule.ruleKey : 'Chọn một rule trước'} />
        <div className="admin-form-grid">
          <TextField label="Source URL" value={sourceUrl} onChange={setSourceUrl} className="span-2" />
          <TextField label="Citation" value={citation} onChange={setCitation} className="span-2" />
          <label className="admin-field">
            <span>Source type</span>
            <select value={sourceType} onChange={(event) => setSourceType(Number(event.target.value))}>
              <option value={1}>Book</option>
              <option value={2}>Official guidance</option>
              <option value={3}>Peer-reviewed</option>
              <option value={4}>Team-defined</option>
            </select>
          </label>
          <label className="admin-field">
            <span>Evidence level</span>
            <select value={evidenceLevel} onChange={(event) => setEvidenceLevel(Number(event.target.value))}>
              <option value={1}>Low</option>
              <option value={2}>Moderate</option>
              <option value={3}>High</option>
            </select>
          </label>
          <label className="admin-field span-2"><span>Parameters JSON</span><textarea value={parametersJson} onChange={(event) => setParametersJson(event.target.value)} /></label>
        </div>
        {message && <p className="admin-message">{message}</p>}
        <div className="admin-form-actions"><button className="primary-admin-action" disabled={addVersion.isPending || !selectedRule || !sourceUrl.trim() || !citation.trim()} onClick={() => addVersion.mutate()}><Plus size={14} /> Tạo draft version</button></div>
      </section>
    </div>
  )
}

function PanelHeading({ title, note }: { title: string; note: string }) {
  return <div className="admin-panel-heading"><div><span className="micro-label">CATALOG ADMIN</span><h2>{title}</h2></div><small>{note}</small></div>
}

function AdminState({ children, error = false }: { children: ReactNode; error?: boolean }) {
  return <div className={'catalog-state ' + (error ? 'error' : '')}>{children}</div>
}

function TextField({ label, value, onChange, className = '' }: { label: string; value: string; onChange: (value: string) => void; className?: string }) {
  return <label className={'admin-field ' + className}><span>{label}</span><input value={value} onChange={(event) => onChange(event.target.value)} /></label>
}

function NumberField({ label, value, onChange }: { label: string; value: number; onChange: (value: number) => void }) {
  return <label className="admin-field"><span>{label}</span><input type="number" min="0" step="0.1" value={value} onChange={(event) => onChange(Number(event.target.value))} /></label>
}
