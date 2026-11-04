import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Activity, ArrowLeft, CalendarDays, Save, Trash2 } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../../lib/api'
import { getApiErrorMessage } from '../../lib/apiError'
import '../tracking/tracking.css'

type ActivityLog = {
  id: string
  kind: number
  durationMinutes: number
  startedAtUtc: string
  note: string | null
}
type Page<T> = { items: T[]; page: number; pageSize: number; totalItems: number; totalPages: number }
type Goal = { weeklyMinutesTarget: number; weeklyStrengthSessionsTarget: number }
type Summary = {
  weekStart: string; weekEnd: string; timeZone: string
  equivalentMinutes: number; strengthSessions: number
  weeklyMinutesTarget: number; weeklyStrengthSessionsTarget: number
  minutesProgressPercent: number; strengthProgressPercent: number
}
type Dimension = { code: string; weight: number; rawScore: number | null; reasonCode: string }
type Score = {
  localDate: string; version: string; score: number | null
  coveragePercent: number; isPartial: boolean; isClinical: false
  missingData: string[]; breakdown: Dimension[]; disclaimer: string
}
const kinds: Record<number, string> = { 1: 'Đi bộ', 2: 'Vận động vừa', 3: 'Vận động mạnh', 4: 'Tập sức mạnh' }
const dims: Record<string, string> = {
  activity: 'Vận động',
  meals: 'Thói quen ăn uống',
  eating_window: 'Khung giờ ăn',
}
function localDateTime() {
  const n = new Date()
  return new Date(n.getTime() - n.getTimezoneOffset() * 60_000).toISOString().slice(0, 16)
}
function todayIn(timeZone: string) {
  const pieces = new Intl.DateTimeFormat('en-US', {
    year: 'numeric', month: '2-digit', day: '2-digit', timeZone,
  }).formatToParts(new Date())
  const field = (type: 'year' | 'month' | 'day') =>
    pieces.find((piece) => piece.type === type)?.value ?? ''
  return `${field('year')}-${field('month')}-${field('day')}`
}
export function ActivityTrackingPage() {
  const qc = useQueryClient()
  const [kind, setKind] = useState(1)
  const [duration, setDuration] = useState(30)
  const [note, setNote] = useState('')
  const [startedAt, setStartedAt] = useState(localDateTime)
  const [goalMinutes, setGoalMinutes] = useState(150)
  const [goalStrength, setGoalStrength] = useState(2)
  const [page, setPage] = useState(1)
  const [feedback, setFeedback] = useState<string | null>(null)

  const profile = useQuery({
    queryKey: ['profile'],
    queryFn: async () => (await api.get<{ timeZone: string }>('/profile')).data,
  })
  const timeZone = profile.data?.timeZone ?? 'Asia/Ho_Chi_Minh'
  const localToday = useMemo(() => todayIn(timeZone), [timeZone])
  const activity = useQuery({
    queryKey: ['activities', page],
    queryFn: async () => (await api.get<Page<ActivityLog>>('/activities', {
      params: { page, pageSize: 10 },
    })).data,
  })
  const summary = useQuery({
    queryKey: ['activity-summary'],
    queryFn: async () => (await api.get<Summary>('/activities/summary')).data,
  })
  const goal = useQuery({
    queryKey: ['activity-goal'],
    queryFn: async () => (await api.get<Goal>('/activities/goal')).data,
  })
  useEffect(() => {
    if (goal.data) {
      setGoalMinutes(goal.data.weeklyMinutesTarget)
      setGoalStrength(goal.data.weeklyStrengthSessionsTarget)
    }
  }, [goal.data])
  const score = useQuery({
    queryKey: ['ldas', localToday],
    enabled: profile.isSuccess,
    queryFn: async () => (await api.get<Score>(`/ldas/${localToday}`)).data,
  })
  const refresh = async () => {
    await Promise.all([
      qc.invalidateQueries({ queryKey: ['activities'] }),
      qc.invalidateQueries({ queryKey: ['activity-summary'] }),
      qc.invalidateQueries({ queryKey: ['ldas'] }),
    ])
  }
  const create = useMutation({
    mutationFn: async () => api.post('/activities', {
      kind, durationMinutes: duration, startedAt: new Date(startedAt).toISOString(),
      note: note.trim() || null,
    }),
    onSuccess: async () => {
      setFeedback('Đã lưu hoạt động vào lịch sử.')
      setNote('')
      await refresh()
    },
    onError: (error) => setFeedback(getApiErrorMessage(error, 'Không thể lưu hoạt động.')),
  })
  const remove = useMutation({
    mutationFn: async (id: string) => api.delete(`/activities/${id}`),
    onSuccess: async () => {
      setFeedback('Đã xóa hoạt động.')
      await refresh()
    },
    onError: (error) => setFeedback(getApiErrorMessage(error)),
  })
  const saveGoal = useMutation({
    mutationFn: async () => api.put('/activities/goal', {
      weeklyMinutesTarget: goalMinutes,
      weeklyStrengthSessionsTarget: goalStrength,
    }),
    onSuccess: async () => {
      setFeedback('Đã lưu mục tiêu tuần.')
      await qc.invalidateQueries({ queryKey: ['activity-goal'] })
      await refresh()
    },
    onError: (error) => setFeedback(getApiErrorMessage(error)),
  })
  return (
    <main className="tracking-page">
      <header className="tracking-header">
        <Link to="/app" className="tracking-back"><ArrowLeft size={18} /> Ứng dụng</Link>
        <span className="eyebrow">THEO DÕI THÓI QUEN • DỮ LIỆU THẬT</span>
        <h1>Vận động và điểm tuân thủ</h1>
        <p>Ghi nhận thói quen của bạn theo múi giờ hồ sơ, không dự đoán tuổi thọ hoặc chẩn đoán y khoa.</p>
      </header>
      {feedback && <p className="tracking-feedback" role="status">{feedback}</p>}
      <div className="tracking-two-col">
        <section className="tracking-panel">
          <h2><Activity size={20} /> Ghi hoạt động mới</h2>
          <form onSubmit={(event) => { event.preventDefault(); setFeedback(null); create.mutate() }}>
            <label>Loại hoạt động
              <select value={kind} onChange={(event) => setKind(Number(event.target.value))}>
                {Object.entries(kinds).map(([id, label]) =>
                  <option key={id} value={id}>{label}</option>)}
              </select>
            </label>
            <label>Thời gian bắt đầu (giờ địa phương của thiết bị)
              <input required type="datetime-local" value={startedAt}
                onChange={(event) => setStartedAt(event.target.value)} />
            </label>
            <label>Thời lượng (phút)
              <input required min={1} max={720} type="number" value={duration}
                onChange={(event) => setDuration(Number(event.target.value))} />
            </label>
            <label>Ghi chú
              <textarea maxLength={800} value={note}
                onChange={(event) => setNote(event.target.value)} placeholder="Không bắt buộc" />
            </label>
            <button type="submit" disabled={create.isPending}><Save size={16} /> Lưu hoạt động</button>
          </form>
        </section>
        <section className="tracking-panel">
          <h2><CalendarDays size={20} /> Tiến độ tuần</h2>
          {summary.isLoading ? <p>Đang tải thống kê...</p> :
            summary.isError ? <p role="alert">{getApiErrorMessage(summary.error)}</p> :
              <div className="tracking-summary">
                <strong>{summary.data?.equivalentMinutes ?? 0} / {summary.data?.weeklyMinutesTarget ?? 150} phút</strong>
                <progress value={summary.data?.minutesProgressPercent ?? 0} max={100} />
                <span>Sức mạnh: {summary.data?.strengthSessions ?? 0} / {summary.data?.weeklyStrengthSessionsTarget ?? 2} buổi</span>
                <small>Múi giờ: {summary.data?.timeZone}</small>
              </div>}
          <div className="tracking-goal">
            <h3>Thay đổi mục tiêu</h3>
            <p>Đang áp dụng: {goal.data?.weeklyMinutesTarget ?? 150} phút / {goal.data?.weeklyStrengthSessionsTarget ?? 2} buổi sức mạnh</p>
            <form onSubmit={(event) => { event.preventDefault(); saveGoal.mutate() }}>
              <label>Phút mỗi tuần <input type="number" min={1} max={1000} value={goalMinutes}
                onChange={(event) => setGoalMinutes(Number(event.target.value))} /></label>
              <label>Buổi sức mạnh <input type="number" min={0} max={7} value={goalStrength}
                onChange={(event) => setGoalStrength(Number(event.target.value))} /></label>
              <button type="submit" disabled={saveGoal.isPending}>Lưu mục tiêu</button>
            </form>
          </div>
        </section>
      </div>
      <section className="tracking-panel">
        <h2>LDAS • {score.data?.version ?? 'ldas-v1'}</h2>
        {score.isLoading ? <p>Đang tính từ dữ liệu đã lưu...</p> :
          score.isError ? <p role="alert">{getApiErrorMessage(score.error)}</p> :
            <>
              <div className="tracking-score">
                <strong>{score.data?.score === null || score.data?.score === undefined
                  ? 'Chưa đủ dữ liệu' : `${score.data.score}/100`}</strong>
                <span>Độ phủ dữ liệu: {score.data?.coveragePercent ?? 0}%</span>
              </div>
              <div className="tracking-dimensions">
                {score.data?.breakdown.map((d) => (
                  <div key={d.code}>
                    <span>{dims[d.code] ?? d.code}</span>
                    <strong>{d.rawScore === null ? 'Chưa ghi nhận' : `${d.rawScore}/100`}</strong>
                  </div>
                ))}
              </div>
              {score.data?.isPartial && <p className="tracking-note">
                Còn thiếu: {score.data.missingData.map((d) => dims[d] ?? d).join(', ')}.
                Không coi dữ liệu thiếu là kết quả kém.
              </p>}
              <p className="tracking-note">LDAS là chỉ số theo dõi thói quen mang tính giáo dục,
                không thay thế tư vấn y khoa hoặc dự đoán tuổi thọ.</p>
            </>}
      </section>
      <section className="tracking-panel">
        <h2>Lịch sử vận động</h2>
        {activity.isLoading ? <p>Đang tải lịch sử...</p> :
          activity.isError ? <p role="alert">{getApiErrorMessage(activity.error)}</p> :
            activity.data?.items.length === 0 ? <p>Chưa ghi nhận hoạt động nào.</p> :
              <div className="tracking-history">
                {activity.data?.items.map((entry) => (
                  <div key={entry.id} className="tracking-history-item">
                    <div><strong>{kinds[entry.kind] ?? 'Hoạt động'}</strong>
                      <small>{new Date(entry.startedAtUtc).toLocaleString('vi-VN', { timeZone })}</small>
                      {entry.note && <span>{entry.note}</span>}
                    </div>
                    <b>{entry.durationMinutes} phút</b>
                    <button type="button" title="Xóa hoạt động" aria-label="Xóa hoạt động"
                      disabled={remove.isPending} onClick={() => {
                        if (window.confirm('Bạn muốn xóa hoạt động này?')) remove.mutate(entry.id)
                      }}><Trash2 size={17} /></button>
                  </div>
                ))}
              </div>}
        {(activity.data?.totalPages ?? 0) > 1 &&
          <div className="tracking-pagination">
            <button disabled={page === 1} onClick={() => setPage(page - 1)}>Trước</button>
            <span>Trang {page} / {activity.data?.totalPages}</span>
            <button disabled={page >= (activity.data?.totalPages ?? 1)}
              onClick={() => setPage(page + 1)}>Sau</button>
          </div>}
      </section>
    </main>
  )
}
