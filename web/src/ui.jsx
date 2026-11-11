import { useEffect, useState } from 'react';

// --- formato -----------------------------------------------------------------

const dinero = new Intl.NumberFormat('es-AR', {
  style: 'currency',
  currency: 'ARS',
  maximumFractionDigits: 0,
});

export const formatearDinero = (valor) => dinero.format(Number(valor) || 0);

export const formatearFecha = (iso, { conAnio = false } = {}) => {
  if (!iso) return '';
  const fecha = new Date(iso);
  if (Number.isNaN(fecha.getTime())) return '';
  return fecha.toLocaleDateString('es-AR', {
    day: '2-digit',
    month: '2-digit',
    ...(conAnio ? { year: 'numeric' } : {}),
  });
};

export const hace = (iso) => {
  if (!iso) return '';
  const minutos = Math.round((Date.now() - new Date(iso).getTime()) / 60000);
  if (minutos < 1) return 'recién';
  if (minutos < 60) return `hace ${minutos} min`;
  const horas = Math.round(minutos / 60);
  if (horas < 24) return `hace ${horas} h`;
  const dias = Math.round(horas / 24);
  return `hace ${dias} d`;
};

// --- piezas de UI ------------------------------------------------------------

export function Cargando({ texto = 'Cargando…' }) {
  return <p className="aviso">{texto}</p>;
}

export function MensajeDeError({ mensaje, onReintentar }) {
  return (
    <div className="error">
      <span>{mensaje}</span>
      {onReintentar && (
        <button type="button" className="boton boton-chico" onClick={onReintentar}>
          Reintentar
        </button>
      )}
    </div>
  );
}

const CLASES_ESTADO = {
  Recibido: 'estado-recibido',
  EnProceso: 'estado-proceso',
  Listo: 'estado-listo',
  Entregado: 'estado-entregado',
  Cancelado: 'estado-cancelado',
};

export function EtiquetaEstado({ estado, texto }) {
  return <span className={`chip ${CLASES_ESTADO[estado] ?? ''}`}>{texto ?? estado}</span>;
}

export function Campo({ etiqueta, ayuda, children }) {
  return (
    <label className="campo">
      <span className="campo-etiqueta">{etiqueta}</span>
      {children}
      {ayuda && <span className="campo-ayuda">{ayuda}</span>}
    </label>
  );
}

export function Aviso({ tipo = 'ok', children }) {
  if (!children) return null;
  return <div className={`aviso aviso-${tipo}`}>{children}</div>;
}

/** Debounce simple para los buscadores. */
export function useDemorado(valor, demora = 350) {
  const [demorado, setDemorado] = useState(valor);
  useEffect(() => {
    const id = setTimeout(() => setDemorado(valor), demora);
    return () => clearTimeout(id);
  }, [valor, demora]);
  return demorado;
}
