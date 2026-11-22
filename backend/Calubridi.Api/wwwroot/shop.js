import { mountAccount, unmountPanels } from './assets/panels.js';
import { api, date, errorField, esc, icon, media, money, onSubmit, options, receipt, refreshSession, session, toast } from './ui.js';
import { renderAdmin } from './admin.js';

const home = document.querySelector('#home');
const view = document.querySelector('#view');
const search = document.querySelector('#search');
const filters = document.querySelector('#filters');
const products = document.querySelector('#products');
const homeRoutes = new Set(['', 'inicio', 'atelier', 'catalogo', 'arreglos', 'consulta']);
let catalog = { currency: 'ARS', products: [] };
let category = '';
let favoriteIds = new Set();
let routeVersion = 0;
let returnAfterLogin = '';
const cartStorage = 'calibridi.cart.v1';
let cart = [];
try {
  const stored = JSON.parse(localStorage.getItem(cartStorage));
  if (Array.isArray(stored)) cart = stored.filter(i => i && Number.isInteger(i.productId) && i.quantity >= 1 && i.quantity <= 25);
} catch { /* The cart can still be used for this visit. */ }
let checkoutKey = sessionStorage.getItem('calibridi.checkoutKey');

function updateCart(changed = true) {
  if (changed) {
    checkoutKey = null;
    sessionStorage.removeItem('calibridi.checkoutKey');
  }
  document.querySelector('#bag-count').textContent = cart.reduce((n, i) => n + Number(i.quantity), 0);
  try { localStorage.setItem(cartStorage, JSON.stringify(cart)); } catch { /* Storage can be disabled. */ }
}
updateCart(false);

for (const node of document.querySelectorAll('[data-icon]')) node.insertAdjacentHTML('afterbegin', icon(node.dataset.icon));

for (const link of document.querySelectorAll('[data-whatsapp]')) {
  const message = link.dataset.whatsapp === 'repair'
    ? 'Hola, Calubridi. Quería consultar por un arreglo de costura.'
    : 'Hola, Calubridi. Quería hacer una consulta.';
  const url = new URL(link.href);
  url.searchParams.set('text', message);
  link.href = url.href;
}

const inquiryForm = document.querySelector('#inquiry-form');
onSubmit(inquiryForm, async fields => {
  const success = inquiryForm.querySelector('.inquiry-success');
  success.textContent = '';
  const body = Object.fromEntries(fields);
  const digits = body.phone.replace(/[^0-9]/g, '');
  if (!/^\+?[0-9 ().-]+$/.test(body.phone.trim()) || digits.length < 8 || digits.length > 15)
    throw new Error('Ingresá un teléfono válido con código de área.');
  await api('/api/inquiries', { method: 'POST', body });
  inquiryForm.reset();
  success.textContent = 'Recibimos tu consulta. Te contactaremos al número que dejaste.';
  success.focus();
});


const image = (url, name, eager = false) => media(url)
  ? `<img src="${media(url)}" alt="${esc(name)}" loading="${eager ? 'eager' : 'lazy'}">`
  : '<span class="blank-photo">CALUBRIDI · TELA EN CASA</span>';

function favoriteButton(id) {
  const active = favoriteIds.has(id);
  return `<button class="favorite" type="button" data-favorite="${id}" aria-pressed="${active}" aria-label="${active ? 'Quitar de favoritos' : 'Guardar en favoritos'}">${icon('heart')}</button>`;
}

function card(p) {
  const link = `#producto/${p.id}`;
  return `<article class="product">
    <div class="product-visual"><a href="${link}" aria-label="Ver ${esc(p.name)}">${image(p.cover, p.name)}</a>${favoriteButton(p.id)}</div>
    <div class="product-meta"><h3><a href="${link}">${esc(p.name)}</a></h3>
      <p>${esc(p.category)} · ${p.estimatedProductionDays} días estimados</p><span class="money">Desde ${money(p.price, catalog.currency)}</span>
    </div>
  </article>`;
}

function renderCatalog() {
  const term = search.value.trim().toLocaleLowerCase('es-AR');
  const categories = ['', ...new Set(catalog.products.map(p => p.category))];
  if (!categories.includes(category)) category = '';
  filters.innerHTML = categories.map((c, index) => `<button class="pill" data-category="${index}" aria-pressed="${c === category}">${esc(c || 'Toda la colección')}</button>`).join('');
  const items = catalog.products.filter(p => (!category || p.category === category) &&
    [p.name, p.description, p.category].some(field => field.toLocaleLowerCase('es-AR').includes(term)));
  products.innerHTML = items.length ? items.map(card).join('') : `<p class="empty">${catalog.products.length ?
    'No encontramos diseños con esos filtros.' : 'Pronto vas a encontrar acá los diseños del taller.'}</p>`;
}

