import { useAuth } from '../auth/AuthContext'
import { AdminDashboard } from './AdminDashboard'
import { CreatorDashboard } from './CreatorDashboard'
import { MemberDashboard } from './MemberDashboard'

export function RoleDashboard() {
  const { user } = useAuth()

  if (user?.role === 'Admin') {
    return <AdminDashboard />
  }

  if (user?.role === 'Creator') {
    return <CreatorDashboard />
  }

  return <MemberDashboard />
}
