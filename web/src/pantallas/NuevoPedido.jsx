import { useEffect, useMemo, useState } from 'react';
import { api, ErrorDeApi } from '../api.js';
import { Aviso, Campo, Cargando, MensajeDeError, formatearDinero } from '../ui.jsx';
import { navegar } from '../App.jsx';

export default function NuevoPedido() {
  const [servicios, setServicios] = useState(null);
  const [error, setError] = useState('');
  const [guardando, setGuardando] = useState(false);

  const [cliente, setCliente] = useState({ nombre: '', telefono: '' });
  const [cantidades, setCantidades] = useState({}); // servicioId -> cantidad string
  const [retiro, setRetiro] = useState(false);
  const [direccion, setDireccion] = useState('');
  const [prometido, setPrometido] = useState('');
  const [notas, setNotas] = useState('');
  const [existente, setExistente] = useState(null);

  useEffect(() => {
    api
      .servicios()
      .then((lista) => setServicios(lista.filter((s) => s.activo)))
      .catch((e) => setError(e instanceof ErrorDeApi ? e.message : String(e)));
  }, []);

  // El backend reutiliza el cliente que ya tiene ese teléfono: avisamos para que
  // no sorprenda que el pedido quede a nombre del cliente viejo.
  useEffect(() => {
    const numero = cliente.telefono.replace(/\D/g, '');
    if (numero.length < 8) {
      setExistente(null);
      return;
    }
    let vigente = true;
    const id = setTimeout(() => {
      api
        .clientes(numero)
        .then((lista) => {
          if (vigente) setExistente(lista.find((c) => (c.telefono ?? '').replace(/\D/g, '').includes(numero)) ?? null);
        })
        .catch(() => {});
    }, 400);
    return () => {
      vigente = false;
      clearTimeout(id);
    };
  }, [cliente.telefono]);

  const elegidos = useMemo(() => {
    if (!servicios) return [];
    return servicios
      .map((servicio) => ({ servicio, cantidad: Number(String(cantidades[servicio.id] ?? '').replace(',', '.')) }))
      .filter((fila) => Number.isFinite(fila.cantidad) && fila.cantidad > 0);
  }, [servicios, cantidades]);

  const total = elegidos.reduce((suma, fila) => suma + fila.cantidad * fila.servicio.precio, 0);

  async function guardar(evento) {
    evento.preventDefault();
    setError('');

    if (!cliente.nombre.trim()) return setError('Escribí el nombre del cliente.');
    if (elegidos.length === 0) return setError('Elegí al menos un servicio con su cantidad.');
    if (retiro && !direccion.trim()) return setError('Para retirar a domicilio hace falta la dirección.');

    setGuardando(true);
    try {
      const pedido = await api.crearPedido({
        clienteNombre: cliente.nombre.trim(),
        clienteTelefono: cliente.telefono.trim() || null,
        retiroADomicilio: retiro,
        direccion: direccion.trim() || null,
        prometidoPara: prometido || null,
        notas: notas.trim() || null,
        items: elegidos.map((fila) => ({ servicioId: fila.servicio.id, cantidad: fila.cantidad })),
      });
      navegar(`/pedido/${pedido.id}`);
    } catch (e) {
      setError(e instanceof ErrorDeApi ? e.message : String(e));
    } finally {
      setGuardando(false);
    }
  }

  return (
    <form className="formulario" onSubmit={guardar}>
      <h1>Nuevo pedido</h1>

      <section className="bloque">
        <h2>Cliente</h2>
        <div className="fila">
          <Campo etiqueta="Nombre">
            <input
              value={cliente.nombre}
              onChange={(evento) => setCliente({ ...cliente, nombre: evento.target.value })}
              placeholder="Ana Gómez"
              autoFocus
            />
          </Campo>
          <Campo etiqueta="Teléfono" ayuda="Con o sin 0 y 15: lo normalizamos para WhatsApp.">
            <input
              value={cliente.telefono}
              onChange={(evento) => setCliente({ ...cliente, telefono: evento.target.value })}
              placeholder="11 5555 1234"
              inputMode="tel"
            />
          </Campo>
        </div>
        {existente && (
          <Aviso tipo="ok">
            Ya tenés a <strong>{existente.nombre}</strong> con ese teléfono: el pedido queda en su ficha.
          </Aviso>
        )}
      </section>

      <section className="bloque">
        <h2>Qué deja</h2>
        {!servicios && <Cargando texto="Cargando servicios…" />}
        <ul className="servicios">
          {(servicios ?? []).map((servicio) => (
            <li key={servicio.id} className="servicio">
              <span className="servicio-nombre">
                {servicio.nombre}
                <span className="apagado"> · {formatearDinero(servicio.precio)} / {servicio.unidad === 'Kilo' ? 'kg' : servicio.unidad.toLowerCase()}</span>
              </span>
              <input
                className="cantidad"
                type="number"
                min="0"
                step={servicio.unidad === 'Kilo' ? '0.1' : '1'}
                value={cantidades[servicio.id] ?? ''}
                onChange={(evento) => setCantidades({ ...cantidades, [servicio.id]: evento.target.value })}
                placeholder="0"
              />
            </li>
          ))}
        </ul>
      </section>

      <section className="bloque">
        <h2>Entrega</h2>
        <div className="fila">
          <Campo etiqueta="Prometido para">
            <input type="date" value={prometido} onChange={(evento) => setPrometido(evento.target.value)} />
          </Campo>
          <label className="campo campo-check">
            <input type="checkbox" checked={retiro} onChange={(evento) => setRetiro(evento.target.checked)} />
            <span>Pasamos a buscarlo</span>
          </label>
        </div>
        {retiro && (
          <Campo etiqueta="Dirección">
            <input value={direccion} onChange={(evento) => setDireccion(evento.target.value)} placeholder="Rivadavia 5000, 3º B" />
          </Campo>
        )}
        <Campo etiqueta="Notas" ayuda="Manchas, instrucciones, lo que haya que recordar en el mostrador.">
          <textarea rows={2} value={notas} onChange={(evento) => setNotas(evento.target.value)} />
        </Campo>
      </section>

      <MensajeDeError mensaje={error} />

      <footer className="pie-formulario">
        <div className="total">
          <span className="apagado">Total</span>
          <strong>{formatearDinero(total)}</strong>
        </div>
        <button type="submit" className="boton" disabled={guardando}>
          {guardando ? 'Guardando…' : 'Crear pedido'}
        </button>
      </footer>
    </form>
  );
}
