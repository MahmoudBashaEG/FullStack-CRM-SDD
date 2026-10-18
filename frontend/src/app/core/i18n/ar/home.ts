import type { Translations } from '../translations';

/** PLAT-001: Arabic strings for the 'home' namespace. Must mirror en/home.ts exactly (compile-time check). */
export const home: Translations['home'] = {
  welcome: 'مرحبًا، {name}',
  subtitle: 'لقد سجّلت الدخول إلى نظام إدارة علاقات العملاء للدعم. تظهر الوحدات هنا فور إتاحتها.',
  customer: {
    title: 'دعم العملاء',
    body: 'أرسل طلبًا، وتابع تقدمه، واعثر على الإجابات في مركز المساعدة.',
    helpCenter: 'مركز المساعدة',
    openPortal: 'فتح بوابة الدعم',
  },
  status: {
    title: 'حالة النظام',
    checking: 'جارٍ التحقق...',
    online: 'واجهة البرمجة متصلة',
    offline: 'واجهة البرمجة غير متاحة',
    report: 'أفادت الخدمة {service} بالحالة {status} في {time}.',
    httpStatus: 'استجابت واجهة البرمجة بالحالة {status}.',
    checkAgain: 'تحقق مرة أخرى',
  },
};
