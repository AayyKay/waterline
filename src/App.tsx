import { useEffect, useMemo, useState } from 'react';
import { Bell, BellRing, Check, ChevronRight, Clock3, Droplets, GlassWater, Minus, Plus, Settings2, Sparkles, Target, Undo2, X } from 'lucide-react';

type Drink = { id: number; amount: number; at: string };
type Settings = { goal: number; interval: number; start: string; end: string; reminders: boolean; weekdays: number[] };
type ModelContextLike = { registerTool: (tool: Record<string, unknown>, options?: { signal?: AbortSignal }) => void | Promise<void> };

const DEFAULT_SETTINGS: Settings = { goal: 80, interval: 60, start: '09:00', end: '17:00', reminders: false, weekdays: [1,2,3,4,5] };
const dayKey = () => new Date().toISOString().slice(0, 10);
const fmtTime = (iso: string) => new Date(iso).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });

function load<T>(key: string, fallback: T): T {
  try { return JSON.parse(localStorage.getItem(key) || '') as T; } catch { return fallback; }
}

function App() {
  const [drinks, setDrinks] = useState<Drink[]>(() => load(`waterline-drinks-${dayKey()}`, []));
  const [settings, setSettings] = useState<Settings>(() => load('waterline-settings', DEFAULT_SETTINGS));
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [customOpen, setCustomOpen] = useState(false);
  const [customAmount, setCustomAmount] = useState(10);
  const [compact, setCompact] = useState(false);
  const [toast, setToast] = useState('');

  const total = drinks.reduce((sum, drink) => sum + drink.amount, 0);
  const pct = Math.min(100, Math.round((total / settings.goal) * 100));
  const remaining = Math.max(0, settings.goal - total);
  const now = new Date();
  const isWorkday = settings.weekdays.includes(now.getDay());
  const [startHour, startMin] = settings.start.split(':').map(Number);
  const [endHour, endMin] = settings.end.split(':').map(Number);
  const start = new Date(now); start.setHours(startHour, startMin, 0, 0);
  const end = new Date(now); end.setHours(endHour, endMin, 0, 0);
  const inWorkHours = isWorkday && now >= start && now <= end;
  const lastDrink = drinks.at(-1);
  const nextReminder = useMemo(() => {
    const base = lastDrink ? new Date(lastDrink.at) : start;
    const next = new Date(base.getTime() + settings.interval * 60_000);
    if (next < now) return now;
    return next;
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [lastDrink?.at, settings.interval, settings.start]);

  useEffect(() => localStorage.setItem(`waterline-drinks-${dayKey()}`, JSON.stringify(drinks)), [drinks]);
  useEffect(() => localStorage.setItem('waterline-settings', JSON.stringify(settings)), [settings]);
  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(''), 2600);
    return () => clearTimeout(timer);
  }, [toast]);
  useEffect(() => {
    const timer = window.setInterval(() => {
      if (!settings.reminders || Notification.permission !== 'granted' || !inWorkHours || total >= settings.goal) return;
      const lastNotified = Number(localStorage.getItem('waterline-last-notified') || 0);
      if (Date.now() >= nextReminder.getTime() && Date.now() - lastNotified > settings.interval * 55_000) {
        new Notification('A small water break?', { body: `${remaining} oz left today. Take a sip, then log it in Waterline.`, icon: '/waterline.svg' });
        localStorage.setItem('waterline-last-notified', String(Date.now()));
      }
    }, 30_000);
    return () => clearInterval(timer);
  }, [settings, inWorkHours, total, nextReminder, remaining]);

  const addDrink = (amount: number) => {
    setDrinks(prev => [...prev, { id: Date.now(), amount, at: new Date().toISOString() }]);
    setToast(`${amount} oz added — nice work.`);
    setCustomOpen(false);
  };

  useEffect(() => {
    const context = (document as Document & { modelContext?: ModelContextLike }).modelContext;
    if (!context?.registerTool) return;
    const lifecycle = new AbortController();
    void Promise.resolve(context.registerTool({
      name: 'log_water',
      title: 'Log water',
      description: 'Log a water drink in ounces and update today’s visible hydration progress.',
      inputSchema: {
        type: 'object',
        properties: { amountOz: { type: 'number', minimum: 1, maximum: 64 } },
        required: ['amountOz'],
        additionalProperties: false,
      },
      annotations: { readOnlyHint: false, untrustedContentHint: false },
      execute(input: unknown) {
        const amount = Number((input as { amountOz?: unknown })?.amountOz);
        if (!Number.isFinite(amount) || amount < 1 || amount > 64) throw new Error('amountOz must be between 1 and 64.');
        const rounded = Math.round(amount * 10) / 10;
        setDrinks(prev => [...prev, { id: Date.now(), amount: rounded, at: new Date().toISOString() }]);
        setToast(`${rounded} oz added — nice work.`);
        return { loggedOz: rounded, date: dayKey() };
      },
    }, { signal: lifecycle.signal })).catch(() => undefined);
    return () => lifecycle.abort();
  }, []);

  const enableReminders = async () => {
    if (!('Notification' in window)) { setToast('Notifications are not supported in this browser.'); return; }
    const permission = await Notification.requestPermission();
    setSettings(s => ({ ...s, reminders: permission === 'granted' }));
    setToast(permission === 'granted' ? 'Workday reminders are on.' : 'Notifications were not enabled.');
  };

  const week = useMemo(() => {
    return Array.from({length: 7}, (_, index) => {
      const date = new Date();
      date.setDate(date.getDate() - (6 - index));
      const key = date.toISOString().slice(0, 10);
      const value = index === 6 ? total : load<Drink[]>(`waterline-drinks-${key}`, []).reduce((sum, drink) => sum + drink.amount, 0);
      return { label: index === 6 ? 'Today' : date.toLocaleDateString([], { weekday: 'narrow' }), value, hit: value >= settings.goal };
    });
  }, [total, settings.goal]);
  const streak = (() => {
    let count = 0;
    for (const day of [...week].reverse()) { if (!day.hit) break; count += 1; }
    return count;
  })();

  if (compact) return (
    <main className="compact-shell">
      <section className="compact-card">
        <div className="compact-top"><Brand /><button className="icon-button" onClick={() => setCompact(false)} aria-label="Exit compact view"><X size={18}/></button></div>
        <div className="compact-progress">
          <ProgressRing percent={pct} size={132} />
          <div><span className="eyebrow">TODAY</span><strong>{total} <small>/ {settings.goal} oz</small></strong><p>{remaining ? `${remaining} oz to go` : 'Goal complete!'}</p></div>
        </div>
        <button className="add-main compact-add" onClick={() => addDrink(12)}><Plus size={18}/> Add 12 oz</button>
        <div className="quick-row compact-quick"><button onClick={() => addDrink(8)}>+ 8 oz</button><button onClick={() => addDrink(16)}>+ 16 oz</button><button onClick={() => setCustomOpen(true)}>Custom</button></div>
        <p className="compact-next"><Bell size={14}/> {settings.reminders ? `Next nudge ${fmtTime(nextReminder.toISOString())}` : 'Reminders are off'}</p>
      </section>
      {customOpen && <CustomDialog value={customAmount} setValue={setCustomAmount} onAdd={() => addDrink(customAmount)} onClose={() => setCustomOpen(false)} />}
      {toast && <div className="toast"><Check size={16}/>{toast}</div>}
    </main>
  );

  return (
    <main className="app-shell">
      <header>
        <Brand />
        <div className="header-actions">
          <button className="quiet-button" onClick={() => setCompact(true)}><Droplets size={17}/> Compact widget</button>
          <button className="icon-button" onClick={() => setSettingsOpen(true)} aria-label="Open settings"><Settings2 size={19}/></button>
        </div>
      </header>

      <section className="status-strip">
        <span className={`status-dot ${inWorkHours ? 'active' : ''}`}/>
        <strong>{inWorkHours ? 'Workday mode is active' : isWorkday ? 'Outside your work hours' : 'Off-duty day'}</strong>
        <span>Reminders only arrive on your schedule.</span>
      </section>

      <div className="dashboard-grid">
        <section className="hero-card">
          <div className="hero-copy">
            <span className="eyebrow">{now.toLocaleDateString([], {weekday:'long'}).toUpperCase()} · DAILY PROGRESS</span>
            <h1>Keep your focus.<br/><em>We’ll remember the water.</em></h1>
            <p>{remaining ? `You’re ${pct}% there. A few calm sips through the afternoon will keep you on pace.` : 'Daily goal reached. Reminders will stay quiet for the rest of the day.'}</p>
          </div>
          <div className="progress-wrap">
            <ProgressRing percent={pct} size={218} />
            <div className="progress-label"><strong>{total}</strong><span>of {settings.goal} oz</span><small>{remaining ? `${remaining} oz left` : 'complete'}</small></div>
          </div>
          <div className="log-zone">
            <button className="add-main" onClick={() => addDrink(12)}><GlassWater size={20}/> Log 12 oz <Plus size={18}/></button>
            <div className="quick-row" aria-label="Quick add water">
              <button onClick={() => addDrink(8)}>+ 8 oz</button>
              <button onClick={() => addDrink(16)}>+ 16 oz</button>
              <button onClick={() => setCustomOpen(true)}>Custom</button>
            </div>
          </div>
        </section>

        <aside className="side-stack">
          <section className="reminder-card">
            <div className="card-heading"><span className="icon-tile"><BellRing size={20}/></span><div><span className="eyebrow">SMART REMINDER</span><h2>{settings.reminders ? 'Next gentle nudge' : 'Stay on your rhythm'}</h2></div></div>
            {settings.reminders ? <><strong className="next-time">{fmtTime(nextReminder.toISOString())}</strong><p>We’ll skip it if you log a drink first, and stop once your goal is met.</p></> : <><strong className="next-time muted-time">Off</strong><p>Enable browser notifications for reminders during your work hours only.</p></>}
            <button className="secondary-button" onClick={settings.reminders ? () => setSettings(s => ({...s, reminders:false})) : enableReminders}>{settings.reminders ? 'Pause reminders' : 'Enable notifications'}<ChevronRight size={16}/></button>
          </section>

          <section className="pace-card">
            <div><span className="eyebrow">TODAY’S PACE</span><h2>{pct >= 60 ? 'Right on track' : 'A little behind'}</h2></div>
            <span className="pace-pill"><Sparkles size={14}/>{pct >= 60 ? 'Steady' : 'Catch up'}</span>
            <div className="pace-line"><span style={{width:`${pct}%`}}/></div>
            <div className="pace-labels"><span>9 AM</span><span>Now</span><span>5 PM</span></div>
          </section>
        </aside>
      </div>

      <div className="lower-grid">
        <section className="history-card">
          <div className="section-title"><div><span className="eyebrow">LAST 7 DAYS</span><h2>Hydration rhythm</h2></div><span className="streak">{Math.max(0,streak)} day streak <span>↗</span></span></div>
          <div className="bars">
            {week.map((day, i) => <div className="bar-col" key={i}><div className="bar-track"><span className={day.hit ? 'hit' : ''} style={{height:`${Math.min(100,(day.value/settings.goal)*100)}%`}}>{day.hit && <Check size={12}/>}</span></div><small>{day.label}</small></div>)}
          </div>
        </section>

        <section className="recent-card">
          <div className="section-title"><div><span className="eyebrow">TODAY</span><h2>Recent sips</h2></div>{drinks.length > 0 && <button className="text-button" onClick={() => setDrinks(d => d.slice(0,-1))}><Undo2 size={14}/> Undo last</button>}</div>
          <div className="drink-list">
            {drinks.length === 0 ? <div className="empty-state"><GlassWater size={23}/><p>Your first glass is one tap away.</p></div> : drinks.slice(-3).reverse().map(drink => <div className="drink-row" key={drink.id}><span className="mini-glass"><Droplets size={16}/></span><div><strong>{drink.amount} oz water</strong><small>{fmtTime(drink.at)}</small></div><Check size={16}/></div>)}
          </div>
        </section>
      </div>

      <footer><p><strong>Waterline</strong> keeps data on this device. Hydration needs vary—follow guidance from your healthcare professional.</p><button onClick={() => setSettingsOpen(true)}>Adjust my goal <Target size={14}/></button></footer>

      {settingsOpen && <SettingsPanel settings={settings} setSettings={setSettings} onClose={() => setSettingsOpen(false)} onEnable={enableReminders}/>} 
      {customOpen && <CustomDialog value={customAmount} setValue={setCustomAmount} onAdd={() => addDrink(customAmount)} onClose={() => setCustomOpen(false)} />}
      {toast && <div className="toast"><Check size={16}/>{toast}</div>}
    </main>
  );
}

