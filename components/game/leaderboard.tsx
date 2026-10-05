'use client'

import { useEffect, useState } from 'react'
import { Trophy, Swords, Gamepad2, Percent } from 'lucide-react'

type PlayerStat = {
  name: string
  wins: number
  kills: number
  gamesPlayed: number
  winRate: number
}

export function Leaderboard({ server }: { server: string }) {
  const [rows, setRows]     = useState<PlayerStat[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError]   = useState('')

  useEffect(() => {
    if (!server) return
    // Convert ws(s):// -> http(s)://
    let base = server.replace(/\/ws$/, '')
    base = base.replace(/^wss:\/\//, 'https://').replace(/^ws:\/\//, 'http://')
    fetch(`${base}/leaderboard`)
      .then(r => r.json())
      .then(data => { setRows(data); setLoading(false) })
      .catch(() => { setError('Could not load leaderboard.'); setLoading(false) })
  }, [server])

  if (!server)  return <p className="muted-footnote">Enter a server address to view the leaderboard.</p>
  if (loading)  return <p className="muted-footnote">Loading leaderboard…</p>
  if (error)    return <p className="muted-footnote">{error}</p>
  if (rows.length === 0) return <p className="muted-footnote">No matches played yet. Be the first!</p>

  return (
    <div className="leaderboard">
      <table className="lb-table">
        <thead>
          <tr>
            <th>#</th>
            <th>Player</th>
            <th title="Wins"><Trophy size={13} /></th>
            <th title="Kills"><Swords size={13} /></th>
            <th title="Games"><Gamepad2 size={13} /></th>
            <th title="Win rate"><Percent size={13} /></th>
          </tr>
        </thead>
        <tbody>
          {rows.map((p, i) => (
            <tr key={p.name} className={i === 0 ? 'lb-gold' : i === 1 ? 'lb-silver' : i === 2 ? 'lb-bronze' : ''}>
              <td className="lb-rank">{i + 1}</td>
              <td className="lb-name">{p.name}</td>
              <td>{p.wins}</td>
              <td>{p.kills}</td>
              <td>{p.gamesPlayed}</td>
              <td>{p.winRate}%</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
