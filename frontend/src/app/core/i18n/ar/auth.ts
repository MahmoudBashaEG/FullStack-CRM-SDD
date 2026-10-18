import type { Translations } from '../translations';

/** PLAT-001: Arabic strings for the 'auth' namespace. Must mirror en/auth.ts exactly (compile-time check). */
export const auth: Translations['auth'] = {
  signIn: {
    title: 'تسجيل الدخول',
    email: 'البريد الإلكتروني',
    password: 'كلمة المرور',
    emailRequired: 'البريد الإلكتروني مطلوب',
    emailInvalid: 'أدخل عنوان بريد إلكتروني صالحًا',
    passwordRequired: 'كلمة المرور مطلوبة',
    submit: 'تسجيل الدخول',
    fallback: 'البريد الإلكتروني أو كلمة المرور غير صحيحة.',
    newCustomer: 'عميل جديد؟',
    createAccount: 'إنشاء حساب',
  },
  signUp: {
    title: 'إنشاء حسابك',
    firstName: 'الاسم الأول',
    lastName: 'اسم العائلة',
    email: 'البريد الإلكتروني',
    phone: 'الهاتف (اختياري)',
    password: 'كلمة المرور',
    confirmPassword: 'تأكيد كلمة المرور',
    firstNameInvalid: 'الاسم الأول مطلوب (100 حرف كحد أقصى)',
    lastNameInvalid: 'اسم العائلة مطلوب (100 حرف كحد أقصى)',
    emailRequired: 'البريد الإلكتروني مطلوب',
    emailInvalid: 'أدخل عنوان بريد إلكتروني صالحًا',
    phoneInvalid: 'رقم الهاتف طويل جدًا',
    passwordHint: '8 أحرف على الأقل',
    passwordInvalid: 'يجب أن تتراوح كلمة المرور بين 8 و128 حرفًا',
    mismatch: 'كلمتا المرور غير متطابقتين',
    submit: 'إنشاء الحساب',
    fallback: 'تعذّر إنشاء الحساب.',
    haveAccount: 'لديك حساب بالفعل؟',
    signIn: 'تسجيل الدخول',
  },
};
