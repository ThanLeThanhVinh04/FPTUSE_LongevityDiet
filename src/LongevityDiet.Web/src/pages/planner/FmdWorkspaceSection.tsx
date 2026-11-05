import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  AlertCircle,
  AlertTriangle,
  BookOpen,
  Calendar,
  CheckCircle2,
  ExternalLink,
  Flame,
  History,
  Play,
  Shield,
  ShieldAlert,
  ShieldCheck,
  StopCircle,
} from 'lucide-react'
import { useState } from 'react'
import { api } from '../../lib/api'
import { getApiErrorMessage } from '../../lib/apiError'

export type FmdEducationCitationDto = {
  title: string
  sourceType: string
  url: string
  evidenceSummary: string
}

export type FmdEducationContentDto = {
  version: string
  title: string
  summary: string
  scopeDisclaimer: string
  keyPrinciples: string[]
  absoluteContraindications: string[]
  citations: FmdEducationCitationDto[]
}

export type FmdSafetyQuestionDto = {
  questionId: string
  category: string
  questionText: string
  medicalRationale: string
  isHighRiskIfTrue: boolean
}

export type FmdSafetyStatusSummaryDto = {
  currentStatus: number
  statusTitle: string
  statusMessage: string
  isEligibleForTracking: boolean
  requiresProfessionalReview: boolean
  lastAcknowledgedAt: string | null
  lastEvaluatedAt: string | null
  triggeredRiskCodes: string[]
  notes: string | null
}

export type FmdCycleDaySummaryDto = {
  id: string
  dayNumber: number
  planDate: string
  targetCaloriesKcal: number
  energyLevel: number | null
  hungerLevel: number | null
  waterIntakeLiters: number | null
  symptoms: string | null
  dailyNotes: string | null
  isCompleted: boolean
  loggedAt: string | null
}

export type FmdCycleSummaryDto = {
  id: string
  userId: string
  cycleNumber: number
  startDate: string
  endDate: string
  targetDays: number
  status: string
  notes: string | null
  startedAt: string
  completedAt: string | null
  stoppedAt: string | null
  stopReason: string | null
  days: FmdCycleDaySummaryDto[]
}

