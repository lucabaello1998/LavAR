import { useEffect, useState } from 'react';
import { api, ErrorDeApi } from '../api.js';
import { Aviso, Campo, Cargando, MensajeDeError, formatearFecha, useDemorado } from '../ui.jsx';

export default function Clientes() {
  const [clientes, setClientes] = useState(null);
  const [error, setError] = useState('');
  const [aviso, setAviso] = useState('');
  const [busqueda, setBusqueda] = useState('');
  const texto = useDemorado(busqueda);
  const [nuevo, setNuevo] = useState({ nombre: '', telefono: '', notas: '' });

  async function cargar() {
    try {
      setClientes(await api.clientes(texto));
    } catch (e) {
      setError(e instanceof ErrorDeApi ? e.message : String(e));
    }
  }

  useEffect(() => {
    cargar();
  }, [texto]);

  async function agregar(evento) {
    evento.preventDefault();
    setError('');
    if (!nuevo.nombre.trim()) return setError('El cliente necesita un nombre.');
    try {
      await api.crearCliente({
        nombre: nuevo.nombre.trim(),
        telefono: nuevo.telefono.trim() || null,
        notas: nuevo.notas.trim() || null,
      });
      setNuevo({ nombre: '', telefono: '', notas: '' });
      setAviso('Cliente agregado.');
      await cargar();
    } catch (e) {
      setError(e instanceof ErrorDeApi ? e.message : String(e));
    }
  }

  return (
    <>
      <h1>Clientes</h1>
      <div className="barra-filtros">
        <input
          className="buscador"
          type="search"
          placeholder="Buscar por nombre o teléfono…"
          value={busqueda}
          onChange={(evento) => setBusqueda(evento.target.value)}
        />
      </div>

      <Aviso tipo="ok">{aviso}</Aviso>
      <MensajeDeError mensaje={error} />

      {!clientes && <Cargando />}

      {clientes && clientes.length === 0 && <p className="vacio">Todavía no hay clientes cargados.</p>}

      {clientes && clientes.length > 0 && (
        <table className="tabla">
          <thead>
            <tr>
              <th>Cliente</th>
              <th>Teléfono</th>
              <th className="numero">Pedidos</th>
              <th className="numero">En curso</th>
              <th>Alta</th>
            </tr>
          </thead>
          <tbody>
            {clientes.map((cliente) => (
              <tr key={cliente.id}>
                <td>{cliente.nombre}</td>
                <td className="apagado">{cliente.telefono ?? '—'}</td>
                <td className="numero">{cliente.pedidos}</td>
                <td className="numero">{cliente.enCurso}</td>
                <td className="apagado">{formatearFecha(cliente.creadoEn, { conAnio: true })}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <section className="bloque">
        <h2>Agregar cliente</h2>
        <form className="fila" onSubmit={agregar}>
          <Campo etiqueta="Nombre">
            <input value={nuevo.nombre} onChange={(evento) => setNuevo({ ...nuevo, nombre: evento.target.value })} placeholder="Ana Gómez" />
          </Campo>
          <Campo etiqueta="Teléfono">
            <input value={nuevo.telefono} onChange={(evento) => setNuevo({ ...nuevo, telefono: evento.target.value })} placeholder="11 5555 1234" inputMode="tel" />
          </Campo>
          <Campo etiqueta="Notas">
            <input value={nuevo.notas} onChange={(evento) => setNuevo({ ...nuevo, notas: evento.target.value })} placeholder="Timbre 3B, deja la ropa en portería" />
          </Campo>
          <button type="submit" className="boton">Agregar</button>
        </form>
      </section>
    </>
  );
}
