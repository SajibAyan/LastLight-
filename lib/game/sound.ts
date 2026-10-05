export class GameSound {
  private context: AudioContext | null = null
  unlock() { try { this.context ??= new AudioContext(); void this.context.resume() } catch { /* Browsers may disable audio in embedded previews. */ } }
  play(kind: 'shoot' | 'hatch' | 'ability' | 'hit') {
    if (!this.context || this.context.state !== 'running') return
    const c = this.context, oscillator = c.createOscillator(), gain = c.createGain()
    const frequencies = { shoot: [480, 140], hatch: [330, 880], ability: [180, 660], hit: [130, 45] }
    const [start, end] = frequencies[kind], duration = kind === 'hatch' ? .6 : .12
    oscillator.type = kind === 'shoot' ? 'triangle' : 'sine'
    oscillator.frequency.setValueAtTime(start, c.currentTime); oscillator.frequency.exponentialRampToValueAtTime(end, c.currentTime + duration)
    gain.gain.setValueAtTime(.055, c.currentTime); gain.gain.exponentialRampToValueAtTime(.001, c.currentTime + duration)
    oscillator.connect(gain); gain.connect(c.destination); oscillator.start(); oscillator.stop(c.currentTime + duration)
    oscillator.onended = () => { oscillator.disconnect(); gain.disconnect() }
  }
  dispose() { if (this.context) void this.context.close(); this.context = null }
}
