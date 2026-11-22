export const icons = {
  heart: '<path d="M20.8 4.6a5.4 5.4 0 0 0-7.6 0L12 5.8l-1.2-1.2a5.4 5.4 0 0 0-7.6 7.6l1.2 1.2L12 21l7.6-7.6 1.2-1.2a5.4 5.4 0 0 0 0-7.6z"/>',
  user: '<circle cx="12" cy="7" r="3.5"/><path d="M4.5 21v-2a5 5 0 0 1 5-5h5a5 5 0 0 1 5 5v2"/>',
  bag: '<path d="M5 7h14l1.5 14h-17z"/><path d="M9 8V5a3 3 0 0 1 6 0v3"/>'
};
export const icon = name => `<svg viewBox="0 0 24 24" aria-hidden="true">${icons[name]}</svg>`;
export const esc = value => String(value ?? '').replace(/[&<>"']/g, c => ({
  '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
})[c]);
export const media = url => typeof url === 'string' && /^\/uploads\/[a-z0-9_./-]+$/i.test(url) ? esc(url) : '';
export const money = (amount, currency = 'ARS') => new Intl.NumberFormat('es-AR', {
  style: 'currency', currency, maximumFractionDigits: 2
}).format(amount);
export const date = time => new Intl.DateTimeFormat('es-AR', {
  dateStyle: 'medium', timeZone: 'America/Argentina/Buenos_Aires'
}).format(new Date(time));
export const statuses = { pending: 'Recibido', confirmed: 'Confirmado', production: 'En confección', ready: 'Listo', delivered: 'Entregado', canceled: 'Cancelado' };
export const options = (items, selected) => items.map(i =>
  `<option value="${esc(i.id)}"${String(i.id) === String(selected) ? ' selected' : ''}>${esc(i.name)}</option>`).join('');
export const receipt = (o, extra = '') => `<details class="receipt">
  <summary><strong>#${esc(o.id.slice(0, 8).toUpperCase())}</strong> · ${date(o.createdAt)} · ${esc(statuses[o.status])} · ${money(o.total, o.currency)}</summary>
  <ul>${o.lines.map(l => `<li>${l.quantity} × ${esc(l.name)}${l.fabric ? ' · ' + esc(l.fabric) : ''}${l.color ? ' · ' + esc(l.color) : ''} · ${money(l.unitPrice, o.currency)} c/u · ${l.productionDays} días estimados</li>`).join('')}</ul>
  <p><strong>Entrega:</strong> ${esc(o.delivery)}</p><p><strong>Pago:</strong> ${o.paidAt ? 'Registrado' : 'A coordinar'}</p>
  ${o.notes ? `<p><strong>Indicaciones:</strong> ${esc(o.notes)}</p>` : ''}${extra}
</details>`;
export const errorField = '<p class="error" role="alert"></p>';
export let session = {};
export async function refreshSession() {
  session = await api('/api/account/session');
  return session;
}
export async function api(url, { method = 'GET', body } = {}) {
  const headers = {};
  if (method !== 'GET') headers['X-CSRF-TOKEN'] = session.csrf || '';
  const isForm = body instanceof FormData;
  if (body && !isForm) headers['Content-Type'] = 'application/json';
  const res = await fetch(url, { method, headers, body: isForm ? body : JSON.stringify(body) });
  let data = null;
  if (res.headers.get('content-type')?.includes('json')) data = await res.json();
  if (!res.ok) {
    const validation = data?.errors ? Object.values(data.errors).flat().join(' ') : '';
    const error = new Error(data?.message || validation || ({
      400: 'Revisá los datos y volvé a intentar.', 401: 'Iniciá sesión para continuar.',
      403: 'Tu cuenta no tiene permiso para esta acción.', 404: 'No encontramos lo que buscabas.',
      413: 'El archivo supera el tamaño permitido.', 429: 'Esperá un minuto antes de volver a intentar.'
    })[res.status] || 'No pudimos completar la operación. Probá nuevamente.');
    error.status = res.status;
    throw error;
  }
  return data;
}

let toastTimer;
export function toast(message) {
  document.querySelector('#toast').textContent = message;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => document.querySelector('#toast').textContent = '', 4800);
}

export function onSubmit(form, action) {
  form.addEventListener('submit', async event => {
    event.preventDefault();
    const error = form.querySelector('.error');
    const button = event.submitter || form.querySelector('[type=submit],button:not([type])');
    if (button?.disabled) return;
    if (button) button.disabled = true;
    if (error) error.textContent = '';
    try { await action(new FormData(form)); }
    catch (e) {
      if (error) error.textContent = e.message;
      else toast(e.message);
    } finally { if (button) button.disabled = false; }
  });
}
