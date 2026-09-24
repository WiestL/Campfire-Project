import { useAuth } from '../auth/AuthContext'

export function CreatorDashboard() {
  const { user, logout } = useAuth()

  return (
    <div className="dashboard">
      <header className="dashboard-header">
        <h1>Creator Dashboard</h1>
        <button type="button" onClick={logout}>Log out</button>
      </header>
      <p>Welcome back, {user?.displayName}.</p>

      <section className="dashboard-grid">
        <article className="dashboard-card">
          <h2>Content</h2>
          <p>Publish and manage posts.</p>
          <p className="placeholder">Coming in Week 3</p>
        </article>
        <article className="dashboard-card">
          <h2>Membership Tiers</h2>
          <p>Define pricing and access levels.</p>
          <p className="placeholder">Coming in Week 4</p>
        </article>
        <article className="dashboard-card">
          <h2>Analytics</h2>
          <p>Member count, revenue, engagement.</p>
          <p className="placeholder">Coming in Week 9</p>
        </article>
      </section>
    </div>
  )
}
