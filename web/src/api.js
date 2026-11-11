const CLAVE_TOKEN = 'lavar.token';

export const leerToken = () => localStorage.getItem(CLAVE_TOKEN) ?? '';
export const guardarToken = (token) => localStorage.setItem(CLAVE_TOKEN, token);
export const borrarToken = () => localStorage.removeItem(CLAVE_TOKEN);

export class ErrorDeApi extends Error {
  constructor(mensaje, estado) {
    super(mensaje);
    this.estado = estado;
  }
}

async function pedir(ruta, { metodo = 'GET', cuerpo, publico = false } = {}) {
  const cabeceras = { Accept: 'application/json' };
  if (cuerpo !== undefined) cabeceras['Content-Type'] = 'application/json';
  if (!publico) cabeceras['X-Admin-Token'] = leerToken();

  let respuesta;
  try {
    respuesta = await fetch(ruta, {
      method: metodo,
      headers: cabeceras,
      body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
    });
  } catch {
    throw new ErrorDeApi('No se pudo hablar con la API. ¿Está levantada?', 0);
  }

  if (respuesta.status === 204) return null;

  const texto = await respuesta.text();
  const datos = texto ? JSON.parse(texto) : null;

  if (!respuesta.ok) {
    throw new ErrorDeApi(datos?.error ?? `Error ${respuesta.status}`, respuesta.status);
  }

  return datos;
}

export const api = {
  // público
  pedidoPorCodigo: (codigo) => pedir(`/api/pedidos/${encodeURIComponent(codigo)}`, { publico: true }),
  serviciosPublicos: () => pedir('/api/servicios', { publico: true }),
  salud: () => pedir('/api/salud', { publico: true }),

  // panel
  resumen: () => pedir('/api/admin/resumen'),
  pedidos: ({ estado, q } = {}) => {
    const parametros = new URLSearchParams();
    if (estado) parametros.set('estado', estado);
    if (q) parametros.set('q', q);
    const cola = parametros.toString();
    return pedir(`/api/admin/pedidos${cola ? `?${cola}` : ''}`);
  },
  pedido: (id) => pedir(`/api/admin/pedidos/${id}`),
  crearPedido: (datos) => pedir('/api/admin/pedidos', { metodo: 'POST', cuerpo: datos }),
  cambiarEstado: (id, estado) => pedir(`/api/admin/pedidos/${id}/estado`, { metodo: 'POST', cuerpo: { estado } }),
  aviso: (id) => pedir(`/api/admin/pedidos/${id}/aviso`),

  clientes: (q) => pedir(`/api/admin/clientes${q ? `?q=${encodeURIComponent(q)}` : ''}`),
  crearCliente: (datos) => pedir('/api/admin/clientes', { metodo: 'POST', cuerpo: datos }),

  servicios: () => pedir('/api/admin/servicios'),
  crearServicio: (datos) => pedir('/api/admin/servicios', { metodo: 'POST', cuerpo: datos }),
  actualizarServicio: (id, datos) => pedir(`/api/admin/servicios/${id}`, { metodo: 'PATCH', cuerpo: datos }),
};
