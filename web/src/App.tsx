import { lazy, Suspense, useEffect } from 'react';
import { BrowserRouter, Navigate, Route, Routes, useLocation, useSearchParams } from 'react-router-dom';
import { Layout } from './components/Layout';
import { Spinner } from './components/common';
import { AuthProvider } from './lib/AuthContext';
import { useAuth } from './lib/useAuth';
import { api } from './lib/api';

import { LandingPage } from './pages/LandingPage';
import { CoursesPage } from './pages/CoursesPage';
import { CourseDetailPage } from './pages/CourseDetailPage';
import { PricingPage } from './pages/PricingPage';
import { LoginPage } from './pages/LoginPage';
import { RegisterPage } from './pages/RegisterPage';
import { ConfirmEmailPage, ForgotPasswordPage, ResetPasswordPage } from './pages/AccountRecoveryPages';
import { QuizPage } from './pages/QuizPage';
import { RoadmapPage } from './pages/RoadmapPage';
import { CheckCertificatePage } from './pages/CheckCertificatePage';
import { AccountPage } from './pages/AccountPage';
import { SubscriptionPage } from './pages/SubscriptionPage';
import { InvoicesPage } from './pages/InvoicesPage';
import { PrivacyDataPage } from './pages/PrivacyDataPage';
import { TutoringPage } from './pages/TutoringPage';
import { PacksPage, PackDetailPage } from './pages/PacksPage';
import { CommunityPage } from './pages/CommunityPage';
import { CompaniesPage } from './pages/CompaniesPage';
import { AboutPage } from './pages/AboutPage';
import { FaqPage, SupportPage } from './pages/StaticPages';
import { LegalPage } from './pages/LegalPage';
import { NotFoundPage } from './pages/NotFoundPage';

// El player y el admin cargan bajo demanda: no forman parte del recorrido de compra y
// mantenerlos fuera del bundle inicial es lo que sostiene el objetivo Lighthouse de T-05.
const PlayerPage = lazy(() => import('./pages/PlayerPage').then((m) => ({ default: m.PlayerPage })));
const AdminPage = lazy(() => import('./pages/admin/AdminPage').then((m) => ({ default: m.AdminPage })));
const AffiliatePage = lazy(() =>
  import('./pages/AffiliatePage').then((m) => ({ default: m.AffiliatePage })),
);

export function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <ScrollToTop />
        <ReferralTracker />
        <Suspense fallback={<Spinner />}>
          <Routes>
            {/* El player ocupa toda la ventana: va fuera del Layout a propósito. */}
            <Route
              path="/aprender/:courseSlug/:lessonSlug"
              element={
                <RequireAuth allowAnonymous>
                  <PlayerPage />
                </RequireAuth>
              }
            />

            <Route element={<Layout />}>
              <Route index element={<LandingPage />} />
              <Route path="cursos" element={<CoursesPage />} />
              <Route path="curso/:slug" element={<CourseDetailPage />} />
              <Route path="precios" element={<PricingPage />} />
              <Route path="packs" element={<PacksPage />} />
              <Route path="pack/:slug" element={<PackDetailPage />} />
              <Route path="roadmap" element={<RoadmapPage />} />
              <Route path="nivel/:slug" element={<QuizPage />} />
              <Route path="check-certificate" element={<CheckCertificatePage />} />
              <Route path="check-certificate/:code" element={<CheckCertificatePage />} />

              <Route path="login" element={<LoginPage />} />
              <Route path="registro" element={<RegisterPage />} />
              <Route path="confirmar" element={<ConfirmEmailPage />} />
              <Route path="recuperar" element={<ForgotPasswordPage />} />
              <Route path="restablecer" element={<ResetPasswordPage />} />

              <Route
                path="cuenta"
                element={
                  <RequireAuth>
                    <AccountPage />
                  </RequireAuth>
                }
              />
              <Route
                path="cuenta/suscripcion"
                element={
                  <RequireAuth>
                    <SubscriptionPage />
                  </RequireAuth>
                }
              />
              <Route
                path="cuenta/facturas"
                element={
                  <RequireAuth>
                    <InvoicesPage />
                  </RequireAuth>
                }
              />
              <Route
                path="cuenta/tutorias"
                element={
                  <RequireAuth>
                    <TutoringPage />
                  </RequireAuth>
                }
              />
              <Route
                path="cuenta/datos"
                element={
                  <RequireAuth>
                    <PrivacyDataPage />
                  </RequireAuth>
                }
              />
              <Route
                path="afiliado"
                element={
                  <RequireAuth>
                    <AffiliatePage />
                  </RequireAuth>
                }
              />
              <Route
                path="comunidad"
                element={
                  <RequireAuth allowAnonymous>
                    <CommunityPage />
                  </RequireAuth>
                }
              />

              <Route path="empresas" element={<CompaniesPage />} />
              <Route path="sobre-mi" element={<AboutPage />} />
              <Route path="faq" element={<FaqPage />} />
              <Route path="soporte" element={<SupportPage />} />
              <Route path="legal/:document" element={<LegalPage />} />

              <Route
                path="admin/*"
                element={
                  <RequireAuth requireAdmin>
                    <AdminPage />
                  </RequireAuth>
                }
              />

              <Route path="*" element={<NotFoundPage />} />
            </Route>
          </Routes>
        </Suspense>
      </AuthProvider>
    </BrowserRouter>
  );
}

/** Cambiar de ruta lleva al principio: sin esto se aterriza a media página. */
function ScrollToTop() {
  const { pathname } = useLocation();

  useEffect(() => {
    window.scrollTo(0, 0);
  }, [pathname]);

  return null;
}

/**
 * Registra el clic de referido en cuanto llega alguien con `?ref=` (T-16). La cookie de
 * atribución la fija la API; aquí solo se avisa una vez por carga.
 */
function ReferralTracker() {
  const [searchParams] = useSearchParams();
  const ref = searchParams.get('ref');

  useEffect(() => {
    if (!ref) {
      return;
    }

    void api.post(`/referrals/${encodeURIComponent(ref)}`).catch(() => {
      // Un código inválido o un fallo de red no debe romper la navegación.
    });
  }, [ref]);

  return null;
}

function RequireAuth({
  children,
  requireAdmin = false,
  allowAnonymous = false,
}: {
  children: React.ReactNode;
  requireAdmin?: boolean;
  allowAnonymous?: boolean;
}) {
  const { user, loading, isAdmin } = useAuth();
  const location = useLocation();

  if (loading) {
    return <Spinner label="Comprobando tu sesión…" />;
  }

  if (allowAnonymous) {
    return <>{children}</>;
  }

  if (!user) {
    // Se conserva el destino para volver ahí tras identificarse.
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  }

  if (requireAdmin && !isAdmin) {
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
}
