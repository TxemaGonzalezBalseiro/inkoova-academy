/** Contratos que devuelve la API. Espejo de los DTO de Application. */

export type CourseCard = {
  slug: string;
  title: string;
  shortDescription: string;
  coverImageUrl: string | null;
  status: 'draft' | 'published' | 'comingsoon';
  level: 'intro' | 'intermediate' | 'advanced';
  isFeatured: boolean;
  isNew: boolean;
  hours: number;
  lessonCount: number;
  sectionCount: number;
  /** Null hasta que la beta cerrada aporte valoraciones reales (T-15). */
  rating: number | null;
  ratingCount: number | null;
  membersOnly: boolean;
};

export type Lesson = {
  slug: string;
  title: string;
  type: 'slides' | 'video' | 'lab' | 'quiz' | 'download';
  durationMinutes: number;
  isFreePreview: boolean;
  isRequired: boolean;
  hasAccess: boolean;
  /** Ausente cuando no hay acceso: la API no lo serializa. */
  contentRef: string | null;
};

export type Section = {
  title: string;
  order: number;
  lessons: Lesson[];
};

export type CourseProgress = {
  completedLessons: number;
  totalRequiredLessons: number;
  percentComplete: number;
  nextLessonSlug: string | null;
  nextLessonTitle: string | null;
  isComplete: boolean;
  certificateCode: string | null;
  /** Curso completo, sin certificado todavía y con acceso a todo lo que dio por completado. */
  canIssueCertificate: boolean;
};

export type CourseDetail = {
  slug: string;
  title: string;
  shortDescription: string;
  longDescription: string;
  coverImageUrl: string | null;
  status: string;
  level: string;
  hours: number;
  lessonCount: number;
  sectionCount: number;
  isNew: boolean;
  isFeatured: boolean;
  sections: Section[];
  admissionQuizSlug: string | null;
  progress: CourseProgress | null;
};

export type Plan = {
  code: string;
  name: string;
  interval: 'monthly' | 'quarterly' | 'biannual' | 'yearly' | 'lifetime';
  price: number;
  currency: string;
  /** Solo lo que NO es un producto: Discord, sesiones, soporte. Los cursos salen aparte. */
  benefits: string[];
  includesPacks: boolean;
  displayOrder: number;
  includesAllCourses: boolean;
  includesAllPacks: boolean;
  includedProducts: PlanProduct[];
};

/** Un curso o pack concreto que entra en un plan. */
export type PlanProduct = { slug: string; title: string; kind: 'course' | 'pack' };

export type Pack = {
  slug: string;
  title: string;
  sector: string;
  version: string;
  changelog: string | null;
  regulatoryCheckDate: string | null;
  price: number | null;
  currency: string | null;
  hasAccess: boolean;
  accessReason: string;
  files: { id: string; fileName: string; sizeInBytes: number; order: number }[];
};

export type Me = {
  id: string;
  email: string;
  displayName: string;
  emailConfirmed: boolean;
  roles: string[];
  isAffiliate: boolean;
};

export type PlayerLesson = {
  lessonId: string;
  slug: string;
  title: string;
  type: string;
  durationMinutes: number;
  contentToken: string;
  contentTokenSecondsToLive: number;
  /** Ancla dentro del documento, sin almohadilla. Null si la lección es un fichero entero. */
  contentFragment: string | null;
  previousLessonSlug: string | null;
  nextLessonSlug: string | null;
  lastPositionRef: string | null;
  completed: boolean;
};

export type QuizQuestion = {
  id: string;
  category: string;
  text: string;
  options: { index: number; text: string }[];
};

export type Quiz = {
  slug: string;
  title: string;
  kind: 'admission' | 'blockcheck';
  questions: QuizQuestion[];
};

export type QuizResult = {
  score: number;
  total: number;
  verdict: 'ready' | 'almost' | 'comebacklater';
  headline: string;
  recommendation: string;
  byCategory: { category: string; correct: number; total: number }[];
  feedback: { questionId: string; wasCorrect: boolean; correctIndexes: number[]; explanation: string }[];
};

export type RoadmapNode = {
  key: string;
  title: string;
  description: string;
  order: number;
  courseSlug: string | null;
  prerequisites: string[];
  state: 'locked' | 'available' | 'in_progress' | 'completed';
};

export type CertificateVerification = {
  isValid: boolean;
  studentName: string | null;
  subject: string | null;
  issuedOn: string | null;
  revocationReason: string | null;
};

export type MySubscription = {
  planCode: string | null;
  planName: string | null;
  status: string;
  currentPeriodEnd: string | null;
  cancelAtPeriodEnd: boolean;
  includesPacks: boolean;
  ownedProductSlugs: string[];
};

export type AffiliateDashboard = {
  code: string;
  commissionPercent: number;
  status: string;
  recurringMonths: number;
  clicksThisMonth: number;
  conversions: number;
  pendingAmount: number;
  approvedAmount: number;
  paidAmount: number;
  currency: string;
  recentCommissions: {
    date: string;
    product: string;
    netBase: number;
    amount: number;
    status: string;
  }[];
  payouts: {
    id: string;
    periodStart: string;
    periodEnd: string;
    total: number;
    status: string;
    hasStatement: boolean;
  }[];
  referralUrl: string;
};
