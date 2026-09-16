import { useState } from 'react';
import { Link, NavLink, Outlet } from 'react-router-dom';
import { useAuth } from '../lib/useAuth';
import { splitBrandName, useBranding } from '../lib/useBranding';
import { CookieBanner } from './CookieBanner';
import { IconLogin, IconLogout, IconShield, IconUser } from './icons';
import { ThemeToggle } from './ThemeToggle';
import './layout.css';

const NAV = [
  { to: '/cursos', label: 'Cursos' },
  { to: '/precios', label: 'Precios' },
  { to: '/empresas', label: 'Empresas' },
  { to: '/roadmap', label: 'Roadmap' },
  { to: '/sobre-mi', label: 'Sobre mí' },
  { to: '/comunidad', label: 'Comunidad' },
];

export function Layout() {
  const { user, logout, isAdmin } = useAuth();
  const [menuOpen, setMenuOpen] = useState(false);
  const branding = useBranding();
  const brand = splitBrandName(branding.academyName);

  return (
    <div className="layout">
      <a className="skip-link" href="#main">
        Saltar al contenido
      </a>

      <header className="site-header">
        <div className="container site-header__inner">
          <Link to="/" className="brand" aria-label={`${branding.academyName}, inicio`}>
            {/*
              Con logo, la marca cuadrada sobra. Sin él, se queda la de siempre: un hueco
              vacío donde antes había un cuadrado deja la cabecera coja mientras alguien
              decide qué imagen subir.
            */}
            {branding.logoUrl ? (
              <img src={branding.logoUrl} alt="" className="brand__logo" />
            ) : (
              <span className="brand__mark" aria-hidden="true" />
            )}
            <span className="brand__text">
              {brand.lead && `${brand.lead} `}
              <strong>{brand.tail}</strong>
            </span>
          </Link>

          <button
            type="button"
            className="site-header__toggle"
            aria-expanded={menuOpen}
            aria-controls="nav-principal"
            onClick={() => setMenuOpen((open) => !open)}
          >
            <span className="sr-only">{menuOpen ? 'Cerrar menú' : 'Abrir menú'}</span>
            <span aria-hidden="true">{menuOpen ? '✕' : '☰'}</span>
          </button>

          {/*
            Cualquier clic dentro del menú lo cierra. Se hace por delegación en el nav en
            vez de reaccionar al cambio de ruta con un efecto: navegar a la misma página
            también debe cerrarlo, y así no hay un render en cascada por cada navegación.
          */}
          <nav
            id="nav-principal"
            className={`site-nav ${menuOpen ? 'site-nav--open' : ''}`}
            aria-label="Navegación principal"
            onClick={() => setMenuOpen(false)}
          >
            <ul>
              {NAV.map((item) => (
                <li key={item.to}>
                  <NavLink to={item.to} className={({ isActive }) => (isActive ? 'is-active' : '')}>
                    {item.label}
                  </NavLink>
                </li>
              ))}
            </ul>

            <div className="site-nav__actions">
              {user ? (
                <>
                  {isAdmin && (
                    <Link to="/admin" className="btn btn--ghost btn--sm btn--icon-text site-nav__admin">
                      <IconShield />
                      Admin
                    </Link>
                  )}

                  {/* La cuenta es la acción principal de este grupo y va sólida; salir es
                      destructivo de la sesión y va discreto, para no invitar a pulsarlo. */}
                  <Link to="/cuenta" className="btn btn--ghost btn--sm btn--icon-text">
                    <IconUser />
                    <span className="site-nav__action-label">Mi cuenta</span>
                  </Link>

                  <button
                    type="button"
                    className="btn btn--sm btn--icon site-nav__logout"
                    onClick={() => void logout()}
                    aria-label="Cerrar sesión"
                    title="Cerrar sesión"
                  >
                    <IconLogout />
                  </button>
                </>
              ) : (
                <>
                  <Link to="/login" className="btn btn--ghost btn--sm btn--icon-text">
                    <IconLogin />
                    Entrar
                  </Link>
                  <Link to="/registro" className="btn btn--primary btn--sm">
                    Crear cuenta
                  </Link>
                </>
              )}
            </div>
          </nav>

          {/*
            Fuera del `<nav>` por dos motivos: el nav cierra el menú móvil con cualquier clic
            dentro, y cambiar de tema no es navegar; y así el selector sigue a mano en móvil
            con el menú cerrado, que es donde más falta hace. Va después del nav en el DOM para
            no meterse entre el botón de menú y el menú que abre.
          */}
          <ThemeToggle />
        </div>
      </header>

      <main id="main" className="site-main">
        <Outlet />
      </main>

      <footer className="site-footer">
        <div className="container site-footer__inner">
          <div>
            <p className="site-footer__brand">
              {brand.lead && `${brand.lead} `}
              <strong>{brand.tail}</strong>
            </p>
            <p className="muted site-footer__tagline">{branding.academyTagline}</p>

            {branding.supportEmail && (
              <p className="muted">
                <a href={`mailto:${branding.supportEmail}`}>{branding.supportEmail}</a>
              </p>
            )}
          </div>

          <nav aria-label="Recursos">
            <h2>Aprender</h2>
            <ul>
              <li>
                <Link to="/cursos">Cursos</Link>
              </li>
              <li>
                <Link to="/roadmap">Roadmap</Link>
              </li>
              <li>
                <Link to="/check-certificate">Verificar certificado</Link>
              </li>
              <li>
                <Link to="/faq">Preguntas frecuentes</Link>
              </li>
            </ul>
          </nav>

          <nav aria-label="Legal">
            <h2>Legal</h2>
            <ul>
              <li>
                <Link to="/legal/aviso-legal">Aviso legal</Link>
              </li>
              <li>
                <Link to="/legal/privacidad">Privacidad</Link>
              </li>
              <li>
                <Link to="/legal/cookies">Cookies</Link>
              </li>
              <li>
                <Link to="/legal/terminos">Términos y condiciones</Link>
              </li>
            </ul>
          </nav>

          <nav aria-label="Contacto">
            <h2>Contacto</h2>
            <ul>
              <li>
                <Link to="/soporte">Soporte</Link>
              </li>
              <li>
                <Link to="/empresas">Formación in-company</Link>
              </li>
              {/*
                Los enlaces externos salen de la marca. Estaban escritos a mano apuntando a la
                empresa de siempre: con varias marcas, el pie de una llevaría al LinkedIn de
                otra. Sin configurar, el enlace no se pinta — mejor que mandar a alguien al
                perfil equivocado.
              */}
              {branding.legal.linkedInUrl && (
                <li>
                  <a href={branding.legal.linkedInUrl} rel="noreferrer noopener" target="_blank">
                    LinkedIn
                  </a>
                </li>
              )}

              {branding.legal.companyUrl && (
                <li>
                  <a href={branding.legal.companyUrl} rel="noreferrer noopener" target="_blank">
                    {branding.academyName}
                  </a>
                </li>
              )}
            </ul>
          </nav>
        </div>

        <div className="container site-footer__legal">
          <p className="muted">
            © {new Date().getFullYear()} Inkoova. Todos los derechos reservados.
          </p>
        </div>
      </footer>

      <CookieBanner />
    </div>
  );
}
