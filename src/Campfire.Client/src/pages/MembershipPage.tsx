import { NavBar } from '../components/NavBar'
import { TierSelection } from '../components/TierSelection'

export function MembershipPage() {
  return (
    <>
      <NavBar />
      <div className="dashboard">
        <header className="dashboard-header">
          <h1>Choose your membership</h1>
        </header>
        <TierSelection />
      </div>
    </>
  )
}
