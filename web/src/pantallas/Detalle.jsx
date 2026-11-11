import { useCallback, useEffect, useState } from 'react';
import { api, ErrorDeApi } from '../api.js';
import { Aviso, Cargando, EtiquetaEstado, MensajeDeError, formatearDinero, formatearFecha } from '../ui.jsx';
import { Enlace, navegar } from '../App.jsx';

// Las mismas reglas que aplica la API (Estados.PuedePasar). Acá se usan solo para
// no ofrecer un botón que va a fallar; la autoridad sigue siendo el servidor.
function puedePasar(desde, hasta) {
  if (desde === hasta) return true;
  if (desde === 'Entregado' || desde === 'Cancelado') return false;
  if (hasta === 'Entregado') return desde === 'Listo';
  return true;
}

const ORDEN = ['Recibido', 'EnProceso', 'Listo'];

export default function Detalle({ parametros }) {
  const [id] = parametros;
  const [pedido, setPedido] = useState(null);
  const [error, setError] = useState('');
  const [aviso, setAviso] = useState('');
  const [ocupado, setOcupado] = useState(false);

  const cargar = useCallback(async () => {
    setError('');
    try {
      setPedido(await api.pedido(id));
    } catch (e) {
      setError(e instanceof ErrorDeApi ? e.message : String(e));
    }
  }, [id]);

  useEffect(() => {
    cargar();
  }, [cargar]);

  async function cambiar(estado) {
    setOcupado(true);
    setAviso('');
    try {
      setPedido(await api.cambiarEstado(id, estado));
      setAviso(`Estado actualizado a "${estado}".`);
    } catch (e) {
      setError(e instanceof ErrorDeApi ? e.message : String(e));
    } finally {
      setOcupado(false);
    }
  }

  async function avisar() {
    try {
      const { whatsapp, mensaje } = await api.aviso(id);
      if (whatsapp) {
        window.open(whatsapp, '_blank', 'noopener');
      } else {
        await navigator.clipboard.writeText(mensaje);
        setAviso('El cliente no tiene un teléfono válido: el mensaje quedó copiado.');
      }
    } catch (e) {
      setError(e instanceof ErrorDeApi ? e.message : String(e));
    }
  }

  if (!pedido && !error) return <Cargando />;
  if (!pedido) return <MensajeDeError mensaje={error} onReintentar={cargar} />;

  const enlacePublico = `${window.location.origin}/p/${pedido.codigo}`;

  return (
    <>
      <div className="detalle-encabezado">
        <div>
          <span className="apagado">
            <Enlace a="/">← Pedidos</Enlace>
          </span>
          <h1 className="codigo-grande">{pedido.codigo}</h1>
          <p className="apagado">
            Recibido {formatearFecha(pedido.recibidoEn, { conAnio: true })}
            {pedido.prometidoPara && ` · prometido para el ${formatearFecha(pedido.prometidoPara)}`}
          </p>
        </div>
        <EtiquetaEstado estado={pedido.estado} texto={pedido.estadoEtiqueta} />
      </div>

      <Aviso tipo="ok">{aviso}</Aviso>
      <MensajeDeError mensaje={error} />

      <div className="columnas">
        <section className="bloque">
          <h2>Cliente</h2>
          <p className="dato-grande">{pedido.cliente?.nombre ?? 'Sin cliente'}</p>
          <p className="apagado">{pedido.cliente?.telefono ?? 'Sin teléfono cargado'}</p>
          {pedido.retiroADomicilio && <p className="apagado">Retira a domicilio: {pedido.direccion ?? 'sin dirección'}</p>}
          {pedido.notas && <p className="notas">“{pedido.notas}”</p>}

          <h2>Estado</h2>
          <div className="botones-estado">
            {ORDEN.map((estado) => (
              <button
                key={estado}
                type="button"
                className={`boton boton-chico ${pedido.estado === estado ? 'boton-actual' : ''}`}
                disabled={ocupado || !puedePasar(pedido.estado, estado) || pedido.estado === estado}
                onClick={() => cambiar(estado)}
              >
                {estado === 'Recibido' ? 'Recibido' : estado === 'EnProceso' ? 'En proceso' : 'Listo'}
              </button>
            ))}
          </div>
          <div className="botones-estado">
            <button
              type="button"
              className="boton boton-chico"
              disabled={ocupado || !puedePasar(pedido.estado, 'Entregado')}
              onClick={() => cambiar('Entregado')}
            >
              Entregar
            </button>
            <button
              type="button"
              className="boton boton-chico boton-fantasma"
              disabled={ocupado || !puedePasar(pedido.estado, 'Cancelado')}
              onClick={() => cambiar('Cancelado')}
            >
              Cancelar
            </button>
          </div>
        </section>

        <section className="bloque">
          <h2>Qué dejó</h2>
          <table className="tabla">
            <thead>
              <tr>
                <th>Servicio</th>
                <th>Cantidad</th>
                <th className="numero">Precio</th>
                <th className="numero">Subtotal</th>
              </tr>
            </thead>
            <tbody>
              {pedido.items.map((item) => (
                <tr key={item.id}>
                  <td>{item.descripcion}</td>
                  <td>{item.cantidadTexto}</td>
                  <td className="numero">{formatearDinero(item.precioUnitario)}</td>
                  <td className="numero">{formatearDinero(item.subtotal)}</td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr>
                <th colSpan={3}>Total</th>
                <th className="numero">{formatearDinero(pedido.total)}</th>
              </tr>
            </tfoot>
          </table>

          <h2>El cliente</h2>
          <p className="apagado">Con este código ve el estado sin instalar nada:</p>
          <div className="caja-codigo">
            <code>{enlacePublico}</code>
            <button
              type="button"
              className="boton boton-chico boton-fantasma"
              onClick={async () => {
                await navigator.clipboard.writeText(enlacePublico);
                setAviso('Link copiado. Se lo podés mandar por WhatsApp junto con el aviso.');
              }}
            >
              Copiar link
            </button>
            <button type="button" className="boton boton-chico boton-fantasma" onClick={() => navegar(`/p/${pedido.codigo}`)}>
              Ver como el cliente
            </button>
          </div>
          <button type="button" className="boton" onClick={avisar}>
            Avisar por WhatsApp
          </button>
        </section>
      </div>
    </>
  );
}
