import assert from 'node:assert/strict'

const endpoint = process.argv[2] || 'ws://127.0.0.1:8080/ws'
const sockets = []
const timeout = 15000
async function connect() {
  const socket = new WebSocket(endpoint)
  sockets.push(socket)
  const peer = { socket, latest: null, errors: [], waiters: [] }
  socket.addEventListener('message', event => {
    const data = JSON.parse(event.data)
    if (data.type === 'state') peer.latest = data
    if (data.type === 'error') peer.errors.push(data.message)
    for (const waiter of [...peer.waiters]) waiter(data)
  })
  await new Promise((resolve, reject) => { socket.addEventListener('open', resolve, { once: true }); socket.addEventListener('error', reject, { once: true }); setTimeout(() => reject(new Error('Connection timeout')), timeout).unref() })
  return peer
}
function waitFor(peer, predicate) {
  if (peer.latest && predicate(peer.latest)) return Promise.resolve(peer.latest)
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => { peer.waiters = peer.waiters.filter(w => w !== listener); reject(new Error('Snapshot condition timed out')) }, timeout)
    const listener = data => { if (predicate(data)) { clearTimeout(timer); peer.waiters = peer.waiters.filter(w => w !== listener); resolve(data) } }
    peer.waiters.push(listener)
  })
}
const send = (peer, payload) => peer.socket.send(JSON.stringify(payload))
try {
  const host = await connect()
  send(host, { type: 'create', name: 'Host' })
  const room = await waitFor(host, data => data.type === 'state')
  assert.match(room.code, /^[A-F0-9]{6}$/)
  assert.equal(room.state.status, 'waiting')
  console.log('PASS: create room returns a real room code and waiting state')
  const peers = [host]
  for (let i = 1; i < 5; i++) { const peer = await connect(); peers.push(peer); send(peer, { type: 'join', name: `Player ${i}`, code: room.code }); await waitFor(peer, data => data.type === 'state') }
  const started = await waitFor(host, data => data.type === 'state' && data.state.status === 'playing')
  assert.equal(started.state.players.length, 5)
  assert.equal(new Set(started.state.players.map(p => `${p.x},${p.y}`)).size, 5)
  assert.ok(started.state.players.every(p => p.ability === -1))
  console.log('PASS: five clients start together at distinct spawns with concealed powers')
  const sixth = await connect()
  const rejection = waitFor(sixth, data => data.type === 'error')
  send(sixth, { type: 'join', name: 'Sixth', code: room.code })
  assert.match((await rejection).message, /full/)
  console.log('PASS: sixth player is rejected by the server')
  await waitFor(host, data => data.type === 'state' && data.state.time > 3.1)
  for (const peer of peers) send(peer, { type: 'input', hatch: true, dx: 0, dy: 0, aimX: 500, aimY: 350 })
  const hatched = await waitFor(host, data => data.type === 'state' && data.state.players.every(p => p.hatched))
  assert.equal(new Set(hatched.state.players.map(p => p.ability)).size, 5)
  assert.ok(hatched.state.players.every(p => p.ability >= 0 && p.ability <= 9))
  console.log('PASS: all eggs reveal distinct valid abilities across network clients')
  const before = hatched.state.players.find(p => p.id === hatched.you)
  send(host, { type: 'input', dx: 999, dy: 0, aimX: 500, aimY: 350, x: 9999, hp: 9999 })
  const moved = await waitFor(host, data => data.type === 'state' && data.state.players.find(p => p.id === data.you).x > before.x)
  const after = moved.state.players.find(p => p.id === moved.you)
  assert.ok(after.x < before.x + 50)
  assert.equal(after.hp, 100)
  console.log('PASS: client cannot spoof position, health, or movement speed')
  for (const peer of peers.slice(1)) peer.socket.close()
  const ended = await waitFor(host, data => data.type === 'state' && data.state.status === 'finished')
  assert.equal(ended.state.winner, ended.you)
  console.log('PASS: disconnects eliminate players and the last survivor wins')
  const missing = await connect()
  const absent = waitFor(missing, data => data.type === 'error')
  send(missing, { type: 'join', name: 'Lost', code: 'ZZZZZZ' })
  assert.match((await absent).message, /does not exist/)
  console.log('PASS: unknown room code is rejected')
  console.log('\nAll multiplayer protocol checks passed.')
} finally { for (const socket of sockets) socket.close() }
