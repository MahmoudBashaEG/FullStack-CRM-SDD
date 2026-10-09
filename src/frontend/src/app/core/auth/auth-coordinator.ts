export class AuthCoordinator {
  generation = 0;
  private flight: Promise<unknown> | null = null;
  run<T>(operation: () => Promise<T>): Promise<T> {
    if (this.flight) return this.flight as Promise<T>;
    const shared = operation().finally(() => { if (this.flight === shared) this.flight = null; });
    this.flight = shared; return shared;
  }
  invalidate(): void { this.generation++; this.flight = null; }
  settle():Promise<void> { return this.flight ? this.flight.then(()=>undefined,()=>undefined) : Promise.resolve(); }
}
