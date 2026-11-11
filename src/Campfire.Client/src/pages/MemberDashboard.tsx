import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { Feed } from '../components/Feed'
import { NavBar } from '../components/NavBar'

export function MemberDashboard() {
  const { user } = useAuth()

  return (
    <>
      <NavBar />
      <div className="dashboard">
        <header className="dashboard-header">
          <h1>Member Dashboard</h1>
        </header>
        <p>Welcome back, {user?.displayName}.</p>

        <section className="dashboard-grid">
          <article className="dashboard-card">
            <h2>Memberships</h2>
            <p>Browse creators' tiers and manage your subscription.</p>
            <Link to="/membership">Choose your membership</Link>
          </article>
        </section>

        <h2 className="feed-heading">Feed</h2>
        <Feed />
      </div>
    </>
  )
}
