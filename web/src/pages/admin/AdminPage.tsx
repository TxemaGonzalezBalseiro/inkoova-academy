import { NavLink, Route, Routes } from 'react-router-dom';
import { AdminAuthoring } from './AdminAuthoring';
import { AdminBilling } from './AdminBilling';
import { AdminBranding } from './AdminBranding';
import { AdminEmail } from './AdminEmail';
import { AdminCourses } from './AdminCourses';
import { AdminUsers } from './AdminUsers';
import { AdminMetrics } from './AdminMetrics';
import { AdminImport } from './AdminImport';
import { AdminAffiliates } from './AdminAffiliates';
import { AdminAudit } from './AdminAudit';
import { AdminTutoring } from './AdminTutoring';
import { AdminTutors } from './AdminTutors';
import { AdminIdentities } from './AdminIdentities';
import { AdminInvoicing } from './AdminInvoicing';
import { AdminRoadmap } from './AdminRoadmap';
import { AdminPacks } from './AdminPacks';
import { useDocumentTitle } from '../../hooks/useApi';
import './admin.css';

const TABS = [
  { to: '/admin', label: 'Cursos', end: true },
  { to: '/admin/contenidos', label: 'Gestor de contenidos' },
  { to: '/admin/importar', label: 'Importar contenido' },
  { to: '/admin/itinerario', label: 'Itinerario' },
  { to: '/admin/packs', label: 'Packs' },
  { to: '/admin/planes', label: 'Planes y cobro' },
  { to: '/admin/correo', label: 'Correo' },
  { to: '/admin/identidad', label: 'Identidad' },
  { to: '/admin/marcas', label: 'Marcas' },
  { to: '/admin/tutorias', label: 'Tutorías' },
  { to: '/admin/profesores', label: 'Profesores' },
  { to: '/admin/usuarios', label: 'Usuarios' },
  { to: '/admin/afiliados', label: 'Afiliados' },
  { to: '/admin/facturacion', label: 'Facturación' },
  { to: '/admin/metricas', label: 'Métricas' },
  { to: '/admin/auditoria', label: 'Auditoría' },
];

export function AdminPage() {
  useDocumentTitle('Administración');

  return (
    <div className="container section admin">
      <header className="section-header">
        <h1>Administración</h1>
        <p className="muted">
          Todas las acciones de esta sección quedan registradas con tu usuario y la hora.
        </p>
      </header>

      <nav className="admin__tabs" aria-label="Secciones de administración">
        {TABS.map((tab) => (
          <NavLink
            key={tab.to}
            to={tab.to}
            end={tab.end}
            className={({ isActive }) => `admin__tab ${isActive ? 'is-active' : ''}`}
          >
            {tab.label}
          </NavLink>
        ))}
      </nav>

      <Routes>
        <Route index element={<AdminCourses />} />
        <Route path="contenidos" element={<AdminAuthoring />} />
        <Route path="importar" element={<AdminImport />} />
        <Route path="itinerario" element={<AdminRoadmap />} />
        <Route path="packs" element={<AdminPacks />} />
        <Route path="planes" element={<AdminBilling />} />
        <Route path="correo" element={<AdminEmail />} />
        <Route path="identidad" element={<AdminBranding />} />
        <Route path="marcas" element={<AdminIdentities />} />
        <Route path="tutorias" element={<AdminTutoring />} />
        <Route path="profesores" element={<AdminTutors />} />
        <Route path="usuarios" element={<AdminUsers />} />
        <Route path="afiliados" element={<AdminAffiliates />} />
        <Route path="facturacion" element={<AdminInvoicing />} />
        <Route path="metricas" element={<AdminMetrics />} />
        <Route path="auditoria" element={<AdminAudit />} />
      </Routes>
    </div>
  );
}
