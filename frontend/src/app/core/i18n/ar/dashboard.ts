import type { Translations } from '../translations';

/** PLAT-001: Arabic strings for the 'dashboard' namespace. Must mirror en/dashboard.ts exactly (compile-time check). */
export const dashboard: Translations['dashboard'] = {
  title: 'لوحة التحكم الخاصة بي',
  subtitle: 'نظرة مباشرة على التذاكر المسندة إليك حاليًا.',
  updated: 'آخر تحديث {date}',
  loadError: 'تعذر تحميل لوحة التحكم الخاصة بك.',
  tasksLoadError: 'تعذر تحميل مهامك.',
  tabs: {
    overview: 'نظرة عامة',
    tasks: 'مهامي',
  },
  actions: {
    aria: 'روابط سريعة',
    createTicket: 'إنشاء تذكرة',
    viewAll: 'عرض جميع تذاكري',
    tasks: 'مهامي',
    notifications: 'الإشعارات',
    quickReplies: 'الردود السريعة',
    collaborations: 'تعاوناتي',
  },
  workload: {
    title: 'عبء العمل',
    hint: '{active} تذكرة نشطة (مفتوحة + قيد المعالجة). الأعداد متداخلة — قد تظهر التذكرة الواحدة في عدة بطاقات.',
    escalated: 'مصعّدة',
    highOrUrgent: 'عالية / عاجلة',
  },
  queue: {
    title: 'تحتاج إلى اهتمام',
    hint: 'أهم {limit} من تذاكرك المفتوحة وقيد المعالجة: المصعّدة أولًا، ثم العاجلة والعالية والمتوسطة والمنخفضة، ثم الأطول بلا تحديث. هذا ترتيب عرض وليس اتفاقية مستوى خدمة.',
    empty: 'لا توجد تذاكر مفتوحة أو قيد المعالجة مسندة إليك حاليًا.',
    updated: 'آخر تحديث {date}',
    showing: 'عرض {shown} من {total} تذكرة نشطة.',
  },
  tasks: {
    title: 'مهامي',
    hint: 'المتأخرة والمستحقة قريبًا ضمن المعلّقة. مستحقة قريبًا = خلال 7 أيام.',
    pending: 'معلّقة',
    overdue: 'متأخرة',
    dueSoon: 'مستحقة قريبًا',
    empty: 'لا توجد مهام معلّقة.',
    due: 'الاستحقاق {date}',
    noDue: 'لا يوجد تاريخ استحقاق',
    reminder: 'التذكير {date}',
    viewAll: 'عرض جميع مهامي',
  },
  sla: {
    NotTracked: 'غير متتبَّع',
    OnTrack: 'ضمن المسار',
    Warning: 'تحذير',
    Breached: 'تم التجاوز',
    Met: 'تم الالتزام',
  },
};
