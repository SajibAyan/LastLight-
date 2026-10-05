import { obstacles, distance, type ArenaState } from './types'

const colors = ['#f1c779', '#a9bdcd', '#b4a6d0', '#a7c596', '#dd9e83']
function circle(ctx: CanvasRenderingContext2D, x: number, y: number, r: number, fill: string, stroke?: string) {
  ctx.beginPath(); ctx.arc(x, y, r, 0, Math.PI * 2); ctx.fillStyle = fill; ctx.fill(); if (stroke) { ctx.strokeStyle = stroke; ctx.stroke() }
}
export function renderArena(ctx: CanvasRenderingContext2D, s: ArenaState, you: string, width: number, height: number, aim: { x: number; y: number }) {
  const scale = Math.min(width / 1000, height / 700)
  ctx.clearRect(0, 0, width, height)
  ctx.fillStyle = '#0d1511'; ctx.fillRect(0, 0, width, height)
  ctx.save(); ctx.translate((width - 1000 * scale) / 2, (height - 700 * scale) / 2); ctx.scale(scale, scale)
  ctx.fillStyle = '#1c2b20'; ctx.fillRect(0, 0, 1000, 700)
  for (let x = 0; x < 1000; x += 50) for (let y = 0; y < 700; y += 50) {
    const n = Math.sin(x * .17 + y * .49) * 1000
    ctx.fillStyle = n % 3 > 1 ? '#253225' : '#202e22'; ctx.fillRect(x + 2, y + 2, 46, 46)
    ctx.strokeStyle = '#35432b15'; ctx.strokeRect(x + 4, y + 4, 42, 42)
  }
  circle(ctx, 500, 350, 185, '#1d2b2070', '#68724c20')
  circle(ctx, 500, 350, 145, '#29332365', '#69784f30')
  circle(ctx, 500, 350, 47, '#303b27', '#6f775344')
  circle(ctx, 500, 350, 32, '#263421', '#69754b')
  ctx.save(); ctx.translate(500, 350); ctx.rotate(Math.PI / 4); ctx.strokeStyle = '#a19a5b66'; ctx.strokeRect(-15, -15, 30, 30); ctx.restore()
  for (let i = 0; i < 115; i++) {
    const x = (i * 137.3 + 29) % 990, y = (i * 83.7 + 63) % 690
    const edge = x < 105 || x > 895 || y < 50 || y > 650
    if (edge) {
      circle(ctx, x, y, 18 + (i % 3) * 7, '#142719'); circle(ctx, x - 5, y - 5, 15 + (i % 3) * 5, '#2e4128')
      circle(ctx, x + 7, y - 8, 10, '#374a2d');
    } else { ctx.fillStyle = i % 2 ? '#50613b50' : '#92946b25'; ctx.fillRect(x, y, 3, 2) }
  }
  for (const o of obstacles) {
    ctx.fillStyle = '#0c170e80'; ctx.fillRect(o.x + 7, o.y + 12, o.w + 4, o.h)
    ctx.fillStyle = '#3c4434'; ctx.fillRect(o.x, o.y, o.w, o.h)
    ctx.fillStyle = '#67705a'; ctx.fillRect(o.x, o.y - 8, o.w, o.h - 4)
    ctx.fillStyle = '#737a61'; ctx.fillRect(o.x + 3, o.y - 8, o.w - 6, 4)
    ctx.strokeStyle = '#242e22'; ctx.lineWidth = 2
    for (let y = o.y + 12; y < o.y + o.h - 7; y += 22) { ctx.beginPath(); ctx.moveTo(o.x, y); ctx.lineTo(o.x + o.w, y); ctx.stroke() }
    ctx.fillStyle = '#3f5630'; ctx.fillRect(o.x + 4, o.y - 6, o.w * .4, 7)
    ctx.fillStyle = '#526a36'; ctx.fillRect(o.x + 7, o.y - 5, o.w * .2, 4)
  }
  ctx.save(); ctx.beginPath(); ctx.rect(-5, -5, 1010, 710); ctx.arc(500, 350, s.radius, 0, Math.PI * 2, true); ctx.fillStyle = '#142136bb'; ctx.fill('evenodd'); ctx.restore()
  ctx.setLineDash([9, 9]); ctx.strokeStyle = '#95b6c795'; ctx.lineWidth = 2; ctx.beginPath(); ctx.arc(500, 350, s.radius, 0, Math.PI * 2); ctx.stroke(); ctx.setLineDash([])
  const me = s.players.find(p => p.id === you)
  for (const e of s.effects) {
    const color = e.kind === 'ice' || e.kind === 'time' ? '#a3d6ed' : '#efc374'
    circle(ctx, e.x, e.y, e.radius, `${color}12`, `${color}66`)
  }
  for (const [i, p] of s.players.entries()) {
    if (!p.hatched && p.alive && p.visible) {
      const pulse = Math.sin(s.time * 3) * 3
      ctx.save(); ctx.shadowColor = '#e9b568'; ctx.shadowBlur = 20
      circle(ctx, p.eggX, p.eggY + 8, 17 + pulse, '#e9b56818', '#e9b56855')
      ctx.beginPath(); ctx.ellipse(p.eggX, p.eggY - 3, 9, 13, 0, 0, Math.PI * 2); ctx.fillStyle = '#e1b876'; ctx.fill(); ctx.shadowBlur = 0
      ctx.strokeStyle = '#7d5a2d'; ctx.lineWidth = 2; ctx.beginPath(); ctx.moveTo(p.eggX + 2, p.eggY - 12); ctx.lineTo(p.eggX - 3, p.eggY - 3); ctx.lineTo(p.eggX + 3, p.eggY); ctx.lineTo(p.eggX - 1, p.eggY + 8); ctx.stroke(); ctx.restore()
      if (p.id === you) { ctx.font = '10px sans-serif'; ctx.textAlign = 'center'; ctx.fillStyle = '#ead6a1'; ctx.fillText('YOUR EGG · [E] HATCH', p.eggX, p.eggY - 26) }
    }
    if (!p.alive) { if (p.visible) { ctx.strokeStyle = '#67705a'; ctx.lineWidth = 2; ctx.beginPath(); ctx.moveTo(p.x - 5, p.y - 5); ctx.lineTo(p.x + 5, p.y + 5); ctx.moveTo(p.x + 5, p.y - 5); ctx.lineTo(p.x - 5, p.y + 5); ctx.stroke() } continue }
    const seen = p.id === you || p.invisibleUntil <= s.time || (me && me.revealUntil > s.time && distance(me, p) < 260)
    if (!p.visible || !seen) continue
    ctx.save()
    if (p.invisibleUntil > s.time) ctx.globalAlpha = .4
    if (p.speedUntil > s.time) { for (let j = 1; j <= 4; j++) circle(ctx, p.x - j * 7, p.y + j * 3, 13 - j, `${colors[i]}25`) }
    if (p.shieldUntil > s.time) circle(ctx, p.x, p.y, 110, '#accde710', '#accde760')
    if (p.revealUntil > s.time) { ctx.setLineDash([3, 9]); circle(ctx, p.x, p.y, 260, '#ecc68105', '#ecc68130'); ctx.setLineDash([]) }
    if (p.invulnerableUntil > s.time) circle(ctx, p.x, p.y, 23, '#f4d59520', '#f4d59590')
    circle(ctx, p.x + 3, p.y + 9, 16, '#06110b80')
    circle(ctx, p.x, p.y, 16, '#131d15', p.id === you ? '#f6d78e' : colors[i])
    circle(ctx, p.x, p.y, 11, colors[i])
    circle(ctx, p.x - 3, p.y - 3, 4, '#fff5dc80')
    if (p.frozenUntil > s.time) circle(ctx, p.x, p.y, 20, '#a5dbeb60', '#c1e6f2')
    ctx.font = '10px sans-serif'; ctx.textAlign = 'center'; ctx.fillStyle = p.id === you ? '#f5e1b1' : '#d6e0cd'; ctx.fillText(p.id === you ? `${p.name} · YOU` : p.name, p.x, p.y - 31)
    ctx.fillStyle = '#0c150de0'; ctx.fillRect(p.x - 20, p.y - 25, 40, 4); ctx.fillStyle = p.hp < 30 ? '#ce8871' : colors[i]; ctx.fillRect(p.x - 20, p.y - 25, 40 * p.hp / 100, 4)
    if (p.teleportAt > s.time) { ctx.setLineDash([4, 4]); circle(ctx, p.teleportX, p.teleportY, 19, '#bbabe830', '#bbabe8'); ctx.setLineDash([]) }
    ctx.restore()
  }
  for (const b of s.projectiles) { ctx.save(); ctx.shadowColor = b.owner === you ? '#f6d485' : '#c2cfb6'; ctx.shadowBlur = 12; circle(ctx, b.x, b.y, 3.5, b.owner === you ? '#f6d485' : '#c2cfb6'); ctx.restore() }
  if (me?.alive) { ctx.strokeStyle = '#ead4a588'; ctx.lineWidth = 1; ctx.beginPath(); ctx.moveTo(aim.x - 7, aim.y); ctx.lineTo(aim.x + 7, aim.y); ctx.moveTo(aim.x, aim.y - 7); ctx.lineTo(aim.x, aim.y + 7); ctx.stroke() }
  const vignette = ctx.createRadialGradient(500, 350, 190, 500, 350, 640); vignette.addColorStop(0, 'transparent'); vignette.addColorStop(1, '#07110b99'); ctx.fillStyle = vignette; ctx.fillRect(0, 0, 1000, 700)
  ctx.restore()
}
