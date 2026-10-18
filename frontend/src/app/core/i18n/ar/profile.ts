import type { Translations } from '../translations';

/** PLAT-001: Arabic strings for the 'profile' namespace. Must mirror en/profile.ts exactly (compile-time check). */
export const profile: Translations['profile'] = {
  title: 'ملفك الشخصي',
  subtitle: 'حدّث بيانات الاتصال الخاصة بك أو غيّر كلمة المرور.',
  loadError: 'تعذر تحميل ملفك الشخصي.',
  customerLoadError: 'تعذر تحميل بيانات العميل الخاصة بك.',
  saveError: 'تعذر حفظ ملفك الشخصي.',
  saved: 'تم حفظ الملف الشخصي.',
  account: {
    title: 'تفاصيل الحساب',
    role: 'الدور',
    customerId: 'معرّف العميل',
    customerUnavailable: 'الشركة ومعرّف العميل غير متاحين إلى أن يتم تحميل بيانات العميل.',
    firstName: 'الاسم الأول',
    firstNameError: 'الاسم الأول مطلوب (بحد أقصى {max} حرفًا)',
    lastName: 'اسم العائلة',
    lastNameError: 'اسم العائلة مطلوب (بحد أقصى {max} حرفًا)',
    phone: 'الهاتف (اختياري)',
    phoneError: 'رقم الهاتف طويل جدًا',
    company: 'الشركة (اختياري)',
    companyError: 'اسم الشركة طويل جدًا (بحد أقصى {max} حرفًا)',
  },
  password: {
    title: 'تغيير كلمة المرور',
    current: 'كلمة المرور الحالية',
    currentError: 'كلمة المرور الحالية مطلوبة',
    new: 'كلمة المرور الجديدة',
    hint: '8 أحرف على الأقل',
    newError: 'يجب أن تتراوح كلمة المرور بين 8 و128 حرفًا',
    confirm: 'تأكيد كلمة المرور الجديدة',
    mismatch: 'كلمتا المرور غير متطابقتين',
    submit: 'تغيير كلمة المرور',
    changed: 'تم تغيير كلمة المرور. يرجى تسجيل الدخول مرة أخرى.',
    error: 'تعذر تغيير كلمة المرور.',
  },
};
