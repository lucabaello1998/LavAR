import { useEffect, useState } from 'react';
import { api, ErrorDeApi } from '../api.js';
import { Cargando, formatearDinero, formatearFecha } from '../ui.jsx';
import { navegar } from '../App.jsx';

const PASOS = [
  { estado: 'Recibido', texto: 'Recibido', detalle: 'Ya está en la lavandería.' },
  { estado: 'EnProceso', texto: 'En proceso', detalle: 'La estamos lavando.' },
  { estado: 'Listo', texto: 'Listo', detalle: 'Podés pasar a retirarlo.' },
  { estado: 'Entregado', texto: 'Entregado', detalle: '¡Gracias!' },
];

export default function Publico({ codigo }) {
  const [pedido, setPedido] = useState(null);
  const [error, setError] = useState('');
  const [buscado, setBuscado] = useState('');
  const [cargando, setCargando] = useState(true);

  useEffect(() => {
    setCargando(true);
    setError('');
    api
      .pedidoPorCodigo(codigo)
      .then(setPedido)
      .catch((e) => setError(e instanceof ErrorDeApi ? e.message : String(e)))
      .finally(() => setCargando(false));
  }, [codigo]);

  const indice = pedido ? PASOS.findIndex((paso) => paso.estado === pedido.estado) : -1;
  const cancelado = pedido?.estado === 'Cancelado';

  return (
    <div className="publico">
      <header className="publico-encabezado">
        <span className="marca">
          <span className="marca-punto" /> {pedido?.lavanderia ?? 'LavAR'}
        </span>
      </header>

      {cargando && <Cargando texto="Buscando tu pedido…" />}

      {error && !cargando && (
        <div className="publico-error">
          <h1>No encontramos ese código</h1>
          <p className="apagado">{error}</p>
        </div>
      )}

      {pedido && (
        <>
          <section className="publico-codigo">
            <span className="apagado">Tu código</span>
            <strong>{pedido.codigo}</strong>
            <span className="apagado">
              {pedido.cliente ? `${pedido.cliente}, ` : ''}recibido el {formatearFecha(pedido.recibidoEn, { conAnio: true })}
            </span>
          </section>

          {cancelado ? (
            <p className="publico-estado-gigante cancelado">Pedido cancelado</p>
          ) : (
            <ol className="pasos">
              {PASOS.map((paso, i) => (
                <li key={paso.estado} className={i < indice ? 'hecho' : i === indice ? 'actual' : 'pendiente'}>
                  <span className="paso-punto" />
                  <div>
                    <strong>{paso.texto}</strong>
                    <span className="apagado">{paso.detalle}</span>
                  </div>
                </li>
              ))}
            </ol>
          )}

          {pedido.prometidoPara && !cancelado && indice < 2 && (
            <p className="publico-promesa">
              Lo tenés listo el <strong>{formatearFecha(pedido.prometidoPara)}</strong>.
            </p>
          )}

          {pedido.estado === 'Listo' && pedido.retiroADomicilio && (
            <p className="publico-promesa">Pasamos a llevártelo a tu domicilio.</p>
          )}

          <section className="publico-detalle">
            <h2>Qué dejaste</h2>
            <ul>
              {pedido.items.map((item, i) => (
                <li key={i}>
                  <span>{item.descripcion}</span>
                  <span className="apagado">{item.cantidadTexto}</span>
                </li>
              ))}
            </ul>
            <p className="publico-total">
              <span className="apagado">Total</span>
              <strong>{formatearDinero(pedido.total)}</strong>
            </p>
          </section>
        </>
      )}

      <form
        className="publico-buscar"
        onSubmit={(evento) => {
          evento.preventDefault();
          const limpio = buscado.replace(/[\s-]/g, '').toUpperCase();
          if (limpio) navegar(`/p/${limpio}`);
        }}
      >
        <label>
          <span className="apagado">¿Otro pedido? Poné su código</span>
          <div className="publico-buscar-fila">
            <input
              value={buscado}
              onChange={(evento) => setBuscado(evento.target.value)}
              placeholder="K7M2QP"
              autoCapitalize="characters"
              autoComplete="off"
            />
            <button type="submit" className="boton">Buscar</button>
          </div>
        </label>
      </form>

      <footer className="publico-pie apagado">LavAR · el estado de tu ropa, sin llamar a nadie.</footer>
    </div>
  );
}
