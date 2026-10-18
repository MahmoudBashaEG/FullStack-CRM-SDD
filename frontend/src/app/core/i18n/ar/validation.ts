import type { Translations } from '../translations';

export const validation: Translations['validation'] = {
  required: 'هذا الحقل مطلوب.',
  email: 'أدخل عنوان بريد إلكتروني صالحًا.',
  minLength: 'أدخل {min} أحرف على الأقل.',
  maxLength: 'أدخل {max} حرفًا على الأكثر.',
  invalidDate: 'أدخل تاريخًا صالحًا.',
  timeRequired: 'اختر الوقت أيضًا.',
  dateRequired: 'اختر التاريخ أيضًا.',
  pattern: 'القيمة ليست بالتنسيق المتوقع.',
  min: 'يجب ألا تقل القيمة عن {min}.',
  max: 'يجب ألا تزيد القيمة عن {max}.',
};