filters.addEventListener('click', event => {
  const button = event.target.closest('[data-category]');
  if (!button) return;
  const categories = ['', ...new Set(catalog.products.map(p => p.category))];
  category = categories[Number(button.dataset.category)];
  renderCatalog();
  filters.querySelector(`[data-category="${button.dataset.category}"]`)?.focus();
});
search.addEventListener('input', renderCatalog);

async function syncFavorites() {
  favoriteIds = new Set(session.user ? await api('/api/shop/favorites') : []);
}
document.addEventListener('click', async event => {
  const button = event.target.closest('[data-favorite]');
  if (!button) return;
  if (!session.user) {
    returnAfterLogin = location.hash;
    location.hash = 'cuenta';
    toast('Iniciá sesión para guardar favoritos.');
    return;
  }
  const id = Number(button.dataset.favorite);
  const removing = favoriteIds.has(id);
  button.disabled = true;
  try {
    await api(`/api/shop/favorites/${id}`, { method: removing ? 'DELETE' : 'PUT' });
    if (removing) favoriteIds.delete(id);
    else favoriteIds.add(id);
    renderCatalog();
    if (location.hash === '#favoritos') showFavorites();
    else if (button.isConnected) button.outerHTML = favoriteButton(id);
    toast(removing ? 'Quitado de favoritos.' : 'Guardado en favoritos.');
  } catch (error) { toast(error.message); }
  finally { button.disabled = false; }
});

function showFavorites() {
  const items = catalog.products.filter(p => favoriteIds.has(p.id));
  view.innerHTML = `<h2>Favoritos</h2>${!session.user ? '<p class="empty">Iniciá sesión para consultar tus diseños guardados. <a class="button" href="#cuenta">Mi cuenta</a></p>' :
    `<div class="product-grid">${items.length ? items.map(card).join('') : '<p class="empty">Todavía no guardaste diseños.</p>'}</div>`}`;
}

function showDetail(product) {
  view.innerHTML = `<a class="back" href="#catalogo">← Volver a la colección</a><div class="detail">
    <div><div class="detail-photo" id="detail-photo"></div><div id="gallery" class="gallery"></div>
    ${product.videos.filter(media).map(url => `<video controls preload="metadata" src="${media(url)}"></video>`).join('')}</div>
    <div class="detail-copy"><p class="eyebrow">${esc(product.category)}</p><h2>${esc(product.name)}</h2>
    <div class="row"><span class="tag">Por encargo</span><span class="muted">${product.estimatedProductionDays} días estimados de confección</span></div>
    <p>${esc(product.description)}</p>
    <form class="form" id="add-to-cart">
    ${product.fabrics.length ? `<label>Tela<select name="fabric">${options(product.fabrics)}</select></label><label id="color-label">Color<select name="color"></select></label>` : ''}
    <label>Cantidad<input type="number" name="quantity" value="1" min="1" max="25" required></label>
    <h3 id="detail-price"></h3><div class="row"><button class="button" type="submit">Agregar al carrito ↗</button>${favoriteButton(product.id)}</div>${errorField}
    </form></div></div>`;
  const form = view.querySelector('form');
  const fabricSelect = form.elements.fabric;
  const colorSelect = form.elements.color;
  const selected = () => {
    const fabric = product.fabrics.find(f => f.id === Number(fabricSelect?.value));
    return { fabric, color: fabric?.colors.find(c => c.id === Number(colorSelect?.value)) };
  };
  function updateVariants(reset) {
    const { fabric } = selected();
    if (colorSelect && reset) {
      colorSelect.innerHTML = options(fabric?.colors || []);
      view.querySelector('#color-label').hidden = !fabric?.colors.length;
    }
    const { color } = selected();
    const pictures = [...new Set([...(color?.images || []), product.coverUrl, ...product.fabrics.flatMap(f => f.colors.flatMap(c => c.images))].filter(media))];
    const photo = view.querySelector('#detail-photo');
    photo.innerHTML = image(pictures[0], product.name, true);
    const gallery = view.querySelector('#gallery');
    gallery.innerHTML = pictures.length > 1 ? pictures.map((url, i) =>
      `<button type="button" aria-label="Ver foto ${i + 1}" data-photo="${i}">${image(url, '')}</button>`).join('') : '';
    gallery.onclick = e => {
      const button = e.target.closest('[data-photo]');
      if (button) photo.innerHTML = image(pictures[Number(button.dataset.photo)], product.name, true);
    };
    view.querySelector('#detail-price').textContent = money(fabric?.price ?? product.price, catalog.currency);
  }
  fabricSelect?.addEventListener('change', () => updateVariants(true));
  colorSelect?.addEventListener('change', () => updateVariants(false));
  updateVariants(true);
  onSubmit(form, fields => {
    const { fabric, color } = selected();
    const quantity = Number(fields.get('quantity'));
    const key = [product.id, fabric?.id || '', color?.id || ''].join(':');
    const exists = cart.find(i => i.key === key);
    if ((exists?.quantity || 0) + quantity > 25) throw new Error('Podés encargar hasta 25 unidades de una misma combinación.');
    if (!exists && cart.length >= 30) throw new Error('El carrito admite hasta 30 combinaciones.');
    if (exists) { exists.quantity += quantity; exists.price = fabric?.price ?? product.price; }
    else cart.push({ key, productId: product.id, fabricId: fabric?.id ?? null, colorId: color?.id ?? null,
      name: product.name, fabric: fabric?.name, color: color?.name, quantity, price: fabric?.price ?? product.price });
    updateCart();
    toast('Agregado al carrito.');
  });
}

