import { computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { map } from 'rxjs';

/** Binds a mat-tab-group selectedIndex to ?tab=<id> (replaceUrl, other query params kept). Unknown or absent ⇒ the first tab. Call in an injection context. */
export function tabQuery(ids: readonly string[]) {
  const route = inject(ActivatedRoute);
  const router = inject(Router);
  const param = toSignal(route.queryParamMap.pipe(map((params) => params.get('tab'))), { initialValue: null });
  const index = computed(() => Math.max(0, ids.indexOf(param() ?? '')));
  const select = (selected: number) =>
    void router.navigate([], {
      relativeTo: route,
      queryParams: { tab: selected === 0 ? null : ids[selected] },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  return { index, select };
}
