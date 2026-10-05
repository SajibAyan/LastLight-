'use client'

import { useEffect, useRef, useState } from 'react'
import { Globe2, Loader2, Radio, Server } from 'lucide-react'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Field, FieldDescription, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import type { GameSession, RoomSnapshot } from '@/lib/game/types'

export function normalizeServer(value: string) {
  const url = new URL(value.trim())
  if (url.protocol === 'https:') url.protocol = 'wss:'
  if (url.protocol === 'http:' && ['localhost', '127.0.0.1'].includes(url.hostname)) url.protocol = 'ws:'
  if (url.protocol !== 'wss:' && !(url.protocol === 'ws:' && ['localhost', '127.0.0.1'].includes(url.hostname))) throw new Error('Use a secure wss:// or https:// server address.')
  if (url.username || url.password || url.hash) throw new Error('Use a server URL without credentials or fragments.')
  if (url.pathname === '/') url.pathname = '/ws'
  url.search = ''
  return url.toString()
}

export function OnlineDialog({ open, onOpenChange, name, server, setServer, initialCode, onConnected }: { open: boolean; onOpenChange: (open: boolean) => void; name: string; server: string; setServer: (value: string) => void; initialCode: string; onConnected: (session: GameSession) => void }) {
  const [mode, setMode] = useState(initialCode ? 'join' : 'create')
  const [code, setCode] = useState(initialCode)
  const [error, setError] = useState('')
  const [connecting, setConnecting] = useState(false)
  const pending = useRef<WebSocket | null>(null)
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null)
  useEffect(() => { if (initialCode) { setMode('join'); setCode(initialCode) } }, [initialCode])
  useEffect(() => { if (!open) { pending.current?.close(); pending.current = null; if (timer.current) clearTimeout(timer.current); setConnecting(false) } }, [open])
  useEffect(() => () => { pending.current?.close(); if (timer.current) clearTimeout(timer.current) }, [])
  function connect() {
    setError('')
    if (!name.trim()) { setError('Choose a player name before joining.'); return }
    if (mode === 'join' && !/^[A-Z0-9]{6}$/.test(code)) { setError('Enter the six-character room code.'); return }
    try {
      const endpoint = normalizeServer(server)
      setServer(endpoint)
      setConnecting(true)
      const socket = new WebSocket(endpoint)
      pending.current = socket
      const fail = (message: string) => { if (pending.current !== socket) return; pending.current = null; if (timer.current) clearTimeout(timer.current); socket.close(); setConnecting(false); setError(message) }
      timer.current = setTimeout(() => fail('The server did not respond. Check the address and confirm the C# server is running.'), 10000)
      socket.onopen = () => socket.send(JSON.stringify({ type: mode, name: name.trim().slice(0, 18), code }))
      socket.onerror = () => fail('Could not connect. The C# game server must be hosted with secure WebSockets enabled.')
      socket.onclose = () => fail('The connection closed before the room was ready. Please try again.')
      socket.onmessage = event => {
        try {
          const message = JSON.parse(event.data)
          if (message.type === 'error') { fail(message.message); return }
          if (message.type === 'state') {
            if (timer.current) clearTimeout(timer.current)
            pending.current = null
            socket.onmessage = null; socket.onerror = null; socket.onclose = null
            setConnecting(false)
            onConnected({ mode: 'online', socket, snapshot: message as RoomSnapshot })
            onOpenChange(false)
          }
        } catch { fail('The server returned an invalid response.') }
      }
    } catch (e) { setConnecting(false); setError(e instanceof Error ? e.message : 'Enter a valid server address.') }
  }
  return <Dialog open={open} onOpenChange={onOpenChange}><DialogContent className="online-dialog"><DialogHeader><div className="dialog-emblem"><Globe2 size={25} /></div><DialogTitle>Your friends. Your arena.</DialogTitle><DialogDescription>One private link. Five players. No account needed.</DialogDescription></DialogHeader>
    <Tabs value={mode} onValueChange={v => { setMode(String(v)); setError('') }}><TabsList className="w-full"><TabsTrigger value="create" disabled={connecting}>Create a room</TabsTrigger><TabsTrigger value="join" disabled={connecting}>Join a room</TabsTrigger></TabsList><TabsContent value="create"><p className="online-intro">Create a private lobby, then send the invite link to four friends. The match starts when all five have joined.</p></TabsContent><TabsContent value="join"><Field className="mt-4"><FieldLabel htmlFor="room-code">Room code</FieldLabel><Input id="room-code" placeholder="ABC123" value={code} maxLength={6} disabled={connecting} onChange={e => setCode(e.target.value.toUpperCase().replace(/[^A-Z0-9]/g, ''))} /></Field></TabsContent></Tabs>
    <FieldGroup><Field><FieldLabel htmlFor="server-address"><Server size={14} /> C# game server</FieldLabel><Input id="server-address" type="url" placeholder="wss://your-game-server.com/ws" value={server} disabled={connecting} onChange={e => setServer(e.target.value)} /><FieldDescription>The included ASP.NET Core server needs a WebSocket-capable host, such as Railway, Render, or a VPS. Deploy its Dockerfile, then paste the public address here. Vercel hosts the website, not the C# server.</FieldDescription></Field></FieldGroup>
    {error && <p role="alert" className="form-error">{error}</p>}
    <Button size="lg" onClick={connect} disabled={connecting || !server.trim()}>{connecting ? <Loader2 className="animate-spin" data-icon="inline-start" /> : <Radio data-icon="inline-start" />}{connecting ? 'Connecting…' : mode === 'create' ? 'Create private room' : 'Join room'}</Button>
    <p className="muted-footnote">Want to play right now? Solo practice works without a server.</p>
  </DialogContent></Dialog>
}
