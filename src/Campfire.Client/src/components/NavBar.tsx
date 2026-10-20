import { Link } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

export function NavBar() {
  const { user, logout } = useAuth()

  return (
    <header className="navbar">
      <div className="navbar-inner">
        <Link to="/" className="navbar-brand">
          Campfire
        </Link>
        <nav className="navbar-links">
          {(user?.role === 'Creator' || user?.role === 'Admin') && (
            <>
              <Link to="/content/new">New post</Link>
              <Link to="/tiers">Tiers</Link>
            </>
          )}
        </nav>
        <div className="navbar-user">
          <span className="navbar-name">{user?.displayName}</span>
          <button type="button" className="navbar-logout" onClick={logout}>
            Log out
          </button>
        </div>
      </div>
    </header>
  )
}
