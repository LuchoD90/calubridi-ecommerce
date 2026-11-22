import { api, esc, errorField, money, onSubmit, options, receipt, statuses, toast } from './ui.js';

const base = '/api/products';
const transitions = {
  pending: ['confirmed', 'canceled'], confirmed: ['production', 'canceled'],
  production: ['ready', 'canceled'], ready: ['delivered'], delivered: [], canceled: []
};
const button = text => `<button class="button" type="submit">${text}</button>`;
const adminNav = `<p class="eyebrow">GESTIÓN DEL ATELIER</p><h2>Panel del taller</h2>
  <nav class="pills" aria-label="Administración"><a class="pill" href="#admin">Ventas y compras</a><a class="pill" href="#admin/catalogo">Catálogo</a><a class="pill" href="#cuenta">Mi cuenta</a></nav>`;
const totalLabel = values => values.length ? values.map(v => money(v.total, v.currency)).join(' · ') : '—';
const localDate = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Argentina/Buenos_Aires' }).format(new Date());

async function loadDashboard(view, context, params = {}) {
  const data = await api('/api/admin/dashboard?' + new URLSearchParams(params));
  if (!context.active()) return;
  view.innerHTML = `${adminNav}<div class="stack admin-section">
    <form class="row" id="period"><label>Desde<input type="date" name="from" required value="${data.from}"></label>
      <label>Hasta<input type="date" name="to" required value="${data.to}"></label>${button('Filtrar')}${errorField}</form>
    <div class="kpis"><div class="panel">Pedidos del período<strong>${data.orderCount}</strong></div>
    <div class="panel">Ventas cobradas<strong>${totalLabel(data.income)}</strong></div>
    <div class="panel">Compras de insumos<strong>${totalLabel(data.spent)}</strong></div></div>
    <div class="pills">${data.statuses.map(s => `<span class="tag">${esc(statuses[s.status])} · ${s.count}</span>`).join('')}</div>
    <h3>Pedidos</h3><div class="stack">${data.orders.length ? data.orders.map(({ receipt: o, email, revision }) =>
      receipt(o, `<p><strong>Contacto:</strong> ${esc(o.customer)} · ${esc(email)} · ${esc(o.phone)}</p>
      <form class="form order-form" data-id="${o.id}" data-revision="${revision}">
      <label>Estado<select name="status">${options([o.status, ...(transitions[o.status] || [])].filter(s => !o.paidAt || s !== 'canceled').map(s => ({ id: s, name: statuses[s] })))}</select></label>
      ${!o.paidAt && o.status !== 'canceled' ? '<label><input type="checkbox" name="markPaid"> Registrar el pago recibido</label>' : ''}
      ${button('Guardar cambios')}${errorField}</form>`)).join('') : '<p class="empty">No hay pedidos en este período.</p>'}</div>
    <div class="row">${data.page > 1 ? '<button class="button secondary" data-page="prev">Anterior</button>' : ''}
      ${data.page * 30 < data.orderCount ? '<button class="button secondary" data-page="next">Siguiente</button>' : ''}</div>
    <div class="two-columns"><div class="panel"><h3>Registrar compra</h3><form class="form" id="purchase">
      <label>Fecha<input name="date" type="date" required value="${localDate()}"></label><label>Proveedor<input name="supplier" maxlength="120" required></label>
      <label>Detalle<input name="description" maxlength="280" required></label>
      <label>Importe<input name="total" type="number" min="0.01" max="999999999" step="0.01" required></label>
      ${button('Registrar compra')}${errorField}</form></div>
    <div class="panel"><h3>Compras registradas</h3>${data.purchaseCount > 100 ? '<p class="muted">Últimas 100 compras del período. Acotá las fechas para consultar otras.</p>' : ''}
      ${data.purchases.length ? `<div class="table-wrap"><table><thead><tr><th>Fecha</th><th>Proveedor y detalle</th><th>Importe</th></tr></thead>
      <tbody>${data.purchases.map(p => `<tr><td>${p.date}</td><td>${esc(p.supplier)}<br>${esc(p.description)}</td><td>${money(p.total, p.currency)}</td></tr>`).join('')}</tbody></table></div>` : '<p class="muted">No hay compras en este período.</p>'}</div></div>
    </div>`;
  const reload = page => loadDashboard(view, context, { from: data.from, to: data.to, page: page || data.page });
  onSubmit(view.querySelector('#period'), fields => loadDashboard(view, context, Object.fromEntries(fields)));
  onSubmit(view.querySelector('#purchase'), async fields => {
    const p = Object.fromEntries(fields);
    p.total = Number(p.total);
    await api('/api/admin/purchases', { method: 'POST', body: p });
    if (p.date < data.from || p.date > data.to)
      await loadDashboard(view, context, { from: p.date, to: p.date });
    else await reload();
    toast('Compra registrada.');
  });
  for (const form of view.querySelectorAll('.order-form')) onSubmit(form, async fields => {
    await api('/api/admin/orders/' + form.dataset.id, { method: 'PATCH', body: {
      status: fields.get('status'), markPaid: fields.has('markPaid'), revision: Number(form.dataset.revision)
    } });
    toast('Pedido actualizado.');
    await reload();
  });
  for (const b of view.querySelectorAll('[data-page]')) b.onclick = () => reload(data.page + (b.dataset.page === 'next' ? 1 : -1)).catch(e => toast(e.message));
}