function showCart() {
  const total = cart.reduce((sum, i) => sum + i.quantity * i.price, 0);
  view.innerHTML = `<a class="back" href="#catalogo">← Seguir explorando</a><h2>Tu carrito</h2>${cart.length ?
    `<div class="two-columns"><div class="panel stack">${cart.map((i, n) => `<div class="line-item">
      <div class="row spread"><a href="#producto/${i.productId}"><strong>${esc(i.name)}</strong></a><strong>${money(i.quantity * i.price, catalog.currency)}</strong></div>
      <p class="muted">${esc([i.fabric, i.color].filter(Boolean).join(' · '))}</p>
      <div class="row"><label>Cantidad <input data-quantity="${n}" type="number" min="1" max="25" value="${i.quantity}"></label><button class="button secondary" data-remove="${n}">Quitar</button></div>
    </div>`).join('')}<p class="row spread"><strong>Total estimado</strong><strong>${money(total, catalog.currency)}</strong></p></div>
    <div class="panel"><h3>Confirmá tu encargo</h3><p class="muted">El taller te contactará para coordinar medidas, pago y entrega.</p>
    ${session.user ? `<form class="form" id="checkout">
      <label>Nombre y apellido<input name="customer" autocomplete="name" required maxlength="120"></label>
      <label>Teléfono<input name="phone" type="tel" autocomplete="tel" required maxlength="40"></label>
      <label>Entrega o retiro<textarea name="delivery" required maxlength="300" placeholder="Indicá si preferís retirar o dónde sería la entrega"></textarea></label>
      <label>Medidas o indicaciones <textarea name="notes" maxlength="1500" placeholder="Contanos cómo querés tu encargo"></textarea></label>
      <button class="button" type="submit">Enviar encargo</button>${errorField}</form>` : '<button class="button" id="login-for-cart">Ingresá para pedir</button>'}
    </div></div>` : '<p class="empty">Tu carrito está vacío. Encontrá tu próximo diseño en la colección.</p>'}`;
  view.querySelectorAll('[data-remove]').forEach(b => b.onclick = () => { cart.splice(Number(b.dataset.remove), 1); updateCart(); showCart(); });
  view.querySelectorAll('[data-quantity]').forEach(input => input.onchange = () => {
    const quantity = Number(input.value);
    if (!Number.isInteger(quantity) || quantity < 1 || quantity > 25) { input.value = cart[Number(input.dataset.quantity)].quantity; return; }
    cart[Number(input.dataset.quantity)].quantity = quantity; updateCart(); showCart();
  });
  const login = view.querySelector('#login-for-cart');
  if (login) login.onclick = () => { returnAfterLogin = '#carrito'; location.hash = 'cuenta'; };
  const form = view.querySelector('#checkout');
  if (form) onSubmit(form, async data => {
    checkoutKey ||= crypto.randomUUID();
    sessionStorage.setItem('calibridi.checkoutKey', checkoutKey);
    const saved = await api('/api/shop/orders', { method: 'POST', body: { ...Object.fromEntries(data), key: checkoutKey,
      items: cart.map(({ productId, fabricId, colorId, quantity, price }) => ({ productId, fabricId, colorId, quantity, expectedUnitPrice: price })) } });
    cart = []; updateCart();
    location.hash = 'cuenta';
    toast('Encargo recibido #' + saved.id.slice(0, 8).toUpperCase());
  });
}

