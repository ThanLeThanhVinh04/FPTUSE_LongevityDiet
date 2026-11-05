import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { BookOpen, ChevronLeft, ChevronRight, Leaf, Search, ShieldCheck, Utensils } from 'lucide-react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../auth/authState'
import { getFoods, getRecipes, getRules } from './catalogApi'

type CatalogTab = 'foods' | 'recipes' | 'rules'

export function CatalogPage() {
  const { user } = useAuth()
  const [tab, setTab] = useState<CatalogTab>('foods')
  const [search, setSearch] = useState('')
  const [foodPage, setFoodPage] = useState(1)
  const [recipePage, setRecipePage] = useState(1)
  const [sortBy, setSortBy] = useState('name')
  const [sortDirection, setSortDirection] = useState('asc')
  const [filter, setFilter] = useState('all')

  const foods = useQuery({
    queryKey: ['catalog', 'foods', foodPage, search, sortBy, sortDirection, filter],
    queryFn: () =>
      getFoods({
        page: foodPage,
        pageSize: 8,
        search: search || undefined,
        isPlantBased: filter === 'plant-based' ? true : undefined,
        isLegume: filter === 'legume' ? true : undefined,
        isWholeGrain: filter === 'whole-grain' ? true : undefined,
        isFish: filter === 'fish' ? true : undefined,
        sortBy,
        sortDirection,
      }),
    enabled: tab === 'foods',
  })

  const recipes = useQuery({
    queryKey: ['catalog', 'recipes', recipePage, search, sortBy, sortDirection, filter],
    queryFn: () =>
      getRecipes({
        page: recipePage,
        pageSize: 8,
        search: search || undefined,
        isPlantForward: filter === 'plant-forward' ? true : undefined,
        hasLegume: filter === 'legume' ? true : undefined,
        hasWholeGrain: filter === 'whole-grain' ? true : undefined,
        hasFish: filter === 'fish' ? true : undefined,
        sortBy: sortBy === 'calories' ? 'name' : sortBy,
        sortDirection,
      }),
    enabled: tab === 'recipes',
  })

  const rules = useQuery({
    queryKey: ['catalog', 'rules'],
    queryFn: () => getRules(false),
    enabled: tab === 'rules',
  })

  const resetSearch = (value: string) => {
    setSearch(value)
    setFoodPage(1)
    setRecipePage(1)
  }

  const changeFilter = (value: string) => {
    setFilter(value)
    setFoodPage(1)
    setRecipePage(1)
  }

  const changeTab = (nextTab: CatalogTab) => {
    setTab(nextTab)
    setSearch('')
    setFilter('all')
    setSortBy('name')
    setSortDirection('asc')
    setFoodPage(1)
    setRecipePage(1)
  }

  return (
    <div className="catalog-page">
      <header className="catalog-topbar">
        <Link className="brand" to="/app">
          <span className="brand-monogram">L</span>
          <span className="brand-copy">
            <strong>Longevity</strong>
            <small>Catalog & rules</small>
          </span>
        </Link>
        <nav className="catalog-nav" aria-label="Catalog navigation">
          <Link to="/app">Hôm nay</Link>
          <Link className="active" to="/catalog">Danh mục</Link>
          {user?.role === 'Admin' && <Link to="/admin/catalog">Quản trị</Link>}
          <Link to="/profile">Hồ sơ</Link>
        </nav>
      </header>

      <main className="catalog-main">
        <section className="catalog-hero">
          <div>
            <span className="eyebrow">DỮ LIỆU DINH DƯỠNG CÓ NGUỒN GỐC</span>
            <h1>Khám phá thực phẩm, công thức và các nguyên tắc đang được áp dụng.</h1>
            <p>
              Nội dung được tải trực tiếp từ API. Dữ liệu đã ngừng hoạt động hoặc rule chưa xuất bản
              không hiển thị cho thành viên.
            </p>
          </div>
          <div className="catalog-safety">
            <ShieldCheck size={22} />
            <div>
              <strong>Minh bạch thay vì “AI tự quyết”</strong>
              <span>Mỗi rule công khai nguồn và phiên bản được xuất bản.</span>
            </div>
          </div>
        </section>

        <section className="catalog-workspace">
          <div className="catalog-toolbar">
            <div className="catalog-tabs" role="tablist">
              <button className={tab === 'foods' ? 'active' : ''} onClick={() => changeTab('foods')}>
                <Leaf size={15} /> Thực phẩm
              </button>
              <button className={tab === 'recipes' ? 'active' : ''} onClick={() => changeTab('recipes')}>
                <Utensils size={15} /> Công thức
              </button>
              <button className={tab === 'rules' ? 'active' : ''} onClick={() => changeTab('rules')}>
                <BookOpen size={15} /> Nguyên tắc
              </button>
            </div>

            {tab !== 'rules' && (
              <div className="catalog-controls">
                <label className="catalog-search">
                  <Search size={15} />
                  <input
                    value={search}
                    onChange={(event) => resetSearch(event.target.value)}
                    placeholder={tab === 'foods' ? 'Tìm thực phẩm...' : 'Tìm công thức...'}
                  />
                </label>
                <select
                  aria-label="Bộ lọc catalog"
                  value={filter}
                  onChange={(event) => changeFilter(event.target.value)}
                >
                  <option value="all">Tất cả</option>
                  {tab === 'foods' && <option value="plant-based">Plant-based</option>}
                  {tab === 'recipes' && <option value="plant-forward">Plant-forward</option>}
                  <option value="legume">Có legume</option>
                  <option value="whole-grain">Có whole grain</option>
                  <option value="fish">Có fish</option>
                </select>
                <select value={sortBy} onChange={(event) => setSortBy(event.target.value)}>
                  <option value="name">Tên</option>
                  <option value="category">Danh mục</option>
                  {tab === 'foods' && <option value="calories">Calories</option>}
                  {tab === 'recipes' && <option value="prepMinutes">Thời gian chuẩn bị</option>}
                </select>
                <select value={sortDirection} onChange={(event) => setSortDirection(event.target.value)}>
                  <option value="asc">Tăng dần</option>
                  <option value="desc">Giảm dần</option>
                </select>
              </div>
            )}
          </div>

          {tab === 'foods' && (
            <CatalogPanel
              loading={foods.isLoading}
              error={foods.isError}
              empty={!foods.isLoading && foods.data?.items.length === 0}
            >
              <div className="catalog-grid">
                {foods.data?.items.map((food) => (
                  <article className="catalog-card" key={food.id}>
                    <div className="catalog-card-head">
                      <span className="catalog-chip">{food.category}</span>
                      <span>{food.calories} kcal / {food.defaultServingAmount}{food.servingUnit}</span>
                    </div>
                    <h2>{food.name}</h2>
                    <p>{food.tags || 'Chưa có tag mô tả.'}</p>
                    <div className="nutrition-row">
                      <span><b>{food.proteinG}g</b> Protein</span>
                      <span><b>{food.fiberG ?? 0}g</b> Fiber</span>
                      <span><b>{food.sugarG ?? 0}g</b> Sugar</span>
                    </div>
                    <div className="flag-row">
                      {food.isPlantBased && <span>Plant-based</span>}
                      {food.isLegume && <span>Legume</span>}
                      {food.isWholeGrain && <span>Whole grain</span>}
                      {food.isFish && <span>Fish</span>}
                    </div>
                  </article>
                ))}
              </div>
              <Pagination
                page={foods.data?.page ?? foodPage}
                totalPages={foods.data?.totalPages ?? 1}
                onChange={setFoodPage}
              />
            </CatalogPanel>
          )}

          {tab === 'recipes' && (
            <CatalogPanel
              loading={recipes.isLoading}
              error={recipes.isError}
              empty={!recipes.isLoading && recipes.data?.items.length === 0}
            >
              <div className="catalog-grid">
                {recipes.data?.items.map((recipe) => (
                  <Link className="catalog-card recipe-card-link" to={`/catalog/recipes/${recipe.id}`} key={recipe.id}>
                    <div className="catalog-card-head">
                      <span className="catalog-chip">{recipe.category}</span>
                      <span>{recipe.prepMinutes} phút</span>
                    </div>
                    <h2>{recipe.name}</h2>
                    <p>{recipe.cuisine || 'International'} · {recipe.servingCount} khẩu phần</p>
                    <div className="nutrition-row">
                      <span><b>{recipe.caloriesPerServing}</b> kcal</span>
                      <span><b>{recipe.hasLegume ? 'Có' : '—'}</b> Đậu</span>
                      <span><b>{recipe.hasWholeGrain ? 'Có' : '—'}</b> Ngũ cốc</span>
                    </div>
                    <span className="catalog-detail-link">Xem thành phần & dị ứng →</span>
                  </Link>
                ))}
              </div>
              <Pagination
                page={recipes.data?.page ?? recipePage}
                totalPages={recipes.data?.totalPages ?? 1}
                onChange={setRecipePage}
              />
            </CatalogPanel>
          )}

          {tab === 'rules' && (
            <CatalogPanel
              loading={rules.isLoading}
              error={rules.isError}
              empty={!rules.isLoading && rules.data?.length === 0}
            >
              <div className="rule-list">
                {rules.data?.map((rule) => (
                  <article className="rule-card" key={rule.id}>
                    <div>
                      <span className="catalog-chip">{rule.category}</span>
                      <h2>{rule.title}</h2>
                      <p>{rule.description}</p>
                    </div>
                    <div className="rule-versions">
                      {rule.versions.map((version) => (
                        <div key={version.id}>
                          <span>v{version.versionNumber} · Published</span>
                          <strong>{version.citation}</strong>
                          <a href={version.sourceUrl} target="_blank" rel="noreferrer">
                            Mở nguồn tham khảo ↗
                          </a>
                        </div>
                      ))}
                    </div>
                  </article>
                ))}
              </div>
            </CatalogPanel>
          )}
        </section>
      </main>
    </div>
  )
}

function CatalogPanel({
  loading,
  error,
  empty,
  children,
}: {
  loading: boolean
  error: boolean
  empty: boolean
  children: React.ReactNode
}) {
  if (loading) {
    return <div className="catalog-state" role="status">Đang tải dữ liệu danh mục...</div>
  }

  if (error) {
    return <div className="catalog-state error">Không thể tải dữ liệu. Hãy thử lại sau.</div>
  }

  if (empty) {
    return <div className="catalog-state">Không có dữ liệu phù hợp với bộ lọc hiện tại.</div>
  }

  return <>{children}</>
}

function Pagination({
  page,
  totalPages,
  onChange,
}: {
  page: number
  totalPages: number
  onChange: (page: number) => void
}) {
  return (
    <div className="catalog-pagination">
      <button disabled={page <= 1} onClick={() => onChange(page - 1)} aria-label="Trang trước">
        <ChevronLeft size={16} />
      </button>
      <span>Trang {page} / {Math.max(totalPages, 1)}</span>
      <button
        disabled={page >= totalPages}
        onClick={() => onChange(page + 1)}
        aria-label="Trang sau"
      >
        <ChevronRight size={16} />
      </button>
    </div>
  )
}