function Brand() { return <div className="brand"><span><Droplets size={20}/></span><div>waterline<small>WORKDAY HYDRATION</small></div></div>; }

function ProgressRing({percent, size}:{percent:number;size:number}) {
  const r = 45, c = 2 * Math.PI * r;
  return <svg className="progress-ring" width={size} height={size} viewBox="0 0 110 110" role="img" aria-label={`${percent}% of daily water goal`}><circle className="ring-bg" cx="55" cy="55" r={r}/><circle className="ring-fill" cx="55" cy="55" r={r} strokeDasharray={c} strokeDashoffset={c-(c*percent/100)}/><path className="ring-drop" d="M55 36c-5.5 7.5-11 13.5-11 20.5a11 11 0 0 0 22 0c0-7-5.5-13-11-20.5Z"/></svg>;
}

function SettingsPanel({settings,setSettings,onClose,onEnable}:{settings:Settings;setSettings:(s:Settings)=>void;onClose:()=>void;onEnable:()=>void}) {
  const days = [{n:'S',v:0},{n:'M',v:1},{n:'T',v:2},{n:'W',v:3},{n:'T',v:4},{n:'F',v:5},{n:'S',v:6}];
  return <div className="overlay" onMouseDown={onClose}><aside className="settings-panel" onMouseDown={e=>e.stopPropagation()} aria-label="Hydration settings">
    <div className="panel-head"><div><span className="eyebrow">YOUR ROUTINE</span><h2>Hydration settings</h2></div><button className="icon-button" onClick={onClose} aria-label="Close settings"><X size={18}/></button></div>
    <label className="setting-field"><span><Target size={18}/><span><strong>Daily goal</strong><small>Set the amount that works for you.</small></span></span><div className="stepper"><button onClick={()=>setSettings({...settings,goal:Math.max(24,settings.goal-8)})}><Minus size={15}/></button><strong>{settings.goal} oz</strong><button onClick={()=>setSettings({...settings,goal:settings.goal+8})}><Plus size={15}/></button></div></label>
    <div className="setting-field block"><span><Clock3 size={18}/><span><strong>Work schedule</strong><small>Reminders stay quiet outside these hours.</small></span></span><div className="time-row"><input type="time" value={settings.start} onChange={e=>setSettings({...settings,start:e.target.value})}/><span>to</span><input type="time" value={settings.end} onChange={e=>setSettings({...settings,end:e.target.value})}/></div><div className="day-row">{days.map((day,i)=><button key={i} className={settings.weekdays.includes(day.v)?'selected':''} onClick={()=>setSettings({...settings,weekdays:settings.weekdays.includes(day.v)?settings.weekdays.filter(d=>d!==day.v):[...settings.weekdays,day.v]})}>{day.n}</button>)}</div></div>
    <label className="setting-field"><span><Bell size={18}/><span><strong>Reminder interval</strong><small>Smart reminders reset when you log.</small></span></span><select value={settings.interval} onChange={e=>setSettings({...settings,interval:Number(e.target.value)})}><option value="30">30 min</option><option value="45">45 min</option><option value="60">1 hour</option><option value="90">90 min</option><option value="120">2 hours</option></select></label>
    <div className="notification-callout"><BellRing size={20}/><div><strong>{settings.reminders?'Notifications enabled':'Turn on desk nudges'}</strong><p>{settings.reminders?'Waterline will remind you only when you are due.':'Your browser will ask for permission once.'}</p></div><button onClick={settings.reminders?()=>setSettings({...settings,reminders:false}):onEnable}>{settings.reminders?'Turn off':'Enable'}</button></div>
    <button className="save-button" onClick={onClose}>Save routine</button>
  </aside></div>;
}

function CustomDialog({value,setValue,onAdd,onClose}:{value:number;setValue:(n:number)=>void;onAdd:()=>void;onClose:()=>void}) {
  return <div className="overlay center" onMouseDown={onClose}><div className="custom-dialog" onMouseDown={e=>e.stopPropagation()}><button className="icon-button close-custom" onClick={onClose}><X size={18}/></button><span className="icon-tile big"><GlassWater size={25}/></span><span className="eyebrow">CUSTOM DRINK</span><h2>How much water?</h2><div className="custom-stepper"><button onClick={()=>setValue(Math.max(1,value-1))}><Minus/></button><strong>{value}<small> oz</small></strong><button onClick={()=>setValue(value+1)}><Plus/></button></div><button className="save-button" onClick={onAdd}>Add to today</button></div></div>;
}

export default App;
