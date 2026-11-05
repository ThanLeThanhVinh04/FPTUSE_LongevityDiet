import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { FileText, RefreshCw } from 'lucide-react'
import { Link } from 'react-router-dom'
import { api } from '../../lib/api'
import { getApiErrorMessage } from '../../lib/apiError'

type Metric = { metricKey: string; numericValue: number | null; textValue: string | null; unit: string }
type Report = {
  id: string
  weekStart: string
  weekEnd: string
  reportVersion: number
  status: string
  timeZone: string
  summary: string
  generatedAt: string
  metrics: Metric[]
}

function metricValue(metric: Metric) {
  if (metric.numericValue !== null) return metric.numericValue + (metric.unit ? ' ' + metric.unit : '')
  return metric.textValue ?? '--'
}

export function WeeklyReportsPage() {
  const client = useQueryClient()
  const reports = useQuery({
    queryKey: ['weekly-reports'],
    queryFn: async () => (await api.get<Report[]>('/weekly-reports')).data,
  })
  const current = useQuery({
    queryKey: ['weekly-report-current'],
    queryFn: async () => {
      try {
        return (await api.get<Report>('/weekly-reports/current')).data
      } catch (error: unknown) {
        if (typeof error === 'object' && error !== null && 'response' in error &&
          (error as { response?: { status?: number } }).response?.status === 404) return null
        throw error
      }
    },
  })
  const queue = useMutation({
    mutationFn: async () => (await api.post('/weekly-reports/current/jobs')).data,
    onSuccess: () => {
      void client.invalidateQueries({ queryKey: ['weekly-report-current'] })
      void client.invalidateQueries({ queryKey: ['weekly-reports'] })
    },
  })

  const report = current.data

  return (
    <div className="site-shell">
      <header className="topbar">
        <Link className="brand" to="/"><span className="brand-monogram">L</span><span className="brand-copy"><strong>Longevity</strong><small>Weekly progress report</small></span></Link>
        <nav className="nav-links"><Link to="/planner">Kế hoạch</Link><Link to="/progress">Thử thách</Link><Link className="active" to="/reports">Báo cáo tuần</Link></nav>
      </header>
      <main className="workspace-main">
        <section className="workspace-section">
          <div className="workspace-header-row">
            <div><span className="eyebrow">WEEK 3 · TASK 2</span><h1>Báo cáo tiến độ theo tuần.</h1><p>Report được persist theo User + Week + Version và không tự ghi đè lịch sử.</p></div>
            <button className="secondary-button" type="button" onClick={() => { void current.refetch(); void reports.refetch() }}><RefreshCw size={16} /> Làm mới</button>
          </div>

          {current.isLoading ? <div className="surface-card status-banner">Đang tải báo cáo...</div> : null}
          {current.isError ? <div className="surface-card status-banner error-banner">{getApiErrorMessage(current.error, 'Không thể tải báo cáo.')}</div> : null}

          {report ? (
            <article className="surface-card window-main-card">
              <div className="card-heading"><span className="icon-box"><FileText size={18} /></span><div><span className="micro-label">BÁO CÁO HIỆN TẠI · V{report.reportVersion}</span><h2>{report.weekStart} → {report.weekEnd}</h2></div></div>
              <p>{report.summary}</p>
              <div className="window-metrics-row">
                {report.metrics.filter((metric) => ['logging_consistency', 'challenge_completed_days', 'challenge_streak', 'eating_window_target_days'].includes(metric.metricKey)).map((metric) => (
                  <div className="window-metric-box" key={metric.metricKey}><span>{metric.metricKey.replaceAll('_', ' ')}</span><strong>{metricValue(metric)}</strong></div>
                ))}
              </div>
              <p>Generated: {report.generatedAt} · Timezone: {report.timeZone}</p>
            </article>
          ) : (
            <article className="surface-card">
              <h2>Báo cáo tuần này chưa được Worker tạo.</h2>
              <p>Worker tự schedule theo timezone. Bạn cũng có thể gửi yêu cầu vào Redis Stream để xử lý bất đồng bộ.</p>
              <button className="primary-button" type="button" onClick={() => queue.mutate()} disabled={queue.isPending}>Yêu cầu tạo báo cáo</button>
              {queue.isSuccess ? <p>Đã đưa yêu cầu vào hàng đợi. Làm mới sau khi Worker xử lý.</p> : null}
              {queue.isError ? <p className="error-text">{getApiErrorMessage(queue.error, 'Không thể gửi yêu cầu.')}</p> : null}
            </article>
          )}
        </section>

        <section className="workspace-section">
          <div className="section-heading"><div><span className="eyebrow">LỊCH SỬ</span><h2>Các báo cáo đã persist.</h2></div></div>
          <div className="planner-days-grid">
            {reports.data?.map((item) => <article className="surface-card" key={item.id}><strong>{item.weekStart} → {item.weekEnd}</strong><span>{item.status} · v{item.reportVersion}</span><small>{item.summary}</small></article>)}
          </div>
        </section>
      </main>
    </div>
  )
}
