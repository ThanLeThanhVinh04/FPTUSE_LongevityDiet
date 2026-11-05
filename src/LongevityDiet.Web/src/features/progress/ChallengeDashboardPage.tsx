import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCircle2, Flame, LineChart, Trophy } from 'lucide-react'
import { Link } from 'react-router-dom'
import { api } from '../../lib/api'
import { getApiErrorMessage } from '../../lib/apiError'

type Template = { id: string; title: string; durationDays: number }
type Day = { id: string; dayNumber: number; localDate: string; title: string; isCompleted: boolean }
type Challenge = {
  id: string
  title: string
  status: string
  completedDays: number
  currentStreak: number
  progressPercent: number
  startDate: string
  endDate: string
  days: Day[]
}
type Dashboard = {
  timeZone: string
  loggedDays: number
  loggingConsistencyPercent: number
  challengeCompletedDays: number
  challengeCurrentStreak: number
  latestCaloriesKcal: number | null
  latestEatingWindowWithinTarget: boolean | null
  activityAvailability: string
  ldasAvailability: string
  trend: { localDate: string; loggedMealCount: number; totalCaloriesKcal: number; challengeDayCompleted: boolean }[]
}

export function ChallengeDashboardPage() {
  const client = useQueryClient()
  const templates = useQuery({
    queryKey: ['challenge-templates'],
    queryFn: async () => (await api.get<Template[]>('/challenges/templates')).data,
  })
  const current = useQuery({
    queryKey: ['challenge-current'],
    queryFn: async () => {
      try {
        return (await api.get<Challenge>('/challenges/current')).data
      } catch (error: unknown) {
        if (typeof error === 'object' && error !== null && 'response' in error &&
          (error as { response?: { status?: number } }).response?.status === 404) return null
        throw error
      }
    },
  })
  const dashboard = useQuery({
    queryKey: ['progress-dashboard'],
    queryFn: async () => (await api.get<Dashboard>('/dashboard/summary')).data,
  })
  const start = useMutation({
    mutationFn: async (templateId: string) => (await api.post('/challenges', { templateId, startDate: null })).data,
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['challenge-current'] })
      void client.invalidateQueries({ queryKey: ['progress-dashboard'] })
    },
  })
  const toggle = useMutation({
    mutationFn: async (day: Day) => {
      const challenge = current.data
      if (!challenge) return
      await api.put('/challenges/' + challenge.id + '/days/' + day.dayNumber, { completed: !day.isCompleted })
    },
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['challenge-current'] })
      void client.invalidateQueries({ queryKey: ['progress-dashboard'] })
    },
  })

  const challenge = current.data
  const summary = dashboard.data
  const error = current.error ?? dashboard.error

  return (
    <div className="site-shell">
      <header className="topbar">
        <Link className="brand" to="/"><span className="brand-monogram">L</span><span className="brand-copy"><strong>Longevity</strong><small>14-day progress</small></span></Link>
        <nav className="nav-links"><Link to="/planner">Kế hoạch</Link><Link className="active" to="/progress">Thử thách & tiến độ</Link><Link to="/catalog">Danh mục</Link></nav>
      </header>
      <main className="workspace-main">
        <section className="workspace-section">
          <div className="section-heading"><div><span className="eyebrow">WEEK 2 · TASK 2</span><h1>Thử thách 14 ngày và bảng tiến độ.</h1></div></div>
          {current.isLoading || dashboard.isLoading ? <div className="surface-card status-banner">Đang tải...</div> : null}
          {error ? <div className="surface-card status-banner error-banner">{getApiErrorMessage(error, 'Không thể tải tiến độ.')}</div> : null}
          <article className="surface-card window-main-card">
            <div className="card-heading"><span className="icon-box"><Trophy size={18} /></span><div><span className="micro-label">THỬ THÁCH HIỆN TẠI</span><h2>{challenge?.title ?? 'Chưa bắt đầu'}</h2></div></div>
            {challenge ? (
              <>
                <div className="window-metrics-row">
                  <div className="window-metric-box"><span>Tiến độ</span><strong>{challenge.progressPercent}%</strong><small>{challenge.completedDays}/14 ngày</small></div>
                  <div className="window-metric-box"><Flame size={16} /><span>Streak</span><strong>{challenge.currentStreak}</strong><small>ngày liên tiếp</small></div>
                  <div className="window-metric-box"><span>Trạng thái</span><strong>{challenge.status}</strong><small>{challenge.startDate} → {challenge.endDate}</small></div>
                </div>
                <div className="planner-days-grid">
                  {challenge.days.map((day) => <button key={day.id} type="button" className={day.isCompleted ? 'surface-card status-ok' : 'surface-card'} onClick={() => toggle.mutate(day)} disabled={toggle.isPending}><CheckCircle2 size={16} /><strong>Ngày {day.dayNumber}</strong><span>{day.title}</span><small>{day.localDate}</small></button>)}
                </div>
              </>
            ) : (
              <div>{templates.data?.map((template) => <button key={template.id} className="primary-button" type="button" onClick={() => start.mutate(template.id)} disabled={start.isPending}>Bắt đầu {template.title} ({template.durationDays} ngày)</button>)}</div>
            )}
          </article>
        </section>
        <section className="workspace-section">
          <div className="section-heading"><div><span className="eyebrow">DASHBOARD</span><h2>Tổng hợp dữ liệu persist thật.</h2></div></div>
          {summary ? (
            <>
              <div className="window-metrics-row">
                <div className="window-metric-box"><LineChart size={16} /><span>Nhất quán ghi nhận</span><strong>{summary.loggingConsistencyPercent}%</strong><small>{summary.loggedDays} ngày có log</small></div>
                <div className="window-metric-box"><Flame size={16} /><span>Challenge streak</span><strong>{summary.challengeCurrentStreak}</strong><small>{summary.challengeCompletedDays} ngày hoàn thành</small></div>
                <div className="window-metric-box"><span>Kcal gần nhất</span><strong>{summary.latestCaloriesKcal ?? '--'}</strong><small>{summary.timeZone}</small></div>
                <div className="window-metric-box"><span>Khung giờ ăn</span><strong>{summary.latestEatingWindowWithinTarget == null ? '--' : summary.latestEatingWindowWithinTarget ? 'Đạt' : 'Chưa đạt'}</strong><small>Không suy đoán khi thiếu dữ liệu</small></div>
              </div>
              <article className="surface-card">
                <div className="planner-days-grid">{summary.trend.map((point) => <div className="surface-card" key={point.localDate}><strong>{point.localDate}</strong><span>{point.loggedMealCount} bữa · {point.totalCaloriesKcal} kcal</span><small>Challenge: {point.challengeDayCompleted ? '✓' : '—'}</small></div>)}</div>
                <p>Activity: <strong>{summary.activityAvailability}</strong> · LDAS: <strong>{summary.ldasAvailability}</strong>. Khi source chưa tích hợp, dashboard ghi unavailable thay vì tạo số giả.</p>
              </article>
            </>
          ) : null}
        </section>
      </main>
    </div>
  )
}
