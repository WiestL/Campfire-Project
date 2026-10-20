import { useAuth } from '../auth/AuthContext'
import { Feed } from '../components/Feed'
import { NavBar } from '../components/NavBar'
import { TierSelection } from '../components/TierSelection'

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
            <h2>Book Club</h2>
            <p>Join reading discussions.</p>
            <p className="placeholder">Coming in Week 8</p>
          </article>
        </section>

        <h2 className="feed-heading">Choose your membership</h2>
        <TierSelection />

        <h2 className="feed-heading">Feed</h2>
        <Feed />
      </div>
    </>
  )
}