function productForm(p = {}, categories) {
  return `<form class="form" id="product-form">
    <label>Nombre<input name="name" required maxlength="120" value="${esc(p.name)}"></label>
    <label>Descripción<textarea name="description" maxlength="2500" rows="4">${esc(p.description)}</textarea></label>
    <label>Categoría<select name="categoryId"><option value="">Otros</option>${options(categories, p.categoryId)}</select></label>
    <label>Precio base<input name="price" type="number" min="1" max="999999999" step="0.01" required value="${esc(p.price)}"></label>
    <label>Días de confección<input name="estimatedProductionDays" type="number" min="1" max="365" required value="${esc(p.estimatedProductionDays)}"></label>
    ${p.id ? `<label><input type="checkbox" name="isActive" ${p.isActive ? 'checked' : ''}> Mostrar en la colección</label>` : '<p class="muted">Guardá el diseño para agregar las fotos y publicarlo.</p>'}
    <label>Foto de portada<input name="cover" type="file" accept="image/jpeg,image/png,image/webp"></label>
    ${button(p.id ? 'Guardar diseño' : 'Crear diseño')}${errorField}</form>`;
}

function lookupForm(kind, items) {
  const label = { categories: 'Categoría', fabrics: 'Tela', colors: 'Color' }[kind];
  return `<details class="receipt"><summary>${label}</summary><form class="form lookup" data-kind="${kind}">
    <label>Nombre<input name="name" required maxlength="100"></label>
    ${kind === 'colors' ? '<label>Tono<input name="hexCode" type="color" value="#a55036"></label>' : ''}
    ${button('Agregar')}${errorField}</form>
    <p class="muted">${items.map(i => esc(i.name)).join(' · ')}</p></details>`;
}

