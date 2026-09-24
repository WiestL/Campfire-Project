import { useAuth } from '../auth/AuthContext'

export function AdminDashboard() {
  const { user, logout } = useAuth()

  return (
    <div className="dashboard">
      <header className="dashboard-header">
        <h1>Admin Dashboard</h1>
        <button type="button" onClick={logout}>Log out</button>
      </header>
      <p>Welcome back, {user?.displayName}.</p>

      <section className="dashboard-grid">
        <article className="dashboard-card">
          <h2>Users</h2>
          <p>View and manage Creator and Member accounts.</p>
          <p className="placeholder">Not yet built</p>
        </article>
        <article className="dashboard-card">
          <h2>Billing Reconciliation</h2>
          <p>Cross-check Stripe payouts against subscription records.</p>
          <p className="placeholder">Not yet built</p>
        </article>
        <article className="dashboard-card">
          <h2>System Health</h2>
          <p>Monitor API, database, and CDN status.</p>
          <p className="placeholder">Not yet built</p>
        </article>
      </section>
    </div>
  )
}