export function FmdWorkspaceSection() {
  const queryClient = useQueryClient()

  // Local form state
  const [answers, setAnswers] = useState<Record<string, boolean>>({})
  const [assessmentNotes, setAssessmentNotes] = useState('')
  const [stopReason, setStopReason] = useState('')
  const [isStoppingDialogOpen, setIsStoppingDialogOpen] = useState(false)
  const [selectedDayNumber, setSelectedDayNumber] = useState<number>(1)

  // Day logging state
  const [dayEnergy, setDayEnergy] = useState<number>(3)
  const [dayHunger, setDayHunger] = useState<number>(3)
  const [dayWater, setDayWater] = useState<string>('2.0')
  const [daySymptoms, setDaySymptoms] = useState<string>('')
  const [dayNotes, setDayNotes] = useState<string>('')

  const [uiFeedback, setUiFeedback] = useState<string | null>(null)
  const [uiError, setUiError] = useState<string | null>(null)

  // Queries
  const educationQuery = useQuery({
    queryKey: ['fmd-education'],
    queryFn: async () => {
      const res = await api.get<{
        education: FmdEducationContentDto
        screeningQuestions: FmdSafetyQuestionDto[]
      }>('/fmd/education')
      return res.data
    },
  })

  const safetyStatusQuery = useQuery({
    queryKey: ['fmd-safety-status'],
    queryFn: async () => {
      const res = await api.get<FmdSafetyStatusSummaryDto>('/fmd/safety/status')
      return res.data
    },
  })

  const activeCycleQuery = useQuery({
    queryKey: ['fmd-active-cycle'],
    queryFn: async () => {
      try {
        const res = await api.get<FmdCycleSummaryDto>('/fmd/cycles/active')
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

  const cycleHistoryQuery = useQuery({
    queryKey: ['fmd-cycles-history'],
    queryFn: async () => {
      const res = await api.get<FmdCycleSummaryDto[]>('/fmd/cycles')
      return res.data
    },
  })

  // Mutations
  const acknowledgeMutation = useMutation({
    mutationFn: async () => {
      const res = await api.post<FmdSafetyStatusSummaryDto>('/fmd/education/acknowledge')
      return res.data
    },
    onSuccess: (data) => {
      setUiError(null)
      setUiFeedback('Đã xác nhận tìm hiểu tài liệu giáo dục và giới hạn an toàn FMD.')
      queryClient.setQueryData(['fmd-safety-status'], data)
    },
    onError: (err) => {
      setUiFeedback(null)
      setUiError(getApiErrorMessage(err, 'Không thể xác nhận lúc này.'))
    },
  })

  const submitAssessmentMutation = useMutation({
    mutationFn: async () => {
      const res = await api.post<FmdSafetyStatusSummaryDto>('/fmd/safety/assessment', {
        answers,
        notes: assessmentNotes.trim() || null,
      })
      return res.data
    },
    onSuccess: (data) => {
      setUiError(null)
      if (data.requiresProfessionalReview) {
        setUiFeedback(
          'Hệ thống ghi nhận yếu tố rủi ro. Bạn cần tham vấn chuyên môn y tế trước khi thực hiện FMD.',
        )
      } else {
        setUiFeedback('Đánh giá hoàn tất: Bạn đủ điều kiện sức khỏe ban đầu để theo dõi chu kỳ.')
      }
      queryClient.setQueryData(['fmd-safety-status'], data)
    },
    onError: (err) => {
      setUiFeedback(null)
      setUiError(getApiErrorMessage(err, 'Không thể gửi bảng sàng lọc.'))
    },
  })

  const startCycleMutation = useMutation({
    mutationFn: async () => {
      const res = await api.post<FmdCycleSummaryDto>('/fmd/cycles', {})
      return res.data
    },
    onSuccess: (data) => {
      setUiError(null)
      setUiFeedback('Đã kích hoạt chu kỳ FMD 5 ngày thành công.')
      queryClient.setQueryData(['fmd-active-cycle'], data)
      void queryClient.invalidateQueries({ queryKey: ['fmd-cycles-history'] })
    },
    onError: (err) => {
      setUiFeedback(null)
      setUiError(getApiErrorMessage(err, 'Không thể bắt đầu chu kỳ FMD.'))
    },
  })

  const logDayMutation = useMutation({
    mutationFn: async ({
      cycleId,
      dayNumber,
    }: {
      cycleId: string
      dayNumber: number
    }) => {
      const res = await api.post<FmdCycleSummaryDto>(
        `/fmd/cycles/${cycleId}/days/${dayNumber}`,
        {
          dayNumber,
          energyLevel: dayEnergy,
          hungerLevel: dayHunger,
          waterIntakeLiters: parseFloat(dayWater) || null,
          symptoms: daySymptoms.trim() || null,
          dailyNotes: dayNotes.trim() || null,
        },
      )
      return res.data
    },
    onSuccess: (data) => {
      setUiError(null)
      setUiFeedback(`Đã lưu nhật ký Ngày ${selectedDayNumber} thành công.`)
      queryClient.setQueryData(['fmd-active-cycle'], data)
      void queryClient.invalidateQueries({ queryKey: ['fmd-cycles-history'] })
    },
    onError: (err) => {
      setUiFeedback(null)
      setUiError(getApiErrorMessage(err, 'Không thể ghi nhật ký ngày này.'))
    },
  })

  const stopCycleMutation = useMutation({
    mutationFn: async ({ cycleId }: { cycleId: string }) => {
      const res = await api.post<FmdCycleSummaryDto>(`/fmd/cycles/${cycleId}/stop`, {
        stopReason: stopReason.trim() || 'Người dùng chủ động dừng chu kỳ',
      })
      return res.data
    },
    onSuccess: () => {
      setUiError(null)
      setUiFeedback('Đã dừng chu kỳ FMD an toàn.')
      setIsStoppingDialogOpen(false)
      setStopReason('')
      queryClient.setQueryData(['fmd-active-cycle'], null)
      void queryClient.invalidateQueries({ queryKey: ['fmd-cycles-history'] })
    },
    onError: (err) => {
      setUiFeedback(null)
      setUiError(getApiErrorMessage(err, 'Không thể dừng chu kỳ lúc này.'))
    },
  })

  const completeCycleMutation = useMutation({
    mutationFn: async ({ cycleId }: { cycleId: string }) => {
      const res = await api.post<FmdCycleSummaryDto>(`/fmd/cycles/${cycleId}/complete`)
      return res.data
    },
    onSuccess: () => {
      setUiError(null)
      setUiFeedback('Chúc mừng bạn đã hoàn thành trọn vẹn chu kỳ FMD 5 ngày!')
      queryClient.setQueryData(['fmd-active-cycle'], null)
      void queryClient.invalidateQueries({ queryKey: ['fmd-cycles-history'] })
    },
    onError: (err) => {
      setUiFeedback(null)
      setUiError(getApiErrorMessage(err, 'Không thể kết thúc chu kỳ lúc này.'))
    },
  })

  const education = educationQuery.data?.education
  const questions = educationQuery.data?.screeningQuestions ?? []
  const safety = safetyStatusQuery.data
  const activeCycle = activeCycleQuery.data
  const history = cycleHistoryQuery.data ?? []

  const isEligible = safety?.isEligibleForTracking ?? false
  const isHighRisk = safety?.requiresProfessionalReview ?? false

  const handleAnswerToggle = (questionId: string, val: boolean) => {
    setAnswers((prev) => ({
      ...prev,
      [questionId]: val,
    }))
  }

  return (
    <section className="workspace-section" id="fmd-tracker">
      <div className="workspace-header-row">
        <div>
          <span className="eyebrow">GIÁO DỤC, SÀNG LỌC & THEO DÕI CHU KỲ</span>
          <h2>Chu trình Fasting Mimicking Diet (FMD 5 ngày).</h2>
          <p className="section-subtitle">
            Mô phỏng trạng thái nhịn ăn có kiểm soát vi chất, tuân thủ nghiêm ngặt ranh giới an toàn
            y khoa và khuyến nghị của Viện Valter Longo.
          </p>
        </div>
      </div>

      {/* FEEDBACK & ERROR NOTICES */}
      {uiFeedback && (
        <div className="status-banner" style={{ background: '#eaf6ee', color: '#1a6b35', padding: '12px 18px', borderRadius: '12px', marginBottom: '18px', border: '1px solid #c3e6cb' }}>
          <CheckCircle2 size={16} style={{ display: 'inline', marginRight: '8px', verticalAlign: '-3px' }} />
          <span>{uiFeedback}</span>
        </div>
      )}

      {uiError && (
        <div className="error-banner" style={{ background: '#fdf2f2', color: '#9b2c2c', padding: '12px 18px', borderRadius: '12px', marginBottom: '18px', border: '1px solid #f8b4b4' }}>
          <AlertCircle size={16} style={{ display: 'inline', marginRight: '8px', verticalAlign: '-3px' }} />
          <span>{uiError}</span>
        </div>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: '28px' }}>
        {/* CARD 1: MEDICAL EDUCATION & SCOPE DISCLAIMER */}
        <div className="card" style={{ background: 'rgba(255, 255, 255, 0.95)', border: '1px solid var(--line)', borderRadius: '16px', padding: '24px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '14px' }}>
            <BookOpen size={20} color="var(--green)" />
            <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700 }}>
              1. Kiến thức khoa học & Tuyên bố giới hạn an toàn
            </h3>
            {education && (
              <span style={{ marginLeft: 'auto', fontSize: '12px', background: '#f0f3f1', padding: '4px 10px', borderRadius: '999px', fontWeight: 600 }}>
                {education.version}
              </span>
            )}
          </div>

          <div style={{ background: '#fffbeb', border: '1px solid #fde68a', borderRadius: '12px', padding: '14px 16px', marginBottom: '18px' }}>
            <div style={{ display: 'flex', alignItems: 'flex-start', gap: '10px' }}>
              <ShieldAlert size={18} color="#b45309" style={{ flexShrink: 0, marginTop: '2px' }} />
              <div>
                <strong style={{ color: '#92400e', fontSize: '13.5px' }}>Ranh giới an toàn bắt buộc:</strong>
                <p style={{ margin: '4px 0 0', fontSize: '13px', color: '#78350f', lineHeight: 1.5 }}>
                  {education?.scopeDisclaimer ||
                    'Ứng dụng này cung cấp kiến thức giáo dục và công cụ nhật ký hỗ trợ; KHÔNG PHẢI là thiết bị y tế, KHÔNG đưa ra phác đồ điều trị bệnh hay đơn thuốc nhịn ăn. Bệnh nhân có bệnh lý nền bắt buộc phải có chỉ định trực tiếp từ bác sĩ chuyên khoa.'}
                </p>
              </div>
            </div>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '20px', marginBottom: '18px' }}>
            <div>
              <h4 style={{ fontSize: '14px', fontWeight: 700, color: '#2d3748', marginBottom: '8px' }}>
                Nguyên tắc cốt lõi (5 ngày):
              </h4>
              <ul style={{ margin: 0, paddingLeft: '18px', fontSize: '13px', color: '#4a5568', lineHeight: 1.6 }}>
                {education?.keyPrinciples.map((p, idx) => (
                  <li key={idx}>{p}</li>
                ))}
              </ul>
            </div>
            <div>
              <h4 style={{ fontSize: '14px', fontWeight: 700, color: '#c53030', marginBottom: '8px' }}>
                Đối tượng chống chỉ định tuyệt đối:
              </h4>
              <ul style={{ margin: 0, paddingLeft: '18px', fontSize: '13px', color: '#742a2a', lineHeight: 1.6 }}>
                {education?.absoluteContraindications.slice(0, 5).map((c, idx) => (
                  <li key={idx}>{c}</li>
                ))}
              </ul>
            </div>
          </div>

          {education?.citations && education.citations.length > 0 && (
            <div style={{ borderTop: '1px solid var(--line)', paddingTop: '14px', marginTop: '14px' }}>
              <h4 style={{ fontSize: '12px', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.05em', color: '#718096', marginBottom: '8px' }}>
                Tài liệu dẫn chứng y văn quốc tế:
              </h4>
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: '10px' }}>
                {education.citations.map((cite, i) => (
                  <a
                    key={i}
                    href={cite.url}
                    target="_blank"
                    rel="noreferrer"
                    style={{
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: '6px',
                      fontSize: '12px',
                      color: 'var(--green)',
                      background: '#f4fbf6',
                      padding: '5px 12px',
                      borderRadius: '8px',
                      border: '1px solid #d4ecd9',
                      textDecoration: 'none',
                    }}
                  >
                    <span>{cite.title}</span>
                    <ExternalLink size={12} />
                  </a>
                ))}
              </div>
            </div>
          )}

          <div style={{ marginTop: '20px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span style={{ fontSize: '13px', color: '#718096' }}>
              {safety?.lastAcknowledgedAt ? (
                <span style={{ color: '#2f855a' }}>
                  ✓ Đã đọc & xác nhận vào: {new Date(safety.lastAcknowledgedAt).toLocaleDateString('vi-VN')}
                </span>
              ) : (
                'Chưa xác nhận tài liệu an toàn'
              )}
            </span>
            <button
              type="button"
              className="primary-button"
              onClick={() => acknowledgeMutation.mutate()}
              disabled={acknowledgeMutation.isPending}
              style={{ padding: '8px 16px', fontSize: '13px' }}
            >
              {acknowledgeMutation.isPending ? 'Đang lưu...' : 'Tôi đã đọc và hiểu rõ'}
            </button>
          </div>
        </div>

        {/* CARD 2: SAFETY GATE ASSESSMENT QUESTIONNAIRE */}
        <div className="card" style={{ background: 'rgba(255, 255, 255, 0.95)', border: '1px solid var(--line)', borderRadius: '16px', padding: '24px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '14px' }}>
            <Shield size={20} color="var(--green)" />
            <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700 }}>
              2. Bảng sàng lọc an toàn y khoa (Safety Gate)
            </h3>
            {isEligible && (
              <span style={{ marginLeft: 'auto', display: 'inline-flex', alignItems: 'center', gap: '4px', fontSize: '12px', color: '#276749', background: '#c6f6d5', padding: '4px 10px', borderRadius: '999px', fontWeight: 700 }}>
                <ShieldCheck size={14} /> Đủ điều kiện theo dõi
              </span>
            )}
            {isHighRisk && (
              <span style={{ marginLeft: 'auto', display: 'inline-flex', alignItems: 'center', gap: '4px', fontSize: '12px', color: '#9b2c2c', background: '#fed7d7', padding: '4px 10px', borderRadius: '999px', fontWeight: 700 }}>
                <ShieldAlert size={14} /> Cần tham vấn bác sĩ
              </span>
            )}
          </div>

          <p style={{ fontSize: '13px', color: '#4a5568', marginTop: 0, marginBottom: '16px' }}>
            Vui lòng trả lời trung thực các câu hỏi dưới đây. Nếu có bất kỳ câu trả lời "CÓ", hệ thống sẽ lập tức chặn kích hoạt chu kỳ để bảo vệ sức khỏe của bạn.
          </p>

          {/* HIGH RISK ALERT IF APPLICABLE */}
          {isHighRisk && (
            <div style={{ background: '#fff5f5', border: '1px solid #feb2b2', borderRadius: '12px', padding: '16px', marginBottom: '20px' }}>
              <div style={{ display: 'flex', alignItems: 'flex-start', gap: '12px' }}>
                <AlertTriangle size={20} color="#e53e3e" style={{ flexShrink: 0 }} />
                <div>
                  <strong style={{ color: '#c53030', fontSize: '14px' }}>
                    Backend đã chặn kích hoạt chu kỳ FMD:
                  </strong>
                  <p style={{ margin: '4px 0 8px', fontSize: '13px', color: '#9b2c2c', lineHeight: 1.5 }}>
                    {safety?.statusMessage}
                  </p>
                  {safety?.triggeredRiskCodes && safety.triggeredRiskCodes.length > 0 && (
                    <div style={{ fontSize: '12px', color: '#742a2a' }}>
                      <strong>Mã nguy cơ ghi nhận:</strong> {safety.triggeredRiskCodes.join(', ')}
                    </div>
                  )}
                </div>
              </div>
            </div>
          )}

          {/* SCREENING QUESTIONS LIST */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: '14px', marginBottom: '20px' }}>
            {questions.map((q) => {
              const qId = q.questionId
              const currentVal = answers[qId] ?? false
              return (
                <div
                  key={qId}
                  style={{
                    padding: '14px 16px',
                    borderRadius: '12px',
                    background: currentVal ? '#fffaf0' : '#f7fafc',
                    border: currentVal ? '1px solid #feebc8' : '1px solid #edf2f7',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    gap: '16px',
                  }}
                >
                  <div style={{ flex: 1 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      <span style={{ fontSize: '11px', fontWeight: 700, color: '#718096', textTransform: 'uppercase' }}>
                        {q.category}
                      </span>
                    </div>
                    <div style={{ fontSize: '13.5px', fontWeight: 600, color: '#2d3748', marginTop: '3px' }}>
                      {q.questionText}
                    </div>
                    <div style={{ fontSize: '12px', color: '#718096', marginTop: '3px' }}>
                      {q.medicalRationale}
                    </div>
                  </div>

                  <div style={{ display: 'flex', gap: '8px', flexShrink: 0 }}>
                    <button
                      type="button"
                      onClick={() => handleAnswerToggle(qId, false)}
                      style={{
                        padding: '6px 14px',
                        borderRadius: '8px',
                        border: '1px solid var(--line)',
                        background: currentVal === false ? 'var(--green)' : '#fff',
                        color: currentVal === false ? '#fff' : '#4a5568',
                        fontWeight: 600,
                        fontSize: '12.5px',
                        cursor: 'pointer',
                      }}
                    >
                      Không
                    </button>
                    <button
                      type="button"
                      onClick={() => handleAnswerToggle(qId, true)}
                      style={{
                        padding: '6px 14px',
                        borderRadius: '8px',
                        border: '1px solid #fed7d7',
                        background: currentVal === true ? '#e53e3e' : '#fff',
                        color: currentVal === true ? '#fff' : '#c53030',
                        fontWeight: 600,
                        fontSize: '12.5px',
                        cursor: 'pointer',
                      }}
                    >
                      Có
                    </button>
                  </div>
                </div>
              )
            })}
          </div>

          <div style={{ marginBottom: '18px' }}>
            <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, color: '#4a5568', marginBottom: '6px' }}>
              Ghi chú thêm về sức khỏe (nếu có):
            </label>
            <input
              type="text"
              className="plain-input"
              value={assessmentNotes}
              onChange={(e) => setAssessmentNotes(e.target.value)}
              placeholder="VD: Đang theo dõi chỉ số huyết áp hoặc ý kiến bác sĩ gia đình..."
              style={{ width: '100%' }}
            />
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
            <button
              type="button"
              className="primary-button"
              onClick={() => submitAssessmentMutation.mutate()}
              disabled={submitAssessmentMutation.isPending}
              style={{ padding: '9px 20px', fontSize: '13.5px' }}
            >
              {submitAssessmentMutation.isPending ? 'Đang đánh giá...' : 'Gửi bảng sàng lọc an toàn'}
            </button>
          </div>
        </div>

        {/* CARD 3: ACTIVE 5-DAY CYCLE TRACKER */}
        <div className="card" style={{ background: 'rgba(255, 255, 255, 0.95)', border: '1px solid var(--line)', borderRadius: '16px', padding: '24px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '14px' }}>
            <Calendar size={20} color="var(--green)" />
            <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700 }}>
              3. Quản lý & Theo dõi chu kỳ 5 ngày
            </h3>
            {activeCycle && (
              <span style={{ marginLeft: 'auto', display: 'inline-flex', alignItems: 'center', gap: '4px', fontSize: '12px', color: '#2b6cb0', background: '#ebf8ff', padding: '4px 10px', borderRadius: '999px', fontWeight: 700 }}>
                Chu kỳ #{activeCycle.cycleNumber} (Đang diễn ra)
              </span>
            )}
          </div>

          {!activeCycle ? (
            <div style={{ textAlign: 'center', padding: '32px 16px', background: '#f8fafc', borderRadius: '12px', border: '1px dashed #cbd5e1' }}>
              <Flame size={36} color="#94a3b8" style={{ marginBottom: '8px' }} />
              <h4 style={{ margin: '0 0 6px', fontSize: '15px', fontWeight: 700, color: '#334155' }}>
                Hiện tại không có chu kỳ FMD nào đang diễn ra
              </h4>
              <p style={{ margin: '0 auto 16px', fontSize: '13px', color: '#64748b', maxWidth: '440px' }}>
                {isEligible
                  ? 'Bạn đã đạt đầy đủ điều kiện an toàn y khoa. Bạn có thể bấm bên dưới để bắt đầu chu kỳ 5 ngày.'
                  : 'Bạn cần hoàn thành bảng sàng lọc an toàn ở mục trên và không có yếu tố rủi ro cao để bắt đầu chu kỳ.'}
              </p>
              <button
                type="button"
                className="primary-button"
                onClick={() => startCycleMutation.mutate()}
                disabled={!isEligible || startCycleMutation.isPending}
                style={{ padding: '10px 24px', fontSize: '14px', display: 'inline-flex', alignItems: 'center', gap: '8px' }}
              >
                <Play size={16} />
                <span>{startCycleMutation.isPending ? 'Đang tạo chu kỳ...' : 'Bắt đầu chu kỳ 5 ngày mới'}</span>
              </button>
            </div>
          ) : (
            <div>
              {/* CYCLE METRICS BANNER */}
              <div style={{ display: 'flex', flexWrap: 'wrap', gap: '16px', padding: '14px 18px', background: '#f1f8f4', borderRadius: '12px', marginBottom: '20px', alignItems: 'center', justifyContent: 'space-between' }}>
                <div style={{ display: 'flex', gap: '20px', fontSize: '13px' }}>
                  <div>
                    <span style={{ color: '#718096' }}>Ngày bắt đầu: </span>
                    <strong>{activeCycle.startDate}</strong>
                  </div>
                  <div>
                    <span style={{ color: '#718096' }}>Ngày kết thúc dự kiến: </span>
                    <strong>{activeCycle.endDate}</strong>
                  </div>
                  <div>
                    <span style={{ color: '#718096' }}>Tiến độ: </span>
                    <strong>
                      {activeCycle.days.filter((d) => d.isCompleted).length} / 5 ngày
                    </strong>
                  </div>
                </div>

                <div style={{ display: 'flex', gap: '10px' }}>
                  <button
                    type="button"
                    onClick={() => setIsStoppingDialogOpen(true)}
                    style={{
                      padding: '7px 14px',
                      borderRadius: '8px',
                      border: '1px solid #feb2b2',
                      background: '#fff5f5',
                      color: '#c53030',
                      fontSize: '12.5px',
                      fontWeight: 600,
                      cursor: 'pointer',
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: '6px',
                    }}
                  >
                    <StopCircle size={14} /> Dừng chu kỳ ngay
                  </button>
                  {activeCycle.days.every((d) => d.isCompleted) && (
                    <button
                      type="button"
                      className="primary-button"
                      onClick={() => completeCycleMutation.mutate({ cycleId: activeCycle.id })}
                      disabled={completeCycleMutation.isPending}
                      style={{ padding: '7px 14px', fontSize: '12.5px' }}
                    >
                      Hoàn thành chu kỳ 5 ngày
                    </button>
                  )}
                </div>
              </div>

              {/* 5 DAYS TABS */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: '10px', marginBottom: '20px' }}>
                {activeCycle.days.map((day) => {
                  const isSelected = day.dayNumber === selectedDayNumber
                  return (
                    <button
                      key={day.dayNumber}
                      type="button"
                      onClick={() => setSelectedDayNumber(day.dayNumber)}
                      style={{
                        padding: '12px 8px',
                        borderRadius: '12px',
                        border: isSelected ? '2px solid var(--green)' : '1px solid var(--line)',
                        background: isSelected ? '#f4fbf6' : '#fff',
                        cursor: 'pointer',
                        textAlign: 'center',
                      }}
                    >
                      <div style={{ fontSize: '13px', fontWeight: 700, color: isSelected ? 'var(--green)' : '#2d3748' }}>
                        Ngày {day.dayNumber}
                      </div>
                      <div style={{ fontSize: '11px', color: '#718096', margin: '2px 0' }}>
                        {day.planDate}
                      </div>
                      <div style={{ fontSize: '12px', fontWeight: 600, color: '#e53e3e' }}>
                        ~{day.targetCaloriesKcal} kcal
                      </div>
                      <div style={{ marginTop: '4px' }}>
                        {day.isCompleted ? (
                          <span style={{ fontSize: '11px', color: '#2f855a', fontWeight: 600 }}>✓ Đã ghi</span>
                        ) : (
                          <span style={{ fontSize: '11px', color: '#a0aec0' }}>Chưa ghi</span>
                        )}
                      </div>
                    </button>
                  )
                })}
              </div>

              {/* DAY CHECK-IN FORM */}
              {(() => {
                const dayData = activeCycle.days.find((d) => d.dayNumber === selectedDayNumber)
                if (!dayData) return null

                return (
                  <div style={{ background: '#f8fafc', border: '1px solid #e2e8f0', borderRadius: '14px', padding: '18px' }}>
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '14px' }}>
                      <h4 style={{ margin: 0, fontSize: '15px', fontWeight: 700, color: '#1a202c' }}>
                        Nhật ký theo dõi Ngày {dayData.dayNumber} ({dayData.planDate})
                      </h4>
                      <span style={{ fontSize: '12.5px', background: '#edf2f7', padding: '3px 10px', borderRadius: '6px', fontWeight: 600 }}>
                        Mục tiêu khuyến nghị: {dayData.targetCaloriesKcal} kcal (Thực vật)
                      </span>
                    </div>

                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '14px', marginBottom: '14px' }}>
                      <div>
                        <label style={{ display: 'block', fontSize: '12.5px', fontWeight: 600, color: '#4a5568', marginBottom: '4px' }}>
                          Mức năng lượng (1: Rất mệt, 5: Rất khỏe):
                        </label>
                        <select
                          className="plain-input"
                          value={dayEnergy}
                          onChange={(e) => setDayEnergy(Number(e.target.value))}
                          style={{ width: '100%', minHeight: '38px' }}
                        >
                          <option value={1}>1 - Kiệt sức / rất mệt</option>
                          <option value={2}>2 - Hơi uể oải</option>
                          <option value={3}>3 - Bình thường</option>
                          <option value={4}>4 - Tỉnh táo, nhẹ nhõm</option>
                          <option value={5}>5 - Rất khỏe, tràn đầy năng lượng</option>
                        </select>
                      </div>

                      <div>
                        <label style={{ display: 'block', fontSize: '12.5px', fontWeight: 600, color: '#4a5568', marginBottom: '4px' }}>
                          Cảm giác đói (1: Không đói, 5: Cực kỳ đói):
                        </label>
                        <select
                          className="plain-input"
                          value={dayHunger}
                          onChange={(e) => setDayHunger(Number(e.target.value))}
                          style={{ width: '100%', minHeight: '38px' }}
                        >
                          <option value={1}>1 - Không cảm thấy đói</option>
                          <option value={2}>2 - Đói nhẹ thoáng qua</option>
                          <option value={3}>3 - Đói vừa phải</option>
                          <option value={4}>4 - Đói nhiều nhưng kiểm soát được</option>
                          <option value={5}>5 - Rất đói / khó chịu</option>
                        </select>
                      </div>

                      <div>
                        <label style={{ display: 'block', fontSize: '12.5px', fontWeight: 600, color: '#4a5568', marginBottom: '4px' }}>
                          Lượng nước đã uống (Lít):
                        </label>
                        <input
                          type="number"
                          step="0.1"
                          min="0"
                          max="10"
                          className="plain-input"
                          value={dayWater}
                          onChange={(e) => setDayWater(e.target.value)}
                          style={{ width: '100%', minHeight: '38px' }}
                        />
                      </div>
                    </div>

                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '14px', marginBottom: '14px' }}>
                      <div>
                        <label style={{ display: 'block', fontSize: '12.5px', fontWeight: 600, color: '#4a5568', marginBottom: '4px' }}>
                          Triệu chứng bất thường (nếu có):
                        </label>
                        <input
                          type="text"
                          className="plain-input"
                          value={daySymptoms}
                          onChange={(e) => setDaySymptoms(e.target.value)}
                          placeholder="VD: Nhức đầu nhẹ, hoa mắt, ớn lạnh..."
                          style={{ width: '100%', minHeight: '38px' }}
                        />
                      </div>
                      <div>
                        <label style={{ display: 'block', fontSize: '12.5px', fontWeight: 600, color: '#4a5568', marginBottom: '4px' }}>
                          Ghi chú tâm trạng & bữa ăn ngày hôm nay:
                        </label>
                        <input
                          type="text"
                          className="plain-input"
                          value={dayNotes}
                          onChange={(e) => setDayNotes(e.target.value)}
                          placeholder="VD: Uống canh rau củ, dùng một ít quả óc chó..."
                          style={{ width: '100%', minHeight: '38px' }}
                        />
                      </div>
                    </div>

                    <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                      <button
                        type="button"
                        className="primary-button"
                        onClick={() =>
                          logDayMutation.mutate({
                            cycleId: activeCycle.id,
                            dayNumber: selectedDayNumber,
                          })
                        }
                        disabled={logDayMutation.isPending}
                        style={{ padding: '8px 18px', fontSize: '13px' }}
                      >
                        {logDayMutation.isPending ? 'Đang lưu nhật ký...' : 'Lưu nhật ký Ngày này'}
                      </button>
                    </div>
                  </div>
                )
              })()}
            </div>
          )}
        </div>

        {/* CARD 4: CYCLE HISTORY */}
        <div className="card" style={{ background: 'rgba(255, 255, 255, 0.95)', border: '1px solid var(--line)', borderRadius: '16px', padding: '24px' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '14px' }}>
            <History size={20} color="var(--green)" />
            <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700 }}>
              4. Lịch sử các chu kỳ FMD trước đây
            </h3>
            {history.length > 0 && (
              <span style={{ marginLeft: 'auto', fontSize: '12px', background: '#f0f3f1', padding: '3px 10px', borderRadius: '999px', fontWeight: 600 }}>
                {history.length} chu kỳ
              </span>
            )}
          </div>

          {history.length === 0 ? (
            <div style={{ textAlign: 'center', padding: '28px 16px', background: '#f8fafc', borderRadius: '12px', border: '1px dashed #cbd5e1', color: '#64748b' }}>
              <p style={{ margin: 0, fontSize: '13.5px' }}>
                Chưa có chu kỳ FMD nào được ghi nhận trong lịch sử. Khi bạn hoàn thành hoặc dừng một chu kỳ, toàn bộ thông tin sẽ được lưu trữ và hiển thị tại đây.
              </p>
            </div>
          ) : (
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
                <thead>
                  <tr style={{ borderBottom: '1px solid var(--line)', color: '#718096' }}>
                    <th style={{ padding: '8px 12px' }}>Chu kỳ</th>
                    <th style={{ padding: '8px 12px' }}>Thời gian</th>
                    <th style={{ padding: '8px 12px' }}>Trạng thái</th>
                    <th style={{ padding: '8px 12px' }}>Số ngày hoàn thành</th>
                    <th style={{ padding: '8px 12px' }}>Lý do dừng / Ghi chú</th>
                  </tr>
                </thead>
                <tbody>
                  {history.map((c) => (
                    <tr key={c.id} style={{ borderBottom: '1px solid #edf2f7' }}>
                      <td style={{ padding: '10px 12px', fontWeight: 600 }}>#{c.cycleNumber}</td>
                      <td style={{ padding: '10px 12px' }}>
                        {c.startDate} → {c.endDate}
                      </td>
                      <td style={{ padding: '10px 12px' }}>
                        {c.status === 'Completed' && (
                          <span style={{ color: '#22543d', background: '#c6f6d5', padding: '3px 8px', borderRadius: '4px', fontWeight: 600 }}>
                            Hoàn thành
                          </span>
                        )}
                        {c.status === 'StoppedByUser' && (
                          <span style={{ color: '#742a2a', background: '#fed7d7', padding: '3px 8px', borderRadius: '4px', fontWeight: 600 }}>
                            Dừng sớm
                          </span>
                        )}
                        {c.status === 'Active' && (
                          <span style={{ color: '#2b6cb0', background: '#bee3f8', padding: '3px 8px', borderRadius: '4px', fontWeight: 600 }}>
                            Đang chạy
                          </span>
                        )}
                      </td>
                      <td style={{ padding: '10px 12px' }}>
                        {c.days.filter((d) => d.isCompleted).length} / 5 ngày
                      </td>
                      <td style={{ padding: '10px 12px', color: '#4a5568' }}>
                        {c.stopReason || c.notes || '—'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      {/* STOP CYCLE MODAL DIALOG */}
      {isStoppingDialogOpen && activeCycle && (
        <div
          style={{
            position: 'fixed',
            inset: 0,
            background: 'rgba(0, 0, 0, 0.45)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            zIndex: 9999,
            padding: '16px',
          }}
        >
          <div
            style={{
              background: '#fff',
              borderRadius: '16px',
              padding: '24px',
              maxWidth: '460px',
              width: '100%',
              boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1)',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px', color: '#c53030', marginBottom: '12px' }}>
              <AlertTriangle size={24} />
              <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700 }}>Xác nhận dừng chu kỳ FMD</h3>
            </div>
            <p style={{ fontSize: '13.5px', color: '#4a5568', lineHeight: 1.5, margin: '0 0 16px' }}>
              Bạn có thể dừng chu kỳ bất cứ lúc nào nếu cảm thấy mệt mỏi, hoa mắt, chóng mặt hoặc không thoải mái. Sức khỏe và an toàn luôn là ưu tiên cao nhất!
            </p>

            <label style={{ display: 'block', fontSize: '12.5px', fontWeight: 600, color: '#2d3748', marginBottom: '6px' }}>
              Lý do dừng chu kỳ:
            </label>
            <textarea
              className="plain-input"
              rows={3}
              value={stopReason}
              onChange={(e) => setStopReason(e.target.value)}
              placeholder="VD: Cảm thấy chóng mặt, mệt mỏi, cần quay lại chế độ ăn thông thường..."
              style={{ width: '100%', resize: 'none', padding: '10px', marginBottom: '18px' }}
            />

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
              <button
                type="button"
                onClick={() => setIsStoppingDialogOpen(false)}
                style={{
                  padding: '8px 16px',
                  borderRadius: '8px',
                  border: '1px solid var(--line)',
                  background: '#fff',
                  fontWeight: 600,
                  fontSize: '13px',
                  cursor: 'pointer',
                }}
              >
                Hủy bỏ
              </button>
              <button
                type="button"
                onClick={() => stopCycleMutation.mutate({ cycleId: activeCycle.id })}
                disabled={stopCycleMutation.isPending}
                style={{
                  padding: '8px 16px',
                  borderRadius: '8px',
                  border: 'none',
                  background: '#c53030',
                  color: '#fff',
                  fontWeight: 600,
                  fontSize: '13px',
                  cursor: 'pointer',
                }}
              >
                {stopCycleMutation.isPending ? 'Đang dừng...' : 'Xác nhận dừng chu kỳ'}
              </button>
            </div>
          </div>
        </div>
      )}
    </section>
  )
}
