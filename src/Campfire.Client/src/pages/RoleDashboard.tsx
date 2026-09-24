import { useAuth } from '../auth/AuthContext'
import { CreatorDashboard } from './CreatorDashboard'
import { MemberDashboard } from './MemberDashboard'

export function RoleDashboard() {
  const { user } = useAuth()

  if (user?.role === 'Creator' || user?.role === 'Admin') {
    return <CreatorDashboard />
  }

  return <MemberDashboard />
}
