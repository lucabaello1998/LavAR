import { useEffect, useState } from 'react';
import { api, ErrorDeApi } from '../api.js';
import { Aviso, Campo, Cargando, MensajeDeError, formatearDinero } from '../ui.jsx';

const UNIDADES = [
  { valor: 'Kilo', texto: 'Por kilo' },
  { valor: 'Unidad', texto: 'Por unidad' },
  { valor: 'Par', texto: 'Por par' },
  { valor: 'Juego', texto: 'Por juego' },
];

export default function Servicios() {
  const [servicios, setServicios] = useState(null);
  const [error, setError] = useState('');
  const [aviso, setAviso] = useState('');
  const [nuevo, setNuevo] = useState({ nombre: '', unidad: 'Unidad', precio: '' });
  const [editando, setEditando] = useState({});

  async function cargar() {
    try {
      setServicios(await api.servicios());
    } catch (e) {
      setError(e instanceof ErrorDeApi ? e.message : String(e));
    }
  }

  useEffect(() => {
    cargar();
  }, []);

  async function guardarPrecio(servicio) {
    const precio = Number(String(editando[servicio.id] ?? '').replace(',', '.'));
    if (!Number.isFinite(precio) || precio <= 0) {
      setError('El precio tiene que ser un número mayor a cero.');
      return;
    }
    try {
      await api.actualizarServicio(servicio.id, {
        nombre: servicio.nombre,
        unidad: servicio.unidad,
        precio,
        activo: servicio.activo,
      });
      setAviso(`"${servicio.nombre}" ahora sale ${formatearDinero(precio)}. Los pedidos viejos no cambian.`);
      setEditando({ ...editando, [servicio.id]: undefined });
      await cargar();
    } catch (e) {
      setError(e instanceof ErrorDeApi ? e.message : String(e));
    }
  }

  async function alternarActivo(servicio) {
    try {
      await api.actualizarServicio(servicio.id, {
        nombre: servicio.nombre,
        unidad: servicio.unidad,
        precio: servicio.precio,
        activo: !servicio.activo,
      });
      await cargar();
    } catch (e) {
      setError(e instanceof ErrorDeApi ? e.message : String(e));
    }
  }

  async function agregar(evento) {
    evento.preventDefault();
    setError('');
    const precio = Number(String(nuevo.precio).replace(',', '.'));
    if (!nuevo.nombre.trim()) return setError('Poné un nombre.');
    if (!Number.isFinite(precio) || precio <= 0) return setError('Poné un precio mayor a cero.');

    try {
      await api.crearServicio({ nombre: nuevo.nombre.trim(), unidad: nuevo.unidad, precio });
      setNuevo({ nombre: '', unidad: 'Unidad', precio: '' });
      setAviso('Servicio agregado.');
      await cargar();
    } catch (e) {
      setError(e instanceof ErrorDeApi ? e.message : String(e));
    }
  }

  return (
    <>
      <h1>Servicios y precios</h1>
      <p className="apagado">
        Cambiar un precio acá no toca los pedidos ya cargados: cada pedido guarda el precio del día en
        que se recibió.
      </p>

      <Aviso tipo="ok">{aviso}</Aviso>
      <MensajeDeError mensaje={error} />

      {!servicios && <Cargando />}

      {servicios && (
        <table className="tabla">
          <thead>
            <tr>
              <th>Servicio</th>
              <th>Se cobra por</th>
              <th className="numero">Precio</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {servicios.map((servicio) => (
              <tr key={servicio.id} className={servicio.activo ? '' : 'apagada'}>
                <td>{servicio.nombre}</td>
                <td className="apagado">{UNIDADES.find((u) => u.valor === servicio.unidad)?.texto ?? servicio.unidad}</td>
                <td className="numero">
                  <input
                    className="input-precio"
                    type="number"
                    min="0"
                    value={editando[servicio.id] ?? servicio.precio}
                    onChange={(evento) => setEditando({ ...editando, [servicio.id]: evento.target.value })}
                  />
                </td>
                <td className="acciones-tabla">
                  {editando[servicio.id] !== undefined && editando[servicio.id] !== String(servicio.precio) && (
                    <button type="button" className="boton boton-chico" onClick={() => guardarPrecio(servicio)}>
                      Guardar
                    </button>
                  )}
                  <button type="button" className="boton boton-chico boton-fantasma" onClick={() => alternarActivo(servicio)}>
                    {servicio.activo ? 'Desactivar' : 'Activar'}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <section className="bloque">
        <h2>Agregar servicio</h2>
        <form className="fila" onSubmit={agregar}>
          <Campo etiqueta="Nombre">
            <input value={nuevo.nombre} onChange={(evento) => setNuevo({ ...nuevo, nombre: evento.target.value })} placeholder="Lavado de cortinas" />
          </Campo>
          <Campo etiqueta="Se cobra por">
            <select value={nuevo.unidad} onChange={(evento) => setNuevo({ ...nuevo, unidad: evento.target.value })}>
              {UNIDADES.map((unidad) => (
                <option key={unidad.valor} value={unidad.valor}>{unidad.texto}</option>
              ))}
            </select>
          </Campo>
          <Campo etiqueta="Precio">
            <input type="number" min="0" value={nuevo.precio} onChange={(evento) => setNuevo({ ...nuevo, precio: evento.target.value })} placeholder="9000" />
          </Campo>
          <button type="submit" className="boton">Agregar</button>
        </form>
      </section>
    </>
  );
}
