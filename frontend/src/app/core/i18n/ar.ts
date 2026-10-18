import { serverMessages } from './ar/serverMessages';
import { app } from './ar/app';
import { common } from './ar/common';
import { nav } from './ar/nav';
import { shell } from './ar/shell';
import { titles } from './ar/titles';
import { auth } from './ar/auth';
import { home } from './ar/home';
import { profile } from './ar/profile';
import { dashboard } from './ar/dashboard';
import { tickets } from './ar/tickets';
import { customers } from './ar/customers';
import { agents } from './ar/agents';
import { tasks } from './ar/tasks';
import { notifications } from './ar/notifications';
import { quickReplies } from './ar/quickReplies';
import { collaboration } from './ar/collaboration';
import { sla } from './ar/sla';
import { kb } from './ar/kb';
import { ai } from './ar/ai';
import { portal } from './ar/portal';
import { reports } from './ar/reports';
import { admin } from './ar/admin';
import { branding } from './ar/branding';
import { errors } from './ar/errors';
import { validation } from './ar/validation';
import { enums } from './ar/enums';
import { dates } from './ar/dates';
import { charts } from './ar/charts';
import { paginator } from './ar/paginator';
import { datepicker } from './ar/datepicker';
import type { Translations } from './translations';

/** Must mirror EN exactly: a missing or extra key fails ng build (PLAT-001 D3, completeness inventory). Lazy chunk. */
export const AR: Translations = {
  app,
  common,
  nav,
  shell,
  titles,
  auth,
  home,
  profile,
  dashboard,
  tickets,
  customers,
  agents,
  tasks,
  notifications,
  quickReplies,
  collaboration,
  sla,
  kb,
  ai,
  portal,
  reports,
  admin,
  branding,
  errors,
  validation,
  enums,
  dates,
  charts,
  paginator,
  datepicker,
  serverMessages,
};
