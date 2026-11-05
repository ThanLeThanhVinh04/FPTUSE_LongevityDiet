import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AuthContext, type SessionUser } from '../../auth/authState'
import { CatalogPage } from './CatalogPage'
import { getFoods, getRecipes, getRules } from './catalogApi'

vi.mock('./catalogApi', () => ({
  getFoods: vi.fn(),
  getRecipes: vi.fn(),
  getRules: vi.fn(),
}))

const member: SessionUser = {
  id: 'member-1',
  email: 'member@example.test',
  displayName: 'Member',
  role: 'Member',
  profileCompleted: true,
}

function renderCatalog(user: SessionUser = member) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <AuthContext.Provider
          value={{
            user,
            isLoading: false,
            login: async () => user,
            register: async () => user,
            logout: async () => undefined,
            updateSessionUser: () => undefined,
          }}
        >
          <CatalogPage />
        </AuthContext.Provider>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('CatalogPage', () => {
  beforeEach(() => {
    vi.mocked(getFoods).mockReset()
    vi.mocked(getRecipes).mockReset()
    vi.mocked(getRules).mockReset()
  })

  it('shows a loading state while the food query is pending', () => {
    vi.mocked(getFoods).mockReturnValue(new Promise(() => undefined))

    renderCatalog()

    expect(screen.getByText('Đang tải dữ liệu danh mục...')).toBeInTheDocument()
  })

  it('shows an empty state when no food matches the query', async () => {
    vi.mocked(getFoods).mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 8,
      totalItems: 0,
      totalPages: 0,
    })

    renderCatalog()

    expect(await screen.findByText('Không có dữ liệu phù hợp với bộ lọc hiện tại.')).toBeInTheDocument()
  })

  it('shows an error state when the catalog request fails', async () => {
    vi.mocked(getFoods).mockRejectedValue(new Error('network unavailable'))

    renderCatalog()

    expect(await screen.findByText('Không thể tải dữ liệu. Hãy thử lại sau.')).toBeInTheDocument()
  })

  it('shows the admin navigation only to Admin sessions', async () => {
    vi.mocked(getFoods).mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 8,
      totalItems: 0,
      totalPages: 0,
    })

    const view = renderCatalog()
    await screen.findByText('Không có dữ liệu phù hợp với bộ lọc hiện tại.')
    expect(screen.queryByRole('link', { name: 'Quản trị' })).not.toBeInTheDocument()

    view.unmount()

    renderCatalog({ ...member, role: 'Admin' })
    expect(await screen.findByRole('link', { name: 'Quản trị' })).toBeInTheDocument()
  })

  it('sends search text to the server-side food query and resets to page one', async () => {
    vi.mocked(getFoods).mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 8,
      totalItems: 0,
      totalPages: 0,
    })

    renderCatalog()

    fireEvent.change(screen.getByPlaceholderText('Tìm thực phẩm...'), {
      target: { value: 'lentil' },
    })

    await waitFor(() => {
      expect(getFoods).toHaveBeenCalledWith(
        expect.objectContaining({
          page: 1,
          pageSize: 8,
          search: 'lentil',
        }),
      )
    })
  })

  it('maps the member filter control to the server-side food flags', async () => {
    vi.mocked(getFoods).mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 8,
      totalItems: 0,
      totalPages: 0,
    })

    renderCatalog()

    fireEvent.change(screen.getByLabelText('Bộ lọc catalog'), {
      target: { value: 'legume' },
    })

    await waitFor(() => {
      expect(getFoods).toHaveBeenCalledWith(
        expect.objectContaining({
          page: 1,
          isLegume: true,
        }),
      )
    })
  })
})
