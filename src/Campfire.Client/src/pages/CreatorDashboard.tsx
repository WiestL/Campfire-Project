import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { AnalyticsPanel } from '../components/AnalyticsPanel'
import { Feed } from '../components/Feed'
import { NavBar } from '../components/NavBar'

export function CreatorDashboard() {
  const { user } = useAuth()

  return (
    <>
      <NavBar />
      <div className="dashboard">
        <header className="dashboard-header">
          <h1>Creator Dashboard</h1>
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
        </section>

        <h2 className="feed-heading">Analytics</h2>
        <AnalyticsPanel />

        <h2 className="feed-heading">Feed</h2>
        <Feed />
      </div>
    </>
  )
}
