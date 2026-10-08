import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { Feed } from '../components/Feed'

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
          <Link to="/content/new">New post</Link>
        </article>
        <article className="dashboard-card">
          <h2>Membership Tiers</h2>
          <p>Define pricing and access levels.</p>
          <Link to="/tiers">Manage tiers</Link>
        </article>
        <article className="dashboard-card">
          <h2>Analytics</h2>
          <p>Member count, revenue, engagement.</p>
          <p className="placeholder">Coming in Week 9</p>
        </article>
      </section>

      <h2 className="feed-heading">Feed</h2>
      <Feed />
    </div>
  )
}
