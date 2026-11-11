import { useEffect, useState } from 'react';
import { borrarToken, guardarToken, leerToken, ErrorDeApi } from './api.js';
import Panel from './pantallas/Panel.jsx';
import NuevoPedido from './pantallas/NuevoPedido.jsx';
import Detalle from './pantallas/Detalle.jsx';
import Servicios from './pantallas/Servicios.jsx';
import Clientes from './pantallas/Clientes.jsx';
import Publico from './pantallas/Publico.jsx';
import { Aviso } from './ui.jsx';

// Router mínimo: son seis rutas y ninguna necesita parámetros de query.
const RUTAS = [
  { prueba: /^\/$/, pantalla: Panel, titulo: 'Pedidos' },
  { prueba: /^\/nuevo$/, pantalla: NuevoPedido, titulo: 'Nuevo pedido' },
  { prueba: /^\/pedido\/([^/]+)$/, pantalla: Detalle, titulo: 'Pedido' },
  { prueba: /^\/servicios$/, pantalla: Servicios, titulo: 'Servicios' },
  { prueba: /^\/clientes$/, pantalla: Clientes, titulo: 'Clientes' },
  { prueba: /^\/p\/([^/]+)$/, pantalla: Publico, titulo: 'Tu pedido', publica: true },
];

function resolver(ruta) {
  for (const entrada of RUTAS) {
    const coincidencia = ruta.match(entrada.prueba);
    if (coincidencia) return { entrada, parametros: coincidencia.slice(1) };
  }
  return { entrada: RUTAS[0], parametros: [] };
}

export function navegar(ruta) {
  history.pushState({}, '', ruta);
  window.dispatchEvent(new PopStateEvent('popstate'));
}

export function Enlace({ a, children, className }) {
  return (
    <a
      href={a}
      className={className}
      onClick={(evento) => {
        if (evento.metaKey || evento.ctrlKey || evento.shiftKey) return;
        evento.preventDefault();
        navegar(a);
      }}
    >
      {children}
    </a>
  );
}

function usarRuta() {
  const [ruta, setRuta] = useState(window.location.pathname);
  useEffect(() => {
    const alCambiar = () => setRuta(window.location.pathname);
    window.addEventListener('popstate', alCambiar);
    return () => window.removeEventListener('popstate', alCambiar);
  }, []);
  return ruta;
}

function Ingreso({ alEntrar }) {
  const [token, setToken] = useState('');
  const [error, setError] = useState('');

  return (
    <div className="ingreso">
      <form
        onSubmit={(evento) => {
          evento.preventDefault();
          if (!token.trim()) return setError('Pegá el token del panel.');
          guardarToken(token.trim());
          alEntrar();
        }}
      >
        <h1>LavAR</h1>
        <p className="apagado">
          Panel de la lavandería. Pegá el token que configuraste en el servidor
          (<code>LAVAR_ADMIN_TOKEN</code>).
        </p>
        <input
          type="password"
          value={token}
          onChange={(evento) => setToken(evento.target.value)}
          placeholder="token del panel"
          autoFocus
        />
        <button type="submit" className="boton">
          Entrar
        </button>
        <Aviso tipo="error">{error}</Aviso>
      </form>
    </div>
  );
}

export default function App() {
  const ruta = usarRuta();
  const { entrada, parametros } = resolver(ruta);
  const [token, setToken] = useState(leerToken());
  const [errorGlobal, setErrorGlobal] = useState('');

  useEffect(() => {
    setErrorGlobal('');
  }, [ruta]);

  // El cliente que consulta su pedido no tiene token: la vista pública va antes
  // que cualquier chequeo de sesión.
  if (entrada.publica) {
    const Pantalla = entrada.pantalla;
    return (
      <div className="app">
        <Pantalla codigo={parametros[0]} />
      </div>
    );
  }

  if (!token) {
    return <Ingreso alEntrar={() => setToken(leerToken())} />;
  }

  const Pantalla = entrada.pantalla;

  return (
    <div className="app">
      <header className="encabezado">
        <Enlace a="/" className="marca">
          <span className="marca-punto" /> LavAR
        </Enlace>
        <nav className="nav">
          <Enlace a="/" className={ruta === '/' ? 'activo' : ''}>Pedidos</Enlace>
          <Enlace a="/servicios" className={ruta === '/servicios' ? 'activo' : ''}>Servicios</Enlace>
          <Enlace a="/clientes" className={ruta === '/clientes' ? 'activo' : ''}>Clientes</Enlace>
        </nav>
        <div className="acciones">
          <Enlace a="/nuevo" className="boton boton-chico">Nuevo pedido</Enlace>
          <button
            type="button"
            className="boton boton-chico boton-fantasma"
            onClick={() => {
              borrarToken();
              setToken('');
            }}
          >
            Salir
          </button>
        </div>
      </header>

      {errorGlobal && <Aviso tipo="error">{errorGlobal}</Aviso>}

      <main>
        <Pantalla parametros={parametros} alError={(e) => setErrorGlobal(e instanceof ErrorDeApi ? e.message : String(e))} />
      </main>
    </div>
  );
}