const accessNotices = {
  'email-exists': 'Ese correo ya tiene una cuenta. Ingresá con tu contraseña y vinculá Google desde Mi cuenta.',
  'google-error': 'No pudimos completar el acceso con Google. Volvé a intentar.',
  'google-linked': 'Google está vinculado. La próxima vez podés ingresar con ese Gmail.',
  'link-password': 'La contraseña del sitio no se pudo validar. Revisala o esperá unos minutos si hiciste varios intentos.',
  'link-used': 'Ese Gmail está asociado a otra cuenta de Calubridi. Elegí otro Gmail.',
  'link-error': 'No pudimos vincular Google. Iniciá el vínculo nuevamente desde Mi cuenta.'
};

async function showAccount(version) {
  const result = new URLSearchParams(location.search).get('auth');
  const notice = accessNotices[result] || '';
  if (result) history.replaceState({}, '', '/#cuenta');
  if (!session.user) {
    const account = document.createElement('div');
    view.replaceChildren(account);
    mountAccount(account, {
      google: session.google,
      notice,
      submit: async (mode, data) => {
        await api('/api/account/' + mode, { method: 'POST', body: data });
        await refreshSession(); await syncFavorites(); renderCatalog();
        const target = returnAfterLogin;
        returnAfterLogin = '';
        if (target && target !== location.hash) location.hash = target;
        else if (session.admin) location.hash = 'admin';
        else await route();
      }
    });
    return;
  }
  const orders = await api('/api/shop/orders');
  if (version !== routeVersion) return;
  view.innerHTML = `<div class="section-title"><div><h2>Mis pedidos</h2><p class="muted">Consultá tus encargos y el avance de confección.</p></div>
    <div class="row">${session.admin ? '<a class="button" href="#admin">Panel del taller</a>' : ''}
    <button class="button secondary" id="logout">Cerrar sesión</button></div></div>
    ${notice ? `<p class="access-notice" role="status">${esc(notice)}</p>` : ''}
    ${session.google && !session.googleLinked ? `<details class="receipt account-link"><summary>Vincular Google</summary>
      <p>Confirmá tu contraseña de Calubridi y elegí tu Gmail. El acceso al panel y a tus pedidos se conserva.</p>
      <form class="form" method="post" action="/api/account/google-link">
        <input type="hidden" name="__RequestVerificationToken" value="${esc(session.csrf)}">
        <label>Contraseña de Calubridi<input type="password" name="password" required maxlength="128" autocomplete="current-password"></label>
        <button class="button secondary" type="submit">Continuar con Google</button>
      </form></details>` : ''}
    <div class="stack">${orders.length ? orders.map(o => receipt(o)).join('') : '<p class="empty">Todavía no tenés pedidos.</p>'}</div>`;
  view.querySelector('#logout').onclick = async () => {
    try {
      await api('/api/account/logout', { method: 'POST' });
      await refreshSession(); await syncFavorites(); renderCatalog(); await route();
    } catch (e) { toast(e.message); }
  };
}

async function route() {
  const version = ++routeVersion;
  unmountPanels();
  const routeName = location.hash.slice(1);
  const isHome = homeRoutes.has(routeName);
  home.hidden = !isHome;
  view.hidden = isHome;
  if (isHome) {
    requestAnimationFrame(() => {
      const section = document.getElementById(routeName);
      if (section) section.scrollIntoView();
      else window.scrollTo(0, 0);
    });
    return;
  }
  window.scrollTo(0, 0);
  view.innerHTML = '<p role="status">Cargando…</p>';
  try {
    if (routeName === 'favoritos') showFavorites();
    else if (routeName === 'carrito') showCart();
    else if (routeName === 'cuenta') await showAccount(version);
    else if (routeName.startsWith('admin') && session.admin) await renderAdmin(view, routeName, {
      active: () => version === routeVersion,
      refresh: async () => { catalog = await api('/api/shop/catalog'); renderCatalog(); }
    });
    else if (/^producto\/\d+$/.test(routeName)) {
      const product = await api('/api/shop/catalog/' + routeName.split('/')[1]);
      if (version === routeVersion) showDetail(product);
    } else if (routeName.startsWith('admin')) location.hash = 'cuenta';
    else view.innerHTML = '<h2>Página no encontrada</h2><a class="button" href="#catalogo">Ver colección</a>';
  } catch (e) {
    if (version === routeVersion) view.innerHTML = `<p class="error" role="alert">${esc(e.message)}</p><a class="back" href="#catalogo">← Ver colección</a>`;
  }
}

try {
  [catalog] = await Promise.all([api('/api/shop/catalog'), refreshSession()]);
  if (session.demo) document.querySelector('.ribbon').textContent = 'TIENDA DE PRUEBA · CATÁLOGO Y PRECIOS DE MUESTRA';
  await syncFavorites();
  renderCatalog();
} catch (e) { products.innerHTML = `<p class="empty error">${esc(e.message)} <a href="">Reintentar</a></p>`; }
window.addEventListener('hashchange', route);
await route();