async function loadCatalog(view, context, id) {
  const [products, categories, fabrics, colors, variants, videos] = await Promise.all([
    api(base), api('/api/categories'), api('/api/fabrics'), api('/api/colors'),
    id ? api(`${base}/${id}/fabrics`) : [], id ? api(`${base}/${id}/videos`) : []
  ]);
  const product = products.find(p => p.id === id);
  if (id && !product) throw new Error('El diseño no existe.');
  const root = `${base}/${id}`;
  const detailed = await Promise.all(variants.map(async f => ({
    ...f, colors: await api(`${root}/fabrics/${f.id}/colors`)
  })));
  if (!context.active()) return;
  view.innerHTML = `${adminNav}<div class="two-columns admin-section">
    <div class="stack"><div class="panel"><h3>Diseños</h3><a class="button secondary" href="#admin/catalogo">Nuevo diseño</a>
    ${products.map(p => `<div class="line-item row spread"><a href="#admin/catalogo/${p.id}">${esc(p.name)} ↗</a>
      <span class="tag">${p.isActive ? 'Publicado' : 'Borrador'}</span></div>`).join('')}</div>
    <div class="panel stack"><h3>Categorías, telas y colores</h3>${lookupForm('categories', categories)}${lookupForm('fabrics', fabrics)}${lookupForm('colors', colors)}</div></div>
    <div class="stack"><div class="panel"><h3>${product ? esc(product.name) : 'Nuevo diseño'}</h3>${productForm(product, categories)}</div>
    ${product ? `<div class="panel stack"><h3>Telas, colores y fotos</h3>
    <form class="form" id="fabric-form"><label>Tela<select name="fabricId" required><option value="">Elegí una tela</option>${options(fabrics.filter(f => f.isActive && !variants.some(v => v.fabricId === f.id)))}</select></label>
      <label>Precio para esta tela<input name="price" type="number" min="1" max="999999999" step="0.01" required value="${product.price}"></label>
      ${button('Agregar tela')}${errorField}</form>
    ${detailed.map(f => `<details class="receipt"><summary>${esc(f.fabricName)} · ${f.isActive ? 'Visible' : 'Oculta'}</summary>
      <form class="form fabric-edit" data-fabric="${f.id}">
      <label>Precio<input name="price" type="number" min="1" max="999999999" step="0.01" value="${f.price}" required></label>
      <label><input type="checkbox" name="isActive" ${f.isActive ? 'checked' : ''}> Mostrar tela</label>${button('Guardar tela')}${errorField}</form>
      <form class="form color-form" data-fabric="${f.id}">
      <label>Color<select name="colorId" required><option value="">Elegí un color</option>${options(colors.filter(c => c.isActive && !f.colors.some(v => v.colorId === c.id)))}</select></label>
      ${button('Agregar color')}${errorField}</form>
      ${f.colors.map(c => `<form class="form color-edit" data-fabric="${f.id}" data-color="${c.id}">
        <label><input type="checkbox" name="isActive" ${c.isActive ? 'checked' : ''}> Mostrar color ${esc(c.colorName)}</label>
        ${button('Guardar color')}${errorField}</form>
        <form class="form gallery-upload" data-fabric="${f.id}" data-color="${c.id}">
        <label>Fotos · ${esc(c.colorName)}<input type="file" name="file" accept="image/jpeg,image/png,image/webp" required></label>
        ${button('Subir foto')}${errorField}</form>`).join('')}</details>`).join('')}</div>
    <div class="panel"><h3>Videos del diseño</h3><form class="form" id="video">
      <label>Video<input type="file" name="file" accept="video/mp4,video/webm" required></label>${button('Subir video')}${errorField}</form>
    ${videos.map(v => `<div class="line-item row"><video controls preload="none" src="${esc(v.url)}"></video><button class="button secondary" data-video="${v.id}">Eliminar video</button></div>`).join('')}</div>` : ''}</div></div>`;
  const reload = async () => { await context.refresh(); await loadCatalog(view, context, id); };
  for (const form of view.querySelectorAll('.lookup')) onSubmit(form, async fields => {
    await api('/api/' + form.dataset.kind, { method: 'POST', body: Object.fromEntries(fields) });
    await reload();
  });
  onSubmit(view.querySelector('#product-form'), async fields => {
    const input = Object.fromEntries(fields);
    const file = input.cover;
    const body = { name: input.name, description: input.description, price: Number(input.price),
      categoryId: Number(input.categoryId) || null, estimatedProductionDays: Number(input.estimatedProductionDays),
      isActive: fields.has('isActive') };
    const saved = await api(product ? root : base, { method: product ? 'PUT' : 'POST', body });
    if (file.size) {
      const upload = new FormData(); upload.set('file', file);
      try { await api(`/api/admin/products/${saved.id}/cover`, { method: 'POST', body: upload }); }
      catch (e) {
        await context.refresh();
        if (!product) {
          location.hash = 'admin/catalogo/' + saved.id;
          toast('Diseño guardado. La foto quedó pendiente. ' + e.message);
          return;
        }
        throw new Error('Diseño guardado. La foto quedó pendiente. ' + e.message);
      }
    }
    await context.refresh();
    if (!product) location.hash = 'admin/catalogo/' + saved.id;
    else await reload();
    toast('Diseño guardado.');
  });
  if (!id) return;
  onSubmit(view.querySelector('#fabric-form'), async f => {
    await api(root + '/fabrics', { method: 'POST', body: { fabricId: Number(f.get('fabricId')), price: Number(f.get('price')), isActive: true } });
    await reload();
  });
  for (const form of view.querySelectorAll('.fabric-edit, .color-edit, .color-form, .gallery-upload')) onSubmit(form, async f => {
    const route = `${root}/fabrics/${form.dataset.fabric}`;
    if (form.matches('.fabric-edit'))
      await api(route, { method: 'PUT', body: { price: Number(f.get('price')), isActive: f.has('isActive'), isDefault: f.has('isActive') && variants.find(v => v.id === Number(form.dataset.fabric)).isDefault } });
    else if (form.matches('.color-edit'))
      await api(`${route}/colors/${form.dataset.color}`, { method: 'PUT', body: { isActive: f.has('isActive'),
        isDefault: f.has('isActive') && detailed.find(v => v.id === Number(form.dataset.fabric)).colors.find(c => c.id === Number(form.dataset.color)).isDefault } });
    else if (form.matches('.color-form'))
      await api(route + '/colors', { method: 'POST', body: { colorId: Number(f.get('colorId')), isActive: true } });
    else await api(`${route}/colors/${form.dataset.color}/media`, { method: 'POST', body: f });
    await reload();
    toast('Catálogo actualizado.');
  });
  onSubmit(view.querySelector('#video'), async f => {
    await api(root + '/videos', { method: 'POST', body: f });
    await reload();
    toast('Video guardado.');
  });
  for (const b of view.querySelectorAll('[data-video]')) b.onclick = async () => {
    if (!confirm('¿Eliminar este video del catálogo?')) return;
    try { await api(root + '/videos/' + b.dataset.video, { method: 'DELETE' }); await reload(); }
    catch (e) { toast(e.message); }
  };
}

export async function renderAdmin(view, route, context) {
  if (route.startsWith('admin/catalogo')) await loadCatalog(view, context, Number(route.split('/')[2]) || null);
  else await loadDashboard(view, context);
}
