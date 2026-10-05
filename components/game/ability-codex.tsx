'use client'

import { useState } from 'react'
import { ArrowUpRight, Clock3, Crosshair, Sparkles, Timer } from 'lucide-react'
import { abilities, featuredAbilities, type Ability } from '@/lib/game/abilities'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'

export function AbilityCodex({ full = false }: { full?: boolean }) {
  const [selected, setSelected] = useState<Ability | null>(null)
  const visible = full ? abilities : featuredAbilities.map(id => abilities[id])
  return <>
    <div className={cn('ability-grid', full && 'ability-grid-full')}>
      {visible.map((ability) => <button className={cn('ability-card', `power-${ability.color}`)} key={ability.id} onClick={() => setSelected(ability)} aria-label={`View ${ability.name} ability`}>
        <div className="ability-art">
          <span className="ability-number">{String(ability.id + 1).padStart(2, '0')}</span>
          <ArrowUpRight className="ability-arrow" size={14} />
          <div className="ability-orbit" /><div className="ability-orbit inner" />
          <ability.icon className="power-icon" strokeWidth={1.25} />
          <span className="power-speck speck-one" /><span className="power-speck speck-two" />
        </div>
        <div className="ability-card-copy"><span className="ability-category">{ability.category}</span><h3>{ability.name}</h3><p>{ability.summary}</p><div className="ability-cooldown"><Clock3 size={12} /> {ability.cooldown}s cooldown</div></div>
      </button>)}
    </div>
    <Dialog open={!!selected} onOpenChange={open => !open && setSelected(null)}>
      <DialogContent className="ability-detail-dialog">
        {selected && <><DialogHeader><div className={cn('detail-power-icon', `power-${selected.color}`)}><selected.icon size={36} strokeWidth={1.4} /></div><Badge variant="secondary">{selected.category}</Badge><DialogTitle>{selected.name}</DialogTitle><DialogDescription>{selected.summary}</DialogDescription></DialogHeader>
          <p className="detail-description">{selected.description}</p>
          <div className="ability-stats"><div><Clock3 /><small>Cooldown</small><strong>{selected.cooldown} seconds</strong></div><div><Crosshair /><small>Range</small><strong>{selected.range}</strong></div><div><Timer /><small>Duration</small><strong>{selected.duration}</strong></div></div>
          {selected.passive && <div className="passive-note"><Sparkles size={16} /><div><strong>Passive power</strong><p>{selected.passive}</p></div></div>}
          <p className="muted-footnote">Every egg is unique. No duplicate starting abilities in a match.</p>
        </>}
      </DialogContent>
    </Dialog>
  </>
}
