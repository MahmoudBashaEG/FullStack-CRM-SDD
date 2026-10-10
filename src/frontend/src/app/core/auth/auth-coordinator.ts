export class AuthCoordinator {
  generation = 0;
  private flight: Promise<unknown> | null = null;
  run<T>(operation: () => Promise<T>): Promise<T> {
    if (this.flight) return this.flight as Promise<T>;
    const shared = operation().finally(() => { if (this.flight === shared) this.flight = null; });
    this.flight = shared; return shared;
  }
  // Reject stale memory state immediately, but drain the transport before another
  // cookie transition: generation checks cannot undo a browser-applied Set-Cookie.
  invalidate(): void { this.generation++; }
  settle():Promise<void> { return this.flight ? this.flight.then(()=>undefined,()=>undefined) : Promise.resolve(); }
}
