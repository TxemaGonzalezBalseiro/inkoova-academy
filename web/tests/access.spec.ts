import { expect, test } from '@playwright/test';

/**
 * Acceso al contenido. Estos son los criterios de aceptación de T-06 y T-07 vistos desde el
 * navegador: la muestra gratuita se abre sin cuenta y la lección de pago no.
 *
 * Los tests se saltan solos si no hay catálogo publicado, en vez de fallar por falta de
 * datos: un fallo debe significar que el código está mal, no que la base está vacía.
 */

type CourseCard = { slug: string; membersOnly: boolean; status: string };

type Lesson = {
  slug: string;
  type: string;
  isFreePreview: boolean;
  hasAccess: boolean;
  contentRef: string | null;
};

type CourseDetail = { slug: string; sections: { lessons: Lesson[] }[] };

test('el catálogo público responde y no incluye borradores', async ({ request }) => {
  const response = await request.get('/api/courses');
  expect(response.ok()).toBeTruthy();

  const courses = (await response.json()) as CourseCard[];
  expect(courses.every((course) => course.status !== 'draft')).toBeTruthy();
});

test('el temario público no expone el contentRef de una lección de pago', async ({ request }) => {
  const courses = (await (await request.get('/api/courses')).json()) as CourseCard[];
  const course = courses.find((c) => c.status === 'published' && c.membersOnly);

  test.skip(!course, 'No hay ningún curso publicado con lecciones de pago.');

  const detail = (await (await request.get(`/api/courses/${course!.slug}`)).json()) as CourseDetail;
  const lessons = detail.sections.flatMap((section) => section.lessons);
  const locked = lessons.filter((lesson) => !lesson.isFreePreview);

  expect(locked.length).toBeGreaterThan(0);

  for (const lesson of locked) {
    expect(lesson.hasAccess, `${lesson.slug} no debería ser accesible sin cuenta`).toBeFalsy();
    expect(lesson.contentRef, `${lesson.slug} filtra su contentRef`).toBeNull();
  }
});

test('una lección de muestra se abre sin cuenta y una de pago redirige a planes', async ({
  page,
  request,
}) => {
  const courses = (await (await request.get('/api/courses')).json()) as CourseCard[];
  const course = courses.find((c) => c.status === 'published');

  test.skip(!course, 'No hay ningún curso publicado.');

  const detail = (await (await request.get(`/api/courses/${course!.slug}`)).json()) as CourseDetail;
  const lessons = detail.sections.flatMap((section) => section.lessons);

  const preview = lessons.find((lesson) => lesson.isFreePreview);
  const locked = lessons.find((lesson) => !lesson.isFreePreview);

  if (preview) {
    await page.goto(`/aprender/${course!.slug}/${preview.slug}`);
    await expect(page.locator('iframe.player__frame, .lab')).toBeVisible();
  }

  if (locked) {
    await page.goto(`/aprender/${course!.slug}/${locked.slug}`);
    await expect(page.getByRole('heading', { name: 'Esta clase es para miembros' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Ver planes' })).toBeVisible();
  }
});

/*
 * Las prácticas de los cursos en HTML se importan como `lab`, pero su contenido es un
 * documento HTML completo, no un Markdown. Tienen que pintarse en el marco como cualquier
 * otra lección: antes el visor las volcaba como texto y el alumno veía el `<!DOCTYPE html>`.
 */
test('una práctica en HTML se pinta en el marco, no como texto en crudo', async ({ page, request }) => {
  const courses = (await (await request.get('/api/courses')).json()) as CourseCard[];

  let target: { course: string; lesson: string } | null = null;

  for (const course of courses.filter((c) => c.status === 'published')) {
    const detail = (await (await request.get(`/api/courses/${course.slug}`)).json()) as CourseDetail;
    const lab = detail.sections
      .flatMap((section) => section.lessons)
      .find((lesson) => lesson.type === 'lab' && lesson.hasAccess && /\.html?$/i.test(lesson.contentRef ?? ''));

    if (lab) {
      target = { course: course.slug, lesson: lab.slug };
      break;
    }
  }

  test.skip(!target, 'No hay ninguna práctica en HTML abierta sin cuenta.');

  await page.goto(`/aprender/${target!.course}/${target!.lesson}`);
  await expect(page.locator('iframe.player__frame')).toBeVisible();
  await expect(page.locator('.lab')).toHaveCount(0);
  await expect(page.getByText('<!DOCTYPE html>')).toHaveCount(0);
});

test('el contenido no se sirve sin un token válido', async ({ request }) => {
  const response = await request.get('/api/content/token-inventado');
  expect(response.status()).toBe(403);
});

test('los endpoints de administración rechazan a quien no ha iniciado sesión', async ({ request }) => {
  const response = await request.get('/api/admin/courses');
  expect(response.status()).toBe(401);
});

test('el panel de afiliado exige sesión', async ({ page }) => {
  await page.goto('/afiliado');
  await expect(page).toHaveURL(/\/login/);
});
