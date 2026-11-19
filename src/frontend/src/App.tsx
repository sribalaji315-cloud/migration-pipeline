import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AppLayout } from './components/layout/AppLayout'
import { ProtectedRoute } from './components/layout/ProtectedRoute'
import { AuthProvider } from './context/AuthContext'
import { BomWavesPage } from './pages/BomWavesPage'
import { IssuesPage } from './pages/IssuesPage'
import { ItemClassPage } from './pages/ItemClassPage'
import { LoginPage } from './pages/LoginPage'
import { MigrationToolPage } from './pages/MigrationToolPage'
import { OverviewPage } from './pages/OverviewPage'
import { ReferencePage } from './pages/ReferencePage'
import { RunsPage } from './pages/RunsPage'
import { SwingPage } from './pages/SwingPage'
import { TypeChangesPage } from './pages/TypeChangesPage'
import { WaveDetailPage } from './pages/WaveDetailPage'

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route element={<ProtectedRoute><AppLayout /></ProtectedRoute>}>
            <Route index element={<OverviewPage />} />
            <Route path="waves" element={<BomWavesPage />} />
            <Route path="waves/:waveId" element={<WaveDetailPage />} />
            <Route path="items" element={<ItemClassPage />} />
            <Route path="items/:itemId" element={<ItemClassPage />} />
            <Route path="issues" element={<IssuesPage />} />
            <Route path="type-changes" element={<TypeChangesPage />} />
            <Route path="swing" element={<SwingPage />} />
            <Route path="migration-tool" element={<ProtectedRoute minimumRole="Admin"><MigrationToolPage /></ProtectedRoute>} />
            <Route path="runs" element={<RunsPage />} />
            <Route path="reference" element={<ReferencePage />} />
          </Route>
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </AuthProvider>
    </BrowserRouter>
  )
}
