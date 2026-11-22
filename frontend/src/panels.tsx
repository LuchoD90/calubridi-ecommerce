import { useState, type FormEvent } from 'react';
import { createRoot } from 'react-dom/client';

type Mode = 'login' | 'register';
type AccessProps = {
  google: boolean;
  notice?: string;
  submit: (mode: Mode, fields: Record<string, FormDataEntryValue>) => Promise<void>;
};

function Access({ google, notice, submit }: AccessProps) {
  const [mode, setMode] = useState<Mode>('login');
  const [error, setError] = useState('');
  const [pending, setPending] = useState(false);
  const [visible, setVisible] = useState(false);
  const register = mode === 'register';

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPending(true);
    setError('');
    try {
      await submit(mode, Object.fromEntries(new FormData(event.currentTarget)));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No pudimos completar el acceso.');
    } finally {
      setPending(false);
    }
  }

  return <div className="access-layout">
    <aside className="access-story" aria-label="Calubridi Tela en casa">
      <img src="/assets/godeco-en-uso.webp" alt="Una máquina Godeco cosiendo tela de lino" />
      <div><span className="eyebrow">CALUBRIDI · TELA EN CASA</span>
        <h3>Tu encargo, puntada a puntada.</h3>
        <p>Guardá tus diseños y seguí el progreso desde tu cuenta.</p>
      </div>
    </aside>
    <section className="access-panel" aria-labelledby="access-title">
      <h2 id="access-title">Mi cuenta</h2>
      {notice && <p className="access-notice" role="status">{notice}</p>}
      <div className="access-switch" role="group" aria-label="Tipo de acceso">
        <button type="button" aria-pressed={!register} disabled={pending} onClick={() => { setMode('login'); setError(''); setVisible(false); }}>Ingresar</button>
        <button type="button" aria-pressed={register} disabled={pending} onClick={() => { setMode('register'); setError(''); setVisible(false); }}>Crear cuenta</button>
      </div>
      {google && <><a className="button secondary google-access" href="/api/account/google">Continuar con Google</a><p className="access-divider">o usá tu correo</p></>}
      <form key={mode} className="form" onSubmit={handleSubmit}>
        {register ? <>
          <label>Correo<input name="email" type="email" required maxLength={254} autoComplete="email" /></label>
          <label>Usuario<input name="username" required minLength={3} maxLength={32} pattern="[A-Za-z0-9_.\-]+" autoComplete="username" /></label>
        </> : <label>Usuario o correo<input name="login" required maxLength={254} autoComplete="username" /></label>}
        <label>Contraseña
          <span className="access-password">
            <input name="password" type={visible ? 'text' : 'password'} required minLength={register ? 12 : undefined} maxLength={128}
              autoComplete={register ? 'new-password' : 'current-password'} aria-describedby={register ? 'password-rules' : undefined} />
            <button type="button" aria-pressed={visible} onClick={() => setVisible(!visible)}>{visible ? 'Ocultar' : 'Mostrar'}</button>
          </span>
        </label>
        {register && <small className="muted" id="password-rules">Al menos 12 caracteres, con mayúscula, minúscula y número.</small>}
        {error && <p className="error" role="alert">{error}</p>}
        <button className="button access-submit" type="submit" disabled={pending}>{pending ? 'Un momento…' : register ? 'Crear cuenta' : 'Iniciar sesión'}</button>
      </form>
    </section>
  </div>;
}

type Stage = { label: string; count: number; key: string };
type DashboardProps = { orders: number; income: string; spent: string; stages: Stage[] };
function Workshop({ orders, income, spent, stages }: DashboardProps) {
  return <div className="workshop-overview">
    <div className="workshop-stages">
      <div className="workshop-title"><h3>Seguimiento de encargos</h3><a href="#admin/consultas">Ver consultas</a></div>
      <div className="workshop-track">{stages.map(({ key, label, count }) =>
        <article key={key} className={'workshop-stage ' + (count > 0 ? 'has-orders' : '')}><span>{label}</span><strong>{count}</strong></article>)}</div>
    </div>
    <div className="kpis workshop-kpis">
      <div className="panel">Encargos del período<strong>{orders}</strong></div>
      <div className="panel">Ventas cobradas<strong>{income}</strong></div>
      <div className="panel">Compras de insumos<strong>{spent}</strong></div>
    </div>
  </div>;
}

const roots = new Map<HTMLElement, ReturnType<typeof createRoot>>();
function mount<T>(element: HTMLElement, view: T, render: (props: T) => React.ReactNode) {
  const root = createRoot(element);
  roots.set(element, root);
  root.render(render(view));
}

export function unmountPanels() {
  for (const root of roots.values()) root.unmount();
  roots.clear();
}
export function mountAccount(element: HTMLElement, props: AccessProps) {
  mount(element, props, p => <Access {...p} />);
}
export function mountWorkshop(element: HTMLElement, props: DashboardProps) {
  // Refreshes of the native dashboard replace its DOM after each save or filter.
  for (const [target, root] of roots) {
    if (!target.isConnected) { root.unmount(); roots.delete(target); }
  }
  mount(element, props, p => <Workshop {...p} />);
}
