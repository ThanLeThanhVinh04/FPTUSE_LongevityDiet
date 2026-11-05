import { useQuery } from '@tanstack/react-query'
import { AlertTriangle, ArrowLeft, Clock3, Leaf, Utensils } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'
import { getRecipe } from './catalogApi'

export function RecipeDetailPage() {
  const { id } = useParams<{ id: string }>()
  const recipe = useQuery({
    queryKey: ['catalog', 'recipe', id],
    queryFn: () => getRecipe(id!),
    enabled: Boolean(id),
  })

  if (recipe.isLoading) {
    return <div className="catalog-state full-page" role="status">Đang tải chi tiết công thức...</div>
  }

  if (recipe.isError || !recipe.data) {
    return (
      <div className="catalog-state full-page error">
        Không thể tìm thấy công thức này.
        <Link to="/catalog">Quay lại danh mục</Link>
      </div>
    )
  }

  const item = recipe.data

  return (
    <div className="recipe-detail-page">
      <header className="catalog-topbar">
        <Link className="brand" to="/catalog">
          <span className="brand-monogram">L</span>
          <span className="brand-copy"><strong>Longevity</strong><small>Recipe detail</small></span>
        </Link>
        <Link className="profile-back" to="/catalog"><ArrowLeft size={15} /> Danh mục</Link>
      </header>

      <main className="recipe-detail-main">
        <section className="recipe-detail-hero">
          <div>
            <span className="catalog-chip">{item.category}</span>
            <h1>{item.name}</h1>
            <p>{item.description}</p>
            <div className="recipe-meta">
              <span><Clock3 size={15} /> {item.prepMinutes} phút</span>
              <span><Utensils size={15} /> {item.servingCount} khẩu phần</span>
              {item.isPlantForward && <span><Leaf size={15} /> Plant-forward</span>}
            </div>
          </div>
          <div className="recipe-energy">
            <span>MỖI KHẨU PHẦN</span>
            <strong>{item.caloriesPerServing}</strong>
            <small>kcal</small>
          </div>
        </section>

        <section className="recipe-detail-grid">
          <article className="recipe-panel">
            <span className="micro-label">THÀNH PHẦN</span>
            <h2>Nguyên liệu</h2>
            <div className="ingredient-list">
              {item.ingredients.map((ingredient) => (
                <div key={ingredient.foodId}>
                  <strong>{ingredient.foodName}</strong>
                  <span>{ingredient.amount} {ingredient.unit}{ingredient.isOptional ? ' · tùy chọn' : ''}</span>
                </div>
              ))}
            </div>
          </article>

          <article className="recipe-panel">
            <span className="micro-label">DINH DƯỠNG</span>
            <h2>Thông tin mỗi khẩu phần</h2>
            <div className="macro-grid">
              <div><strong>{item.proteinGPerServing}g</strong><span>Protein</span></div>
              <div><strong>{item.carbohydrateGPerServing}g</strong><span>Carb</span></div>
              <div><strong>{item.fatGPerServing}g</strong><span>Fat</span></div>
              <div><strong>{item.fiberGPerServing ?? 0}g</strong><span>Fiber</span></div>
              <div><strong>{item.sugarGPerServing ?? 0}g</strong><span>Sugar</span></div>
              <div><strong>{item.saturatedFatGPerServing ?? 0}g</strong><span>Saturated fat</span></div>
            </div>
          </article>

          <article className="recipe-panel allergen-panel">
            <span className="micro-label">AN TOÀN</span>
            <h2>Dị ứng đã khai báo</h2>
            {item.allergens.length > 0 ? (
              <div className="allergen-list">
                {item.allergens.map((allergen) => (
                  <span key={allergen.id}><AlertTriangle size={14} /> {allergen.name}</span>
                ))}
              </div>
            ) : (
              <p>Không có allergen nào được gắn cho công thức này trong catalog.</p>
            )}
            <small>
              Thông tin catalog hỗ trợ lựa chọn thực phẩm nhưng không thay thế hướng dẫn y khoa cá nhân.
            </small>
          </article>
        </section>
      </main>
    </div>
  )
}
