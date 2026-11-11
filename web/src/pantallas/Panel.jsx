import { useCallback, useEffect, useState } from 'react';
import { api, ErrorDeApi } from '../api.js';
import { EtiquetaEstado, formatearDinero, formatearFecha, hace, Cargando, MensajeDeError, useDemorado, Aviso } from '../ui.jsx';
import { Enlace, navegar } from '../App.jsx';

const FILTROS = [
  { clave: '', texto: 'Todos' },
  { clave: 'Recibido', texto: 'Recibido' },
  { clave: 'EnProceso', texto: 'En proceso' },
  { clave: 'Listo', texto: 'Listos' },
  { clave: 'Entregado', texto: 'Entregados' },
];

const SIGUIENTE = { Recibido: 'EnProceso', EnProceso: 'Listo', Listo: 'Entregado' };
const TEXTO_SIGUIENTE = {
  Recibido: 'Empezar',
  EnProceso: 'Marcar listo',
  Listo: 'Entregar',
};

export default function Panel() {
  const [filtro, setFiltro] = useState('');
  const [busqueda, setBusqueda] = useState('');
  const textoBuscado = useDemorado(busqueda);
  const [pedidos, setPedidos] = useState(null);
  const [resumen, setResumen] = useState(null);
  const [error, setError] = useState('');
  const [aviso, setAviso] = useState('');
  const [ocupado, setOcupado] = useState(null);

  const cargar = useCallback(async () => {
    setError('');
    try {
      const [lista, datos] = await Promise.all([
        api.pedidos({ estado: filtro, q: textoBuscado }),
        api.resumen(),
      ]);
      setPedidos(lista);
      setResumen(datos);
    } catch (e) {
      setError(e instanceof ErrorDeApi ? e.message : String(e));
    }
  }, [filtro, textoBuscado]);

  useEffect(() => {
    cargar();
  }, [cargar]);

  async function cambiar(pedido, estado) {
    setOcupado(pedido.id);
    setAviso('');
    try {
      await api.cambiarEstado(pedido.id, estado);
      await cargar();
      setAviso(`${pedido.codigo}: ${estado === 'Listo' ? 'listo para retirar' : 'actualizado'}.`);
    } catch (e) {
      setError(e instanceof ErrorDeApi ? e.message : String(e));
    } finally {
      setOcupado(null);
    }
  }

  async function avisar(pedido) {
    try {
      const { whatsapp, mensaje } = await api.aviso(pedido.id);
      if (whatsapp) {
        window.open(whatsapp, '_blank', 'noopener');
      } else {
        await navigator.clipboard.writeText(mensaje);
        setAviso('Sin teléfono válido: el mensaje quedó copiado para pegarlo a mano.');
      }
    } catch (e) {
      setError(e instanceof ErrorDeApi ? e.message : String(e));
    }
  }

  const porEstado = (estado) =>
    resumen?.porEstado?.find((e) => e.estado === estado)?.cantidad ?? 0;

  return (
    <>
      <section className="tarjetas">
        <div className="tarjeta-dato">
          <span className="apagado">Pedidos hoy</span>
          <strong>{resumen?.pedidosHoy ?? '—'}</strong>
        </div>
        <div className="tarjeta-dato">
          <span className="apagado">Facturado hoy</span>
          <strong>{resumen ? formatearDinero(resumen.facturadoHoy) : '—'}</strong>
        </div>
        <div className="tarjeta-dato">
          <span className="apagado">Para retirar</span>
          <strong>{porEstado('Listo')}</strong>
        </div>
        <div className="tarjeta-dato">
          <span className="apagado">En proceso</span>
          <strong>{porEstado('EnProceso') + porEstado('Recibido')}</strong>
        </div>
        <div className="tarjeta-dato">
          <span className="apagado">Clientes</span>
          <strong>{resumen?.clientes ?? '—'}</strong>
        </div>
      </section>

      <div className="barra-filtros">
        <div className="tabs">
          {FILTROS.map((f) => (
            <button
              key={f.clave || 'todos'}
              type="button"
              className={`tab ${filtro === f.clave ? 'activo' : ''}`}
              onClick={() => setFiltro(f.clave)}
            >
              {f.texto}
              {f.clave && <span className="tab-cuenta">{porEstado(f.clave)}</span>}
            </button>
          ))}
        </div>
        <input
          className="buscador"
          type="search"
          placeholder="Código, cliente o teléfono…"
          value={busqueda}
          onChange={(evento) => setBusqueda(evento.target.value)}
        />
      </div>

      <Aviso tipo="ok">{aviso}</Aviso>
      <MensajeDeError mensaje={error} onReintentar={cargar} />

      {!pedidos && !error && <Cargando />}

      {pedidos && pedidos.length === 0 && (
        <p className="vacio">
          No hay pedidos con ese filtro. <Enlace a="/nuevo">Cargá el primero</Enlace>.
        </p>
      )}

      <ul className="lista-pedidos">
        {(pedidos ?? []).map((pedido) => {
          const siguiente = SIGUIENTE[pedido.estado];
          return (
            <li key={pedido.id} className="pedido">
              <div className="pedido-principal">
                <button
                  type="button"
                  className="codigo"
                  title="Abrir el pedido"
                  onClick={() => navegar(`/pedido/${pedido.id}`)}
                >
                  {pedido.codigo}
                </button>
                <div className="pedido-datos">
                  <strong>{pedido.clienteNombre ?? 'Sin cliente'}</strong>
                  <span className="apagado">
                    {pedido.items} {pedido.items === 1 ? 'ítem' : 'ítems'} · {formatearDinero(pedido.total)} ·{' '}
                    {hace(pedido.recibidoEn)}
                    {pedido.prometidoPara && ` · para el ${formatearFecha(pedido.prometidoPara)}`}
                    {pedido.retiroADomicilio && ' · retiro a domicilio'}
                  </span>
                </div>
                <EtiquetaEstado estado={pedido.estado} texto={pedido.estadoEtiqueta} />
              </div>
              <div className="pedido-acciones">
                {siguiente && (
                  <button
                    type="button"
                    className="boton boton-chico"
                    disabled={ocupado === pedido.id}
                    onClick={() => cambiar(pedido, siguiente)}
                  >
                    {TEXTO_SIGUIENTE[pedido.estado]}
                  </button>
                )}
                {pedido.estado === 'Listo' && (
                  <button type="button" className="boton boton-chico boton-fantasma" onClick={() => avisar(pedido)}>
                    Avisar
                  </button>
                )}
                <Enlace a={`/pedido/${pedido.id}`} className="boton boton-chico boton-fantasma">
                  Detalle
                </Enlace>
              </div>
            </li>
          );
        })}
      </ul>
    </>
  );
}
