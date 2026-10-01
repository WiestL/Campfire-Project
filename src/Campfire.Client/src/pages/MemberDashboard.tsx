import { useAuth } from '../auth/AuthContext'
import { Feed } from '../components/Feed'

export function MemberDashboard() {
  const { user, logout } = useAuth()

  return (
    <div className="dashboard">
      <header className="dashboard-header">
        <h1>Member Dashboard</h1>
        <button type="button" onClick={logout}>Log out</button>
      </header>
      <p>Welcome back, {user?.displayName}.</p>

      <section className="dashboard-grid">
        <article className="dashboard-card">
          <h2>Membership</h2>
          <p>Manage your subscription and payment methods.</p>
          <p className="placeholder">Coming in Week 6</p>
        </article>
        <article className="dashboard-card">
          <h2>Book Club</h2>
          <p>Join reading discussions.</p>
          <p className="placeholder">Coming in Week 8</p>
        </article>
      </section>

      <h2 className="feed-heading">Feed</h2>
      <Feed />
    </div>
  )
}
