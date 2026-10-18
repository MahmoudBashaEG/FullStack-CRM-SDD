import type { Translations } from '../translations';

export const enums: Translations['enums'] = {
  ticketStatus: {
    Open: 'مفتوحة',
    InProgress: 'قيد المعالجة',
    Resolved: 'تم الحل',
    Closed: 'مغلقة',
  },
  ticketPriority: {
    Low: 'منخفضة',
    Medium: 'متوسطة',
    High: 'عالية',
    Urgent: 'عاجلة',
  },
  ticketCategory: {
    General: 'عام',
    Technical: 'تقني',
    Billing: 'الفوترة',
    Other: 'أخرى',
  },
  role: {
    SuperAdmin: 'مدير النظام',
    Agent: 'وكيل',
    Customer: 'عميل',
  },
};
